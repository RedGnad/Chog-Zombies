using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using ChogZombies.LevelGen;
using ChogZombies.Player;
using ChogZombies.Enemies;
using ChogZombies.Loot;

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

        [Header("Loot")]
        [SerializeField] LootTable bossLootTable;
        [SerializeField] float bossLootDropChance = 0.4f;

        [Header("Economy")]
        [SerializeField] int startingGold = 0;
        [SerializeField] int goldOnBossKill = 3;

        static int s_currentLevelIndex = 0;
        static int s_currentGold = 0;

        RunState _state = RunState.Playing;
        int _levelIndexUsed;
        BossBehaviour _boss;
        bool _lootRolled;

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
        }

        void Start()
        {
            _levelIndexUsed = s_currentLevelIndex;

            if (player == null)
            {
                player = FindObjectOfType<Player.PlayerCombatController>();
                if (player == null)
                {
                    Debug.LogWarning("RunGameController: aucun PlayerCombatController trouvé dans la scène.");
                }
            }

            if (levelVisualizer != null)
            {
                levelVisualizer.BuildWithParams(_levelIndexUsed, seed);
            }
            _boss = FindObjectOfType<BossBehaviour>();
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

        void TryRollBossLoot()
        {
            if (_lootRolled)
                return;

            _lootRolled = true;

            if (player == null)
                return;

            AddGold(goldOnBossKill);

            if (bossLootTable == null)
                return;

            int rngSeed = seed ^ (_levelIndexUsed * 73856093) ^ unchecked((int)0x9e3779b9);
            var rng = new System.Random(rngSeed);

            double roll = rng.NextDouble();
            if (roll > bossLootDropChance)
            {
                Debug.Log("Boss loot: no drop this time.");
                return;
            }

            var item = bossLootTable.RollItem(rng);
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

            lootController.ApplyLoot(item);

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
