using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using ChogZombies.LevelGen;
using ChogZombies.Player;
using ChogZombies.Enemies;
using ChogZombies.Loot;
using ChogZombies.Reown;
using ChogZombies.UI;
using global::Reown.AppKit.Unity;

namespace ChogZombies.Game
{
    public class RunGameController : MonoBehaviour
    {
        public enum RunState
        {
            Playing,
            Won,
            Lost
        }

        [Header("References")]
        [SerializeField] LevelRuntimeVisualizer levelVisualizer;
        [SerializeField] PlayerCombatController player;

        [Header("Run Parameters")]
        [SerializeField] int startingLevelIndex = 1;
        [SerializeField] int seed = 12345;

        [Header("VRF (Switchboard)")]
        [SerializeField] bool useVrfForRunSeed;
        [SerializeField] bool useVrfForBossLoot;
        [SerializeField] int vrfMinSettlementDelaySeconds = 5;
        [SerializeField] SwitchboardRandomnessService vrfService;
        [SerializeField] VRFLoadingUIBridge vrfLoadingUI;
        SwitchboardRandomnessService.RandomnessResult _runVrf;
        SwitchboardRandomnessService.RandomnessResult _bossLootVrf;

        [Header("VRF (Pyth Entropy)")]
        [SerializeField] bool usePythEntropyForRunSeed;
        [SerializeField] bool usePythEntropyForBossLoot;
        [SerializeField] PythEntropyRandomnessService pythEntropyService;

        [Header("Wallet Gate")]
        [SerializeField] bool requireWalletConnectedToStart;
        [SerializeField] int walletGateTimeoutSeconds = 300;

        [Header("Loot")]
        [SerializeField] LootTable bossLootTable;
        [SerializeField] float bossLootDropChance = 0.4f;

        [Header("Economy")]
        [SerializeField] int startingGold = 0;
        [SerializeField] int goldOnBossKill = 3;

        static int s_currentLevelIndex = 0;
        static int s_currentGold = 0;

        static int s_cachedRunBaseSeed = 0;
        static string s_cachedRunSeedWalletAddress = null;
        static SwitchboardRandomnessService.RandomnessResult s_cachedRunVrf = null;

        [Serializable]
        class UserStateDto
        {
            public string walletAddress;
            public string[] ownedItems;
            public string[] equippedItems;
            public int runBaseSeed;
            public int gold;
        }

        CancellationTokenSource _startupCts;

        RunState _state = RunState.Playing;
        int _levelIndexUsed;
        BossBehaviour _boss;
        bool _lootRolled;
        bool _levelBuilt;
        bool _bossRewardGranted;

        bool _bossLootVrfInFlight;
        bool _runSeedRerollInFlight;
        bool _sceneLoadInFlight;

        Vector3 _playerSpawnPosition;
        Quaternion _playerSpawnRotation;
        bool _playerSpawnCaptured;

        int DeriveLevelSeed(int runBaseSeed, int levelIndex)
        {
            unchecked
            {
                int x = runBaseSeed;
                x ^= levelIndex * 73856093;
                x ^= (x << 13);
                x ^= (x >> 17);
                x ^= (x << 5);
                return x;
            }
        }

        public bool IsBossLootVrfAvailable => _state == RunState.Won && useVrfForBossLoot && !_lootRolled && !_bossLootVrfInFlight;

        public bool IsRerollRunSeedAvailable
        {
            get
            {
                if (!useVrfForRunSeed)
                    return false;

                if (_state != RunState.Won && _state != RunState.Lost)
                    return false;

                if (_runSeedRerollInFlight)
                    return false;

                try
                {
                    return AppKit.IsAccountConnected;
                }
                catch
                {
                    return false;
                }
            }
        }

        public void OnBossLootVrfButton()
        {
            if (_state != RunState.Won)
                return;

            if (_bossLootVrfInFlight)
                return;

            TryRollBossLoot(true);
        }

