using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using ChogZombies.LevelGen;
using ChogZombies.Player;
using ChogZombies.Enemies;
using ChogZombies.Loot;
using ChogZombies.Reown;
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
        SwitchboardRandomnessService.RandomnessResult _runVrf;
        SwitchboardRandomnessService.RandomnessResult _bossLootVrf;

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

        CancellationTokenSource _startupCts;

        RunState _state = RunState.Playing;
        int _levelIndexUsed;
        BossBehaviour _boss;
        bool _lootRolled;
        bool _levelBuilt;

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

            Debug.Log($"RunGameController: Start. levelIndex={_levelIndexUsed} requireWalletConnectedToStart={requireWalletConnectedToStart} useVrfForRunSeed={useVrfForRunSeed} useVrfForBossLoot={useVrfForBossLoot}");

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
                    enabled = false;
                    return;
                }
            }

            if (player == null)
            {
                player = FindObjectOfType<Player.PlayerCombatController>();
                if (player == null)
                {
                    Debug.LogWarning("RunGameController: aucun PlayerCombatController trouvé dans la scène.");
                }
            }

            AutoRunner autoRunner = null;
            if (player != null)
            {
                autoRunner = player.GetComponent<AutoRunner>();
                if (autoRunner != null)
                {
                    autoRunner.Enabled = false;
                }
            }

            if (useVrfForRunSeed)
            {
                if (vrfService == null)
                    vrfService = FindObjectOfType<SwitchboardRandomnessService>();

                if (vrfService == null)
                {
                    Debug.LogError("[VRF] Strict mode: SwitchboardRandomnessService not found. Aborting run.");
                    enabled = false;
                    return;
                }

                try
                {
                    Debug.Log("[VRF] Run seed: requesting randomness...");
                    _runVrf = await vrfService.RequestAndSettleRandomnessAsync(vrfMinSettlementDelaySeconds);
                    seed = vrfService.DeriveSeed(_runVrf.Value);
                    Debug.Log($"[VRF] Run seed resolved. randomnessId={_runVrf.RandomnessId} value={_runVrf.Value} seed={seed} createTx={_runVrf.CreateTxHash} settleTx={_runVrf.SettleTxHash}");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[VRF] Strict mode: Run seed VRF failed. Aborting run. Error: {e.Message}");
                    enabled = false;
                    return;
                }
            }

            if (levelVisualizer == null)
                levelVisualizer = FindObjectOfType<LevelRuntimeVisualizer>();

            if (levelVisualizer == null)
            {
                Debug.LogError("RunGameController: LevelRuntimeVisualizer not found in scene. Aborting run.");
                enabled = false;
                return;
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
                autoRunner.Enabled = true;
            }
            _boss = FindObjectOfType<BossBehaviour>();

            if (player != null)
            {
                var lootController = player.GetComponent<PlayerLootController>();
                if (lootController == null)
                    lootController = player.gameObject.AddComponent<PlayerLootController>();

                var meta = FindObjectOfType<ChogZombies.Loot.MetaProgressionController>();
                if (meta != null)
                    meta.ApplyOwnedToPlayer(lootController);
            }
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
                TryRollBossLoot();
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
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        void NextLevel()
        {
            s_currentLevelIndex = _levelIndexUsed + 1;
            var scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex);
        }

        async void TryRollBossLoot()
        {
            if (_lootRolled)
                return;

            _lootRolled = true;

            if (player == null)
                return;

            AddGold(goldOnBossKill);

            if (bossLootTable == null)
                return;

            if (useVrfForBossLoot)
            {
                if (vrfService == null)
                    vrfService = FindObjectOfType<SwitchboardRandomnessService>();

                if (vrfService == null)
                {
                    Debug.LogError("[VRF] Strict mode: SwitchboardRandomnessService not found. Boss loot cannot be rolled.");
                    return;
                }

                try
                {
                    if (_bossLootVrf == null)
                    {
                        _bossLootVrf = await vrfService.RequestAndSettleRandomnessAsync(vrfMinSettlementDelaySeconds);
                        Debug.Log($"[VRF] Boss loot randomness resolved. randomnessId={_bossLootVrf.RandomnessId} value={_bossLootVrf.Value} createTx={_bossLootVrf.CreateTxHash} settleTx={_bossLootVrf.SettleTxHash}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[VRF] Strict mode: Boss loot VRF failed. Loot not rolled. Error: {e.Message}");
                    return;
                }
            }

            int rngSeed = seed ^ (_levelIndexUsed * 73856093) ^ unchecked((int)0x9e3779b9);
            if (useVrfForBossLoot)
            {
                rngSeed = (_levelIndexUsed * 73856093) ^ unchecked((int)0x9e3779b9);
                rngSeed = rngSeed ^ vrfService.DeriveSeed(_bossLootVrf.Value);
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
                meta.TryAddOwned(item);

            lootController.TryApplyLoot(item);

            Debug.Log($"Boss loot: obtained {item.DisplayName} ({item.EffectType} +{item.EffectValue}).");
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
                return;

            s_currentGold += amount;
            Debug.Log($"Gold: +{amount} (total {s_currentGold})");
        }

        public bool TrySpendGold(int cost)
        {
            if (cost <= 0)
                return true;

            if (s_currentGold < cost)
                return false;

            s_currentGold -= cost;
            Debug.Log($"Gold: -{cost} (total {s_currentGold})");
            return true;
        }
    }
}
