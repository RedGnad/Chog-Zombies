using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.Player;
using ChogZombies.Game;
using ChogZombies.Effects;
using ChogZombies.Loot;

namespace ChogZombies.Enemies
{
    public class EnemyChaserGroupBehaviour : MonoBehaviour
    {
        [SerializeField] int enemyCount;
        [SerializeField] int hpPerEnemy = 14;

        [Header("Difficulty")]
        [SerializeField] float hpPerLevel = 0.18f;

        [Header("Rewards")]
        [SerializeField] int powerRewardPerEnemy = 1;
        [SerializeField] int maxPowerReward = 1;
        [SerializeField] float goldDropChance = 0.5f;
        [SerializeField] int goldReward = 1;
        [SerializeField] GameObject coinPickupPrefab;
        [SerializeField] GameObject deathVfxPrefab;
        [SerializeField] AudioClip deathSfxClip;
        [SerializeField, Range(0f, 1f)] float deathSfxVolume = 1f;

        [Header("Chaser Movement")]
        [SerializeField] float lateralAlignSpeed = 0.8f;
        [SerializeField] float maxLateralStepPerFrame = 0.10f;
        [SerializeField] float targetOffsetX = 0.0f;

        int _currentHp;
        int _goldRngSeed;
        int _levelIndex;
        PlayerCombatController _player;
        bool _activated;
        [SerializeField] float activationDistanceZ = 15.0f;
        float _fixedZ;
        float _spikeAuraAccumulatedDamage;

        public int EnemyCount => enemyCount;

        public void SetCoinPickupPrefab(GameObject prefab)
        {
            coinPickupPrefab = prefab;
        }

        public void SetDeathVfxPrefab(GameObject prefab)
        {
            deathVfxPrefab = prefab;
        }

        public void SetDeathSfxClip(AudioClip clip)
        {
            deathSfxClip = clip;
        }

        public void SetDeathSfxVolume(float volume)
        {
            deathSfxVolume = Mathf.Clamp01(volume);
        }

        public void Initialize(int count, int levelIndex)
        {
            enemyCount = count;
            _levelIndex = Mathf.Max(1, levelIndex);

            int l = _levelIndex;
            float k = GameDifficultySettings.EvaluateEnemyLevelCurveK(l);
            float hpScale = 1f + Mathf.Max(0f, hpPerLevel) * k;
            float globalMultiplier = GameDifficultySettings.GetEnemyHpMultiplierOrDefault();
            int effectiveHpPerEnemy = Mathf.Max(1, Mathf.RoundToInt(hpPerEnemy * hpScale * globalMultiplier));
            _currentHp = enemyCount * effectiveHpPerEnemy;

            _player = PlayerCombatController.Main;
            if (_player == null)
                _player = FindObjectOfType<PlayerCombatController>();

            _fixedZ = transform.position.z;

            int runSeed = 12345;
            var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
            if (run != null)
                runSeed = run.Seed;

            var p = transform.position;
            int px = Mathf.RoundToInt(p.x * 100f);
            int pz = Mathf.RoundToInt(p.z * 100f);
            _goldRngSeed = runSeed ^ (l * 19349663) ^ (px * 83492791) ^ (pz * 73856093) ^ (enemyCount * 297121507);
        }

        void Update()
        {
            var player = _player ?? PlayerCombatController.Main;
            if (player == null)
                return;

            var pos = transform.position;
            float playerZ = player.transform.position.z;

            // Tant que le joueur est loin derrière, on reste statique pour éviter un mouvement "magique" sur tout le couloir.
            if (!_activated)
            {
                if (playerZ + activationDistanceZ < pos.z)
                    return;

                _activated = true;
            }

            pos.z = _fixedZ;

            float targetX = player.transform.position.x + targetOffsetX;
            float step = Mathf.Max(0f, lateralAlignSpeed) * Time.deltaTime;
            float maxStep = Mathf.Max(0f, maxLateralStepPerFrame);
            if (maxStep > 0f)
                step = Mathf.Min(step, maxStep);
            pos.x = Mathf.MoveTowards(pos.x, targetX, step);

            transform.position = pos;

            ApplySpikeAuraDamage(player);
        }

        void ApplySpikeAuraDamage(PlayerCombatController player)
        {
            float radius;
            if (!player.TryGetActiveSpikeAuraRadius(out radius))
                return;

            float auraDps = Mathf.Max(0f, RunMetaEffects.SpikeAuraDamagePerSecond);
            if (auraDps <= 0f)
                return;

            Vector3 center = player.transform.position;
            Vector3 pos = transform.position;
            float radiusSqr = radius * radius;
            float distSqr = (pos - center).sqrMagnitude;
            if (distSqr > radiusSqr)
                return;

            _spikeAuraAccumulatedDamage += auraDps * Time.deltaTime;
            int dmgInt = Mathf.FloorToInt(_spikeAuraAccumulatedDamage);
            if (dmgInt <= 0)
                return;

            _spikeAuraAccumulatedDamage -= dmgInt;
            TakeDamage(dmgInt);

            if (player.DebugSpikeAuraHits)
            {
                float dist = Mathf.Sqrt(distSqr);
                Debug.Log($"[SpikeAura] EnemyChaserGroup '{name}' hit for {dmgInt} (radius={radius:F2}, dist={dist:F2}, dps={auraDps:F2})");
            }
        }

        public void TakeDamage(float damage)
        {
            int dmg = Mathf.RoundToInt(damage);
            _currentHp -= dmg;

            // Hit feedback
            var hitFeedback = GetComponent<HitFeedbackController>();
            if (hitFeedback != null)
                hitFeedback.TriggerHitFeedback();

            if (_currentHp <= 0)
            {
                int reward = Mathf.Clamp(enemyCount * powerRewardPerEnemy, 0, maxPowerReward);
                if (reward > 0 && PlayerCombatController.Main != null)
                {
                    PlayerCombatController.Main.AddPower(reward);
                }

                var rng = new System.Random(_goldRngSeed);

                if (goldReward > 0 && goldDropChance > 0f)
                {
                    if (rng.NextDouble() < goldDropChance)
                    {
                        var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
                        if (run != null)
                            run.AddGold(goldReward);
                    }
                }

                float coinDropChance = RunMetaEffects.CoinDropChancePerEnemy;
                if (coinDropChance > 0f && coinPickupPrefab != null)
                {
                    if (rng.NextDouble() < coinDropChance)
                    {
                        var pos = transform.position;
                        Instantiate(coinPickupPrefab, pos, Quaternion.identity);
                        Debug.Log($"[CoinDrop] EnemyChaserGroup '{name}' spawned a coin (chance={coinDropChance:P0}).");
                    }
                }

                if (deathVfxPrefab != null)
                {
                    var vfx = Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
                    var ps = vfx.GetComponent<ParticleSystem>();
                    if (ps != null)
                        Destroy(vfx, ps.main.duration + ps.main.startLifetime.constantMax);
                    else
                        Destroy(vfx, 2f);
                }
                
                if (deathSfxClip != null)
                {
                    AudioSource.PlayClipAtPoint(deathSfxClip, transform.position, deathSfxVolume);
                }

                Destroy(gameObject);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<Projectile>(out var projectile))
            {
                TakeDamage(projectile.Damage);
                Destroy(projectile.gameObject);
            }
        }
    }
}