        public async void OnRerollRunSeedButton()
        {
            // Nécessite au moins un provider VRF configuré
            if (!usePythEntropyForRunSeed && !useVrfForRunSeed)
                return;

            if (_runSeedRerollInFlight)
                return;

            string currentWalletAddress = null;
            try
            {
                if (AppKit.IsAccountConnected)
                    currentWalletAddress = AppKit.Account.Address;
            }
            catch
            {
                currentWalletAddress = null;
            }

            if (string.IsNullOrWhiteSpace(currentWalletAddress))
                return;

            try
            {
                _runSeedRerollInFlight = true;
                if (vrfLoadingUI == null)
                    vrfLoadingUI = FindObjectOfType<VRFLoadingUIBridge>();
                vrfLoadingUI?.ShowLoading(VRFLoadingUI.VRFContext.RerollSeed);

                bool rerollDone = false;
                Exception lastError = null;

                // 1) Pyth Entropy en priorité si activé
                if (usePythEntropyForRunSeed)
                {
                    if (pythEntropyService == null)
                        pythEntropyService = FindObjectOfType<PythEntropyRandomnessService>();

                    if (pythEntropyService == null)
                    {
                        Debug.LogError("[Pyth][Reroll] PythEntropyRandomnessService not found.");
                    }
                    else
                    {
                        try
                        {
                            Debug.Log("[Pyth][Reroll] Requesting new run seed...");
                            int newBaseSeed = await pythEntropyService.RequestRunSeedAsync(_startupCts.Token);

                            // On écrase le cache de baseSeed pour ce wallet
                            s_cachedRunBaseSeed = newBaseSeed;
                            s_cachedRunSeedWalletAddress = currentWalletAddress;
                            // On invalide le cache Switchboard pour ce run
                            s_cachedRunVrf = null;

                            rerollDone = true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[Pyth][Reroll] Run seed Entropy failed. Error: {e.Message}");
                            lastError = e;
                        }
                    }
                }

                // 2) Fallback Switchboard si configuré et si Pyth n'a pas réussi
                if (!rerollDone && useVrfForRunSeed)
                {
                    if (vrfService == null)
                        vrfService = FindObjectOfType<SwitchboardRandomnessService>();

                    if (vrfService == null)
                    {
                        Debug.LogError("[VRF][Reroll] SwitchboardRandomnessService not found.");
                    }
                    else
                    {
                        try
                        {
                            Debug.Log("[VRF][Reroll] Requesting new run seed (Switchboard)...");
                            var newVrf = await vrfService.RequestAndSettleRandomnessAsync(vrfMinSettlementDelaySeconds);
                            int newBaseSeed = vrfService.DeriveSeed(newVrf.Value);

                            s_cachedRunVrf = newVrf;
                            s_cachedRunBaseSeed = newBaseSeed;
                            s_cachedRunSeedWalletAddress = currentWalletAddress;

                            rerollDone = true;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[VRF][Reroll] Run seed VRF (Switchboard) failed. Error: {e.Message}");
                            lastError = e;
                        }
                    }
                }

                if (!rerollDone)
                {
                    if (lastError != null)
                        vrfLoadingUI?.ShowError(lastError.Message);
                    return;
                }

                PushRunStateToBackendIfPossible();

                // Une nouvelle baseSeed VRF a été choisie, on recharge le même niveau avec cette seed
                ReloadSameLevel();
            }
            catch (Exception e)
            {
                Debug.LogError($"[VRF] Reroll run seed failed. Error: {e.Message}");
                vrfLoadingUI?.ShowError(e.Message);
            }
            finally
            {
                _runSeedRerollInFlight = false;
            }
        }

        public static int CurrentLevelIndex => s_currentLevelIndex;
        public static int CurrentGold => s_currentGold;
        public RunState State => _state;
        public int Seed => seed;

        void Awake()
        {
            if (s_currentLevelIndex <= 0)
            {
                s_currentLevelIndex = Mathf.Max(1, startingLevelIndex);
                s_currentGold = Mathf.Max(0, startingGold);
            }

            _startupCts = new CancellationTokenSource();
        }

        void OnDestroy()
        {
            _startupCts?.Cancel();
            _startupCts?.Dispose();
            _startupCts = null;
        }

