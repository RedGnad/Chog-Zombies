using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.Player;
using ChogZombies.Game;
using ChogZombies.Effects;
using ChogZombies.Loot;

namespace ChogZombies.Enemies
{
    public class EnemyGroupBehaviour : MonoBehaviour
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

        [Header("Movement")]
        [SerializeField] bool enableSideMovement = true;
        [SerializeField] bool sweepFullWidth = true;
        [SerializeField] float sweepMargin = 0.25f;
        [SerializeField] float sideAmplitude = 3.5f;
        [SerializeField] float sideSpeed = 0.5f;
        [SerializeField] float amplitudePerLevel = 0.04f;
        [SerializeField] float speedPerLevel = 0.005f;

        int _currentHp;
        Vector3 _basePosition;
        float _phase;
        float _effectiveAmplitude;
        float _effectiveSpeed;
        float _laneCenterX;
        int _goldRngSeed;
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

        public void Initialize(int count)
        {
            Initialize(count, 1);
        }

        public void Initialize(int count, int levelIndex)
        {
            enemyCount = count;
            int l = Mathf.Max(1, levelIndex);
            float k = GameDifficultySettings.EvaluateEnemyLevelCurveK(l);
            float hpScale = 1f + Mathf.Max(0f, hpPerLevel) * k;
            float globalMultiplier = GameDifficultySettings.GetEnemyHpMultiplierOrDefault();
            int effectiveHpPerEnemy = Mathf.Max(1, Mathf.RoundToInt(hpPerEnemy * hpScale * globalMultiplier));
            _currentHp = enemyCount * effectiveHpPerEnemy;

            // Centre de la "lane" : on garde le Z/Y de base et on fait balayer X.
            // Par défaut, les ennemis sont générés au centre (x=0), on conserve donc ce centre.
            _laneCenterX = transform.position.x;

            float baseAmplitude = sideAmplitude;
            if (sweepFullWidth && PlayerCombatController.Main != null)
            {
                var lateral = PlayerCombatController.Main.GetComponent<PlayerLateralController>();
                if (lateral != null)
                {
                    baseAmplitude = Mathf.Max(0.5f, lateral.MaxOffsetX - sweepMargin);
                    _laneCenterX = 0f;
                }
            }

            _effectiveAmplitude = baseAmplitude * (1f + amplitudePerLevel * (l - 1));
            _effectiveSpeed = sideSpeed * (1f + speedPerLevel * (l - 1));

            _basePosition = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);

            int runSeed = 12345;
            var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
            if (run != null)
                runSeed = run.Seed;

            int px = Mathf.RoundToInt(_basePosition.x * 100f);
            int pz = Mathf.RoundToInt(_basePosition.z * 100f);
            _goldRngSeed = runSeed ^ (l * 19349663) ^ (px * 83492791) ^ (pz * 73856093) ^ (enemyCount * 297121507);
        }

        void Update()
        {
            if (enableSideMovement)
            {
                float xOffset = Mathf.Sin(Time.time * _effectiveSpeed + _phase) * _effectiveAmplitude;
                var pos = _basePosition;
                pos.x = _laneCenterX + xOffset;
                transform.position = pos;
            }

            ApplySpikeAuraDamage();
        }

        void ApplySpikeAuraDamage()
        {
            var player = PlayerCombatController.Main;
            if (player == null)
                return;

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
                Debug.Log($"[SpikeAura] EnemyGroup '{name}' hit for {dmgInt} (radius={radius:F2}, dist={dist:F2}, dps={auraDps:F2})");
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
                        Debug.Log($"[CoinDrop] EnemyGroup '{name}' spawned a coin (chance={coinDropChance:P0}).");
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
            else
            {
                // Pas de shrink : on garde l'échelle constante pour éviter un rendu confus.
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
