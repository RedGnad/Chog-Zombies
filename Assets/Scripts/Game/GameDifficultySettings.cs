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
        [SerializeField] int bossLateBoostStartLevel = 10;
        [SerializeField] int bossLateBoostFullLevel = 20;
        [SerializeField] float bossLateHpMaxMultiplier = 1.18f;
        [SerializeField] float bossLateDamageMaxMultiplier = 1.12f;

        public int StartingSoldiersOverride => startingSoldiersOverride;
        public float EnemyHpMultiplier => enemyHpMultiplier;
        public float BossHpMultiplier => bossHpMultiplier;
        public float BossDamageMultiplier => bossDamageMultiplier;
        public float EnemyLevelCurveExponent => enemyLevelCurveExponent;
        public float BossLevelCurveExponent => bossLevelCurveExponent;
        public float PlayerPowerDamageMultiplierFactor => playerPowerDamageMultiplierFactor;
        public float PlayerPowerDamageExponent => playerPowerDamageExponent;
        public float BossAttackIntervalMultiplier => bossAttackIntervalMultiplier;
        public int BossLateBoostStartLevel => bossLateBoostStartLevel;
        public int BossLateBoostFullLevel => bossLateBoostFullLevel;
        public float BossLateHpMaxMultiplier => bossLateHpMaxMultiplier;
        public float BossLateDamageMaxMultiplier => bossLateDamageMaxMultiplier;

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

        public static int GetBossLateBoostStartLevelOrDefault()
        {
            var s = Instance;
            return s == null ? 10 : Mathf.Max(1, s.bossLateBoostStartLevel);
        }

        public static int GetBossLateBoostFullLevelOrDefault()
        {
            var s = Instance;
            int start = GetBossLateBoostStartLevelOrDefault();
            if (s == null)
                return Mathf.Max(start + 1, 20);
            return Mathf.Max(start + 1, s.bossLateBoostFullLevel);
        }

        public static float GetBossLateHpMaxMultiplierOrDefault()
        {
            var s = Instance;
            return s == null ? 1.18f : Mathf.Max(1f, s.bossLateHpMaxMultiplier);
        }

        public static float GetBossLateDamageMaxMultiplierOrDefault()
        {
            var s = Instance;
            return s == null ? 1.12f : Mathf.Max(1f, s.bossLateDamageMaxMultiplier);
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