        async Task WaitForAppKitInitializedAsync(DateTime deadlineUtc, CancellationToken ct)
        {
            if (AppKit.IsInitialized)
                return;

            Debug.Log("RunGameController: waiting for AppKit initialization...");

            var nextLogAt = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!AppKit.IsInitialized)
            {
                ct.ThrowIfCancellationRequested();
                if (!this || !isActiveAndEnabled)
                    throw new OperationCanceledException("RunGameController disabled");
                if (DateTime.UtcNow > deadlineUtc)
                    throw new TimeoutException("AppKit not initialized (timeout)");

                if (DateTime.UtcNow >= nextLogAt)
                {
                    var remaining = Math.Max(0, (int)(deadlineUtc - DateTime.UtcNow).TotalSeconds);
                    Debug.Log($"RunGameController: still waiting for AppKit init... remaining={remaining}s");
                    nextLogAt = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                }

                await Task.Yield();
            }

            Debug.Log("RunGameController: AppKit initialized.");
        }

        async Task WaitForWalletConnectedAsync(DateTime deadlineUtc, CancellationToken ct)
        {
            if (AppKit.IsAccountConnected)
                return;

            Debug.Log("RunGameController: waiting for wallet connection...");

            var nextLogAt = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!AppKit.IsAccountConnected)
            {
                ct.ThrowIfCancellationRequested();
                if (!this || !isActiveAndEnabled)
                    throw new OperationCanceledException("RunGameController disabled");
                if (DateTime.UtcNow > deadlineUtc)
                    throw new TimeoutException("Wallet not connected (timeout)");

                if (DateTime.UtcNow >= nextLogAt)
                {
                    var remaining = Math.Max(0, (int)(deadlineUtc - DateTime.UtcNow).TotalSeconds);
                    Debug.Log($"RunGameController: still waiting for wallet... remaining={remaining}s");
                    nextLogAt = DateTime.UtcNow + TimeSpan.FromSeconds(5);
                }

                await Task.Yield();
            }

