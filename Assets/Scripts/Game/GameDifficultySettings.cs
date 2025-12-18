using UnityEngine;

namespace ChogZombies.Game
{
    public class GameDifficultySettings : MonoBehaviour
    {
        public static GameDifficultySettings Instance { get; private set; }

        [Header("Player")]
        [SerializeField] int startingSoldiersOverride = -1;
        [SerializeField] float playerPowerDamageMultiplierFactor = 1f;
        [SerializeField] float playerPowerDamageExponent = 0.4f;

        [Header("Enemies")]
        [SerializeField] float enemyHpMultiplier = 1f;
        [SerializeField] float enemyLevelCurveExponent = 0.5f;

        [Header("Boss")]
        [SerializeField] float bossHpMultiplier = 1f;
        [SerializeField] float bossDamageMultiplier = 1f;
        [SerializeField] float bossLevelCurveExponent = 1f;
        [SerializeField] float bossAttackIntervalMultiplier = 1f;

        public int StartingSoldiersOverride => startingSoldiersOverride;
        public float EnemyHpMultiplier => enemyHpMultiplier;
        public float BossHpMultiplier => bossHpMultiplier;
        public float BossDamageMultiplier => bossDamageMultiplier;
        public float EnemyLevelCurveExponent => enemyLevelCurveExponent;
        public float BossLevelCurveExponent => bossLevelCurveExponent;
        public float PlayerPowerDamageMultiplierFactor => playerPowerDamageMultiplierFactor;
        public float PlayerPowerDamageExponent => playerPowerDamageExponent;
        public float BossAttackIntervalMultiplier => bossAttackIntervalMultiplier;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void ApplyToPlayer(ChogZombies.Player.PlayerCombatController player)
        {
            if (player == null)
                return;

            if (startingSoldiersOverride > 0)
                player.SetSoldierCount(startingSoldiersOverride);
        }

        public static float GetEnemyHpMultiplierOrDefault()
        {
            var s = Instance;
            return s == null ? 1f : Mathf.Max(0.05f, s.enemyHpMultiplier);
        }

        public static float GetPlayerPowerDamageMultiplierFactorOrDefault()
        {
            var s = Instance;
            return s == null ? 1f : Mathf.Max(0.05f, s.playerPowerDamageMultiplierFactor);
        }

        public static float GetPlayerPowerDamageExponentOrDefault()
        {
            var s = Instance;
            return s == null ? 0.4f : Mathf.Max(0.05f, s.playerPowerDamageExponent);
        }

        public static float GetEnemyLevelCurveExponentOrDefault()
        {
            var s = Instance;
            return s == null ? 0.5f : Mathf.Max(0.05f, s.enemyLevelCurveExponent);
        }

        public static float GetBossHpMultiplierOrDefault()
        {
            var s = Instance;
            return s == null ? 1f : Mathf.Max(0.05f, s.bossHpMultiplier);
        }

        public static float GetBossDamageMultiplierOrDefault()
        {
            var s = Instance;
            return s == null ? 1f : Mathf.Max(0.05f, s.bossDamageMultiplier);
        }

        public static float GetBossAttackIntervalMultiplierOrDefault()
        {
            var s = Instance;
            return s == null ? 1f : Mathf.Max(0.05f, s.bossAttackIntervalMultiplier);
        }

        public static float GetBossLevelCurveExponentOrDefault()
        {
            var s = Instance;
            return s == null ? 1f : Mathf.Max(0.05f, s.bossLevelCurveExponent);
        }

        public static float EvaluateEnemyLevelCurveK(int levelIndex)
        {
            float k = Mathf.Max(0f, Mathf.Max(1, levelIndex) - 1f);
            return Mathf.Pow(k, GetEnemyLevelCurveExponentOrDefault());
        }

        public static float EvaluateBossLevelCurveK(int levelIndex)
        {
            float k = Mathf.Max(0f, Mathf.Max(1, levelIndex) - 1f);
            return Mathf.Pow(k, GetBossLevelCurveExponentOrDefault());
        }
    }
}