            var addr = AppKit.Account.Address;
            Debug.Log($"RunGameController: wallet connected. address={addr}");
        }

        async void Start()
        {
            _levelIndexUsed = s_currentLevelIndex;

            Debug.Log($"RunGameController: Start. levelIndex={_levelIndexUsed} requireWalletConnectedToStart={requireWalletConnectedToStart} usePythEntropyForRunSeed={usePythEntropyForRunSeed} useVrfForRunSeed={useVrfForRunSeed} usePythEntropyForBossLoot={usePythEntropyForBossLoot} useVrfForBossLoot={useVrfForBossLoot}");

            if (player == null)
            {
                player = FindObjectOfType<Player.PlayerCombatController>();
                if (player == null)
                {
                    Debug.LogWarning("RunGameController: aucun PlayerCombatController trouvé dans la scène.");
                }
            }

            if (player != null)
            {
                var difficulty = GameDifficultySettings.Instance;
                if (difficulty != null)
                    difficulty.ApplyToPlayer(player);
            }

            AutoRunner autoRunner = null;
            if (player != null)
            {
                if (!_playerSpawnCaptured)
                {
                    _playerSpawnPosition = player.transform.position;
                    _playerSpawnRotation = player.transform.rotation;
                    _playerSpawnCaptured = true;
                }

                autoRunner = player.GetComponent<AutoRunner>();
                if (autoRunner != null)
                {
                    autoRunner.Enabled = false;
                }
            }

            if (requireWalletConnectedToStart)
            {
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Mathf.Max(1, walletGateTimeoutSeconds));
                try
                {
                    await WaitForAppKitInitializedAsync(deadline, _startupCts.Token);
                    await WaitForWalletConnectedAsync(deadline, _startupCts.Token);
                }
                catch (Exception e)
                {
                    Debug.LogError($"RunGameController: startup gate failed. Aborting run. Error: {e.Message}");
                    if (this != null)
                        enabled = false;
                    return;
                }
            }

            bool runSeedResolved = false;

            // Adresse du wallet courant (commune aux chemins Pyth et Switchboard)
            string currentWalletAddress = null;
            try
            {
                if (AppKit.IsAccountConnected)
                    currentWalletAddress = AppKit.Account.Address;
            }
            catch
            {
                currentWalletAddress = null;
            }

            if (!string.IsNullOrWhiteSpace(currentWalletAddress))
            {
                try
                {
                    await PullUserStateFromBackendAsync(currentWalletAddress, _startupCts.Token);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"RunGameController: backend state pull failed (non blocking). Error: {e.Message}");
                }
            }

            // 1) Pyth Entropy comme provider principal pour le run seed si activé
            if (usePythEntropyForRunSeed)
            {
                if (pythEntropyService == null)
                    pythEntropyService = FindObjectOfType<PythEntropyRandomnessService>();

                if (pythEntropyService == null)
                {
                    Debug.LogError("[Pyth] Strict mode: PythEntropyRandomnessService not found. Aborting run.");
                    if (this != null)
                        enabled = false;
                    return;
                }

                // Si on a déjà un runBaseSeed en cache pour ce wallet, on le réutilise pour tous les niveaux
                if (s_cachedRunBaseSeed != 0
                    && !string.IsNullOrWhiteSpace(s_cachedRunSeedWalletAddress)
                    && !string.IsNullOrWhiteSpace(currentWalletAddress)
                    && string.Equals(s_cachedRunSeedWalletAddress, currentWalletAddress, StringComparison.OrdinalIgnoreCase))
                {
                    int runBaseSeed = s_cachedRunBaseSeed;
                    seed = DeriveLevelSeed(runBaseSeed, _levelIndexUsed);
                    Debug.Log($"[Pyth] Run seed: using cached base seed. levelIndex={_levelIndexUsed} baseSeed={runBaseSeed} seed={seed}");
                    runSeedResolved = true;
                }
                else
                {
                    try
                    {
                        Debug.Log("[Pyth] Run seed: requesting randomness...");
                        if (vrfLoadingUI == null)
                            vrfLoadingUI = FindObjectOfType<VRFLoadingUIBridge>();
                        vrfLoadingUI?.ShowLoading(VRFLoadingUI.VRFContext.RunSeed);

                        int runBaseSeed = await pythEntropyService.RequestRunSeedAsync(_startupCts.Token);
                        seed = DeriveLevelSeed(runBaseSeed, _levelIndexUsed);
                        Debug.Log($"[Pyth] Run seed resolved. baseSeed={runBaseSeed} levelIndex={_levelIndexUsed} seed={seed}");
                        vrfLoadingUI?.HideLoading();

                        // Mettre en cache pour les niveaux suivants de la même run / même wallet
                        s_cachedRunBaseSeed = runBaseSeed;
                        if (!string.IsNullOrWhiteSpace(currentWalletAddress))
                            s_cachedRunSeedWalletAddress = currentWalletAddress;

                        PushRunStateToBackendIfPossible();

                        runSeedResolved = true;
                    }
                    catch (Exception e)
                    {
                        if (useVrfForRunSeed)
                        {
                            Debug.LogWarning($"[Pyth] Run seed Entropy failed, falling back to Switchboard. Error: {e.Message}");
                            // On ne ferme pas ici le loading: le fallback Switchboard réutilisera l'UI via VRFLoadingUIBridge
                        }
                        else
                        {
                            Debug.LogError($"[Pyth] Strict mode: Run seed Entropy failed and no Switchboard fallback configured. Aborting run. Error: {e.Message}");
                            vrfLoadingUI?.ShowError(e.Message);
                            if (this != null)
                                enabled = false;
                            return;
                        }
                    }
                }
            }

            // 2) Fallback Switchboard si configuré et si Pyth n'a pas produit de seed
            if (useVrfForRunSeed && !runSeedResolved)
            {
                if (vrfService == null)
                    vrfService = FindObjectOfType<SwitchboardRandomnessService>();

                if (vrfService == null)
                {
                    Debug.LogError("[VRF] Strict mode: SwitchboardRandomnessService not found. Aborting run.");
                    if (this != null)
                        enabled = false;
                    return;
                }

                if (s_cachedRunVrf != null
                    && !string.IsNullOrWhiteSpace(s_cachedRunSeedWalletAddress)
                    && !string.IsNullOrWhiteSpace(currentWalletAddress)
                    && string.Equals(s_cachedRunSeedWalletAddress, currentWalletAddress, StringComparison.OrdinalIgnoreCase))
                {
                    _runVrf = s_cachedRunVrf;
                    seed = DeriveLevelSeed(s_cachedRunBaseSeed, _levelIndexUsed);
                    Debug.Log($"[VRF] Run seed: using cached run seed. levelIndex={_levelIndexUsed} seed={seed} randomnessId={_runVrf.RandomnessId}");
                }
                else
                {
                    try
                    {
                        Debug.Log("[VRF] Run seed: requesting randomness (Switchboard)...");
                        if (vrfLoadingUI == null)
                            vrfLoadingUI = FindObjectOfType<VRFLoadingUIBridge>();
                        vrfLoadingUI?.ShowLoading(VRFLoadingUI.VRFContext.RunSeed);

                        _runVrf = await vrfService.RequestAndSettleRandomnessAsync(vrfMinSettlementDelaySeconds);
                        int runBaseSeed = vrfService.DeriveSeed(_runVrf.Value);
                        seed = DeriveLevelSeed(runBaseSeed, _levelIndexUsed);
                        Debug.Log($"[VRF] Run seed resolved (Switchboard). randomnessId={_runVrf.RandomnessId} value={_runVrf.Value} seed={seed} createTx={_runVrf.CreateTxHash} settleTx={_runVrf.SettleTxHash}");

                        if (!string.IsNullOrWhiteSpace(currentWalletAddress))
                        {
                            s_cachedRunVrf = _runVrf;
                            s_cachedRunBaseSeed = runBaseSeed;
                            s_cachedRunSeedWalletAddress = currentWalletAddress;
                        }

                        PushRunStateToBackendIfPossible();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[VRF] Strict mode: Run seed VRF failed. Aborting run. Error: {e.Message}");
                        vrfLoadingUI?.ShowError(e.Message);
                        if (this != null)
                            enabled = false;
                        return;
                    }
                }
            }

            if (levelVisualizer == null)
                levelVisualizer = FindObjectOfType<LevelRuntimeVisualizer>();

            if (levelVisualizer == null)
            {
                Debug.LogError("RunGameController: LevelRuntimeVisualizer not found in scene. Aborting run.");
                if (this != null)
                    enabled = false;
                return;
            }

            // Réinitialiser la position du joueur avant de construire le niveau (évite d’être déjà à la fin après la VRF)
            if (player != null)
            {
                if (_playerSpawnCaptured)
                {
                    player.transform.position = _playerSpawnPosition;
                    player.transform.rotation = _playerSpawnRotation;
                }
                var rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            if (levelVisualizer != null)
            {
                Debug.Log($"RunGameController: building level. levelIndex={_levelIndexUsed} seed={seed}");
                levelVisualizer.BuildWithParams(_levelIndexUsed, seed);
                _levelBuilt = true;
                Debug.Log("RunGameController: level built.");
            }

            if (autoRunner != null)
            {
                await Task.Yield();
                autoRunner.Enabled = true;
            }
            _boss = FindObjectOfType<BossBehaviour>();

            var lootController = EnsurePlayerLootController();
            if (lootController != null)
            {
                var meta = FindObjectOfType<ChogZombies.Loot.MetaProgressionController>();
                if (meta != null)
                    meta.ApplyEquippedToPlayer(lootController);
            }

            RegisterLootRevealCallbacks();
        }

        void Update()
        {
            if (_state == RunState.Playing)
            {
                UpdateRunState();
            }

            HandleInput();
        }

        void UpdateRunState()
        {
            if (!_levelBuilt)
                return;

            if (player != null && !player.IsAlive)
            {
                _state = RunState.Lost;
                Debug.Log("Run state: LOST");
                return;
            }

            if (_boss == null)
            {
                _boss = FindObjectOfType<BossBehaviour>();
            }

            if (_boss == null)
            {
                _state = RunState.Won;
                Debug.Log("Run state: WON");
                if (!_bossRewardGranted)
                {
                    _bossRewardGranted = true;
                    AddGold(goldOnBossKill);
                }

                TryRollBossLoot(false);
            }
        }

        void HandleInput()
        {
            if (Keyboard.current == null)
                return;

            if (_state == RunState.Lost && Keyboard.current.rKey.wasPressedThisFrame)
            {
                ReloadSameLevel();
            }
            else if (_state == RunState.Won && Keyboard.current.nKey.wasPressedThisFrame)
            {
                NextLevel();
            }
        }

        public void OnRetryButton()
        {
            if (_state != RunState.Lost)
                return;

            ReloadSameLevel();
        }

        public void OnNextLevelButton()
        {
            if (_state != RunState.Won)
                return;

            NextLevel();
        }

        void ReloadSameLevel()
        {
            if (_sceneLoadInFlight)
                return;
            _sceneLoadInFlight = true;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        void NextLevel()
        {
            if (_sceneLoadInFlight)
                return;
            _sceneLoadInFlight = true;
            s_currentLevelIndex = _levelIndexUsed + 1;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        async void TryRollBossLoot(bool forceVrf)
        {
            if (_lootRolled)
            {
                Debug.Log("[BossLoot] Already rolled, skipping.");
                return;
            }

            if (player == null)
            {
                Debug.LogWarning("[BossLoot] Early exit: player is null.");
                return;
            }

            if (bossLootTable == null)
            {
                Debug.LogWarning("[BossLoot] Early exit: bossLootTable is null.");
                return;
            }

            if ((useVrfForBossLoot || usePythEntropyForBossLoot) && !forceVrf)
            {
                Debug.Log("[BossLoot] VRF mode enabled and forceVrf=false. Waiting for explicit VRF roll.");
                return;
            }

            _lootRolled = true;

            Debug.Log($"[BossLoot] Rolling boss loot. usePythEntropyForBossLoot={usePythEntropyForBossLoot}, useVrfForBossLoot={useVrfForBossLoot}, dropChance={bossLootDropChance}");

            int rngSeed = 0;
            bool usedVrf = false;

            // 1) Pyth Entropy comme provider principal pour le boss loot si activé
            if (usePythEntropyForBossLoot)
            {
                if (pythEntropyService == null)
                    pythEntropyService = FindObjectOfType<PythEntropyRandomnessService>();

                if (pythEntropyService == null)
                {
                    Debug.LogError("[Pyth][BossLoot] Strict mode: PythEntropyRandomnessService not found. Boss loot cannot be rolled with Pyth.");
                }
                else
                {
                    try
                    {
                        if (vrfLoadingUI == null)
                            vrfLoadingUI = FindObjectOfType<VRFLoadingUIBridge>();
                        vrfLoadingUI?.ShowLoading(VRFLoadingUI.VRFContext.BossLoot);

                        int pythSeed = await pythEntropyService.RequestBossLootSeedAsync(CancellationToken.None);
                        Debug.Log($"[Pyth][BossLoot] Boss loot seed resolved. baseSeed={pythSeed} levelIndex={_levelIndexUsed}");

                        rngSeed = (_levelIndexUsed * 73856093) ^ unchecked((int)0x9e3779b9) ^ pythSeed;
                        usedVrf = true;
                        vrfLoadingUI?.HideLoading();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[Pyth][BossLoot] VRF failed. Error: {e.Message}");
                        vrfLoadingUI?.ShowError(e.Message);

                        // Pas de RNG locale si aucun autre VRF n'est configuré
                        if (!useVrfForBossLoot)
                        {
                            _lootRolled = false;
                            return;
                        }
                    }
                }
            }

            // 2) Fallback Switchboard si configuré et si Pyth n'a pas fourni de seed
            if (!usedVrf && useVrfForBossLoot)
            {
                if (vrfService == null)
                    vrfService = FindObjectOfType<SwitchboardRandomnessService>();

                if (vrfService == null)
                {
                    Debug.LogError("[VRF][BossLoot] Strict mode: SwitchboardRandomnessService not found. Boss loot cannot be rolled.");
                    _lootRolled = false;
                    return;
                }

                try
                {
                    if (_bossLootVrfInFlight)
                        return;

                    _bossLootVrfInFlight = true;
                    if (_bossLootVrf == null)
                    {
                        if (vrfLoadingUI == null)
                            vrfLoadingUI = FindObjectOfType<VRFLoadingUIBridge>();
                        vrfLoadingUI?.ShowLoading(VRFLoadingUI.VRFContext.BossLoot);

                        _bossLootVrf = await vrfService.RequestAndSettleRandomnessAsync(vrfMinSettlementDelaySeconds);
                        Debug.Log($"[VRF][BossLoot] Boss loot randomness resolved. randomnessId={_bossLootVrf.RandomnessId} value={_bossLootVrf.Value} createTx={_bossLootVrf.CreateTxHash} settleTx={_bossLootVrf.SettleTxHash}");
                    }

                    rngSeed = (_levelIndexUsed * 73856093) ^ unchecked((int)0x9e3779b9);
                    rngSeed = rngSeed ^ vrfService.DeriveSeed(_bossLootVrf.Value);
                    usedVrf = true;
                    vrfLoadingUI?.HideLoading();
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[VRF][BossLoot] Strict mode: Boss loot VRF failed. Loot not rolled. Error: {e.Message}");
                    vrfLoadingUI?.ShowError(e.Message);
                    _lootRolled = false;
                    return;
                }
                finally
                {
                    _bossLootVrfInFlight = false;
                }
            }

            // Si aucun VRF n'a pu produire une seed, on ne roll pas de loot (VRF pure)
            if (!usedVrf)
            {
                Debug.LogWarning("[BossLoot] No VRF provider succeeded. Boss loot not rolled.");
                _lootRolled = false;
                return;
            }

            var rng = new System.Random(rngSeed);

            double roll = rng.NextDouble();
            if (roll > bossLootDropChance)
            {
                Debug.Log("Boss loot: no drop this time.");
                return;
            }

            var meta = FindObjectOfType<ChogZombies.Loot.MetaProgressionController>();

            LootItemDefinition item = null;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var candidate = bossLootTable.RollItem(rng);
                if (candidate == null)
                {
                    item = null;
                    break;
                }

                if (meta == null || !meta.IsOwned(candidate))
                {
                    item = candidate;
                    break;
                }

                item = candidate;
            }
            if (item == null)
            {
                Debug.Log("Boss loot: table empty or no valid item.");
                return;
            }

            var lootController = player.GetComponent<PlayerLootController>();
            if (lootController == null)
            {
                lootController = player.gameObject.AddComponent<PlayerLootController>();
            }

            if (meta != null)
            {
                bool added = meta.TryAddOwned(item);
                if (added)
                {
                    try
                    {
                        var lootBackend = FindObjectOfType<LootBackendSync>();
                        lootBackend?.PushForCurrentWallet();
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[BossLoot] Failed to push loot state to backend: {e.Message}");
                    }
                }
            }

            Debug.Log($"Boss loot: obtained {item.DisplayName} ({item.Rarity} - {item.EffectType} +{item.EffectValue}).");

            // Afficher le reveal animé
            var lootRevealUI = LootRevealUI.Instance;
            if (lootRevealUI == null)
                lootRevealUI = FindObjectOfType<LootRevealUI>();
            if (lootRevealUI != null)
                lootRevealUI.Show(item);
        }

        async Task PullUserStateFromBackendAsync(string walletAddress, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                return;

            var lootBackend = FindObjectOfType<LootBackendSync>();
            if (lootBackend == null)
                return;

            var baseUrl = lootBackend.ApiBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
                return;

            string url = baseUrl.TrimEnd('/') + "/users/" + walletAddress.ToLowerInvariant();

            using (var request = UnityWebRequest.Get(url))
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogWarning($"RunGameController: backend GET state failed: {request.error}");
                    return;
                }

                var json = request.downloadHandler.text;
                if (string.IsNullOrEmpty(json))
                    return;

                UserStateDto dto = null;
                try
                {
                    dto = JsonUtility.FromJson<UserStateDto>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"RunGameController: failed to parse backend state JSON: {e.Message}");
                    return;
                }

                if (dto == null)
                    return;

                if (dto.runBaseSeed != 0)
                {
                    s_cachedRunBaseSeed = dto.runBaseSeed;
                    s_cachedRunSeedWalletAddress = walletAddress;
                }

                s_currentGold = Mathf.Max(0, dto.gold);
                Debug.Log($"RunGameController: backend state loaded. runBaseSeed={s_cachedRunBaseSeed} gold={s_currentGold}");

                // Appliquer l'état de loot (owned / equipped) s'il est présent dans la réponse backend
                try
                {
                    var meta = FindObjectOfType<ChogZombies.Loot.MetaProgressionController>();
                    if (meta != null)
                    {
                        var owned = dto.ownedItems ?? Array.Empty<string>();
                        var equipped = dto.equippedItems ?? Array.Empty<string>();
                        Debug.Log($"RunGameController: backend loot state owned=[{string.Join(",", owned)}] equipped=[{string.Join(",", equipped)}]");
                        meta.ApplyRemoteState(owned, equipped);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"RunGameController: failed to apply loot state from backend: {e.Message}");
                }
            }
        }

        async Task PushUserStateToBackendAsync(string walletAddress, int runBaseSeed, int gold, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(walletAddress))
                return;

            var lootBackend = FindObjectOfType<LootBackendSync>();
            if (lootBackend == null)
                return;

            var baseUrl = lootBackend.ApiBaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
                return;

            string url = baseUrl.TrimEnd('/') + "/users/" + walletAddress.ToLowerInvariant() + "/state";

            var dto = new UserStateDto
            {
                walletAddress = walletAddress.ToLowerInvariant(),
                runBaseSeed = runBaseSeed,
                gold = gold
            };

            string json = JsonUtility.ToJson(dto);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest(url, "PUT"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogWarning($"RunGameController: backend PUT state failed: {request.error}");
                    return;
                }

                Debug.Log("RunGameController: backend state updated.");
            }
        }

        async void PushRunStateToBackendIfPossible()
        {
            string walletAddress = null;
            try
            {
                if (AppKit.IsAccountConnected)
                    walletAddress = AppKit.Account.Address;
            }
            catch
            {
                walletAddress = null;
            }

            if (string.IsNullOrWhiteSpace(walletAddress))
                return;

            var ct = _startupCts != null ? _startupCts.Token : CancellationToken.None;

            try
            {
                await PushUserStateToBackendAsync(walletAddress, s_cachedRunBaseSeed, s_currentGold, ct);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"RunGameController: backend state push failed: {e.Message}");
            }
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            s_currentGold += amount;
            Debug.Log($"Gold: +{amount} (total {s_currentGold})");

            PushRunStateToBackendIfPossible();
        }

        public bool TrySpendGold(int cost)
        {
            if (cost <= 0)
                return true;

            if (s_currentGold < cost)
                return false;

            s_currentGold -= cost;
            Debug.Log($"Gold: -{cost} (total {s_currentGold})");
            PushRunStateToBackendIfPossible();
            return true;
        }

        void RegisterLootRevealCallbacks()
        {
            var lootReveal = LootRevealUI.Instance ?? FindObjectOfType<LootRevealUI>();
            if (lootReveal == null)
            {
                Debug.LogWarning("[RunGameController] LootRevealUI not found in scene. Equip button will do nothing.");
                return;
            }

            lootReveal.OnEquipClicked -= HandleLootRevealEquipClicked;
            lootReveal.OnEquipClicked += HandleLootRevealEquipClicked;
        }

        void HandleLootRevealEquipClicked(LootItemDefinition item)
        {
            if (item == null)
                return;

            var meta = FindObjectOfType<ChogZombies.Loot.MetaProgressionController>();
            if (meta == null)
            {
                Debug.LogWarning("[RunGameController] MetaProgressionController not found. Cannot equip loot.");
                return;
            }

            if (!meta.IsOwned(item))
            {
                Debug.LogWarning($"[RunGameController] Cannot equip '{item.DisplayName}' because it is not owned.");
                return;
            }

            var lootController = EnsurePlayerLootController();
            if (lootController == null)
            {
                Debug.LogWarning("[RunGameController] PlayerLootController not available. Cannot equip loot.");
                return;
            }

            bool isEquipped = meta.IsEquipped(item);
            if (isEquipped)
            {
                if (meta.SetEquipped(item, false))
                {
                    lootController.TryUnequip(item);
                }
            }
            else
            {
                if (meta.SetEquipped(item, true))
                {
                    lootController.TryEquip(item);
                }
            }
        }

        PlayerLootController EnsurePlayerLootController()
        {
            if (player == null)
                player = FindObjectOfType<Player.PlayerCombatController>();

            if (player == null)
                return null;

            var lootController = player.GetComponent<PlayerLootController>();
            if (lootController == null)
                lootController = player.gameObject.AddComponent<PlayerLootController>();
            return lootController;
        }
    }
}
