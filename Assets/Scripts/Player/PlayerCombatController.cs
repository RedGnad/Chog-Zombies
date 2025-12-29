using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.LevelGen;
using ChogZombies.Enemies;
using ChogZombies.Game;
using ChogZombies.Loot;

namespace ChogZombies.Player
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCombatController : MonoBehaviour
    {
        public static PlayerCombatController Main { get; private set; }

        [Header("Soldiers")]
        [SerializeField] int startingSoldiers = 1;
        [SerializeField] int minSoldiers = 1;
        [SerializeField] int maxSoldiers = 500;

        [Header("Shooting")]
        [SerializeField] float fireRate = 3f; // bullets per second
        [SerializeField] float projectileSpeed = 20f;
        [SerializeField] float baseDamagePerShot = 5f;
        [SerializeField] float powerDamageMultiplier = 1.2f;
        [SerializeField] Transform firePoint;
        [SerializeField] Vector3 projectileScale = new Vector3(0.25f, 0.25f, 0.25f);
        [SerializeField] Vector3 projectileOffset = Vector3.zero;
        [SerializeField] float projectileLifetime = 0.6f;
        [SerializeField] GameObject projectilePrefab;

        [Header("Shooting Tuning")]
        [SerializeField] float maxShootDistance = 12f;
        [SerializeField] float targetDetectionRadius = 1.0f;

        [Header("Difficulty")]
        [SerializeField] float enemyCollisionLossPerEnemy = 0.6f;
        [SerializeField] float enemyCollisionLossPerLevel = 0.04f;

        [Header("Visual Crowd")]
        [SerializeField] Transform visualRoot;
        [SerializeField] float minVisualScale = 0.85f;
        [SerializeField] float maxVisualScale = 2.2f;
        [SerializeField] int soldiersAtMaxScale = 70;
        [SerializeField] float visualScaleExponent = 0.75f;

        [Header("Spike Aura")]
        [SerializeField] bool enableSpikeAura = true;
        [SerializeField, Min(0f)] float spikeAuraBaseRadius = 0.8f;
        [SerializeField, Min(0.05f)] float spikeAuraTickInterval = 0.25f;
        [SerializeField, Min(1)] int spikeAuraMaxTargets = 24;
        [SerializeField] LayerMask spikeAuraLayerMask = ~0;
        [SerializeField, Min(0f)] float spikeAuraMaxDamagePerTick = 40f;
        [SerializeField] bool debugSpikeAuraHits = false;
        [SerializeField] ParticleSystem spikeAuraParticle;
        [SerializeField] TrailRenderer spikeAuraTrail;
        [SerializeField] SpriteRenderer spikeAuraIndicator;
        [Header("Aegis")]
        [SerializeField] bool debugAegisConsumption = false;

        float _fireTimer;

        Vector3 _baseVisualScale;

        float _damageMultiplierFromLoot = 1f;
        float _fireRateMultiplierFromLoot = 1f;
        float _damageTakenMultiplierFromLoot = 1f;
        float _spikeAuraTimer;
        Collider[] _spikeAuraResults;
        int[] _spikeAuraProcessedIds;
        float _currentSpikeAuraRadius;

        public int SoldierCount { get; private set; }

        public bool IsAlive => SoldierCount > 0;

        public bool IsMain => Main == this;

        public void SetSoldierCount(int soldiers)
        {
            int before = SoldierCount;
            SoldierCount = Mathf.Clamp(soldiers, minSoldiers, maxSoldiers);
            Debug.Log($"Player soldiers override: {before} -> {SoldierCount}");
            UpdateVisualSoldiers();
        }

        void Awake()
        {
            if (Main == null)
            {
                Main = this;
            }

            SoldierCount = Mathf.Clamp(startingSoldiers, minSoldiers, maxSoldiers);

            var rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            _baseVisualScale = visualRoot.localScale;
            UpdateVisualSoldiers();

            int maxTargets = Mathf.Max(1, spikeAuraMaxTargets);
            _spikeAuraResults = new Collider[maxTargets];
            _spikeAuraProcessedIds = new int[maxTargets];

            // Seul le joueur principal conserve l'indicateur centralisé.
            if (!IsMain && spikeAuraIndicator != null)
                spikeAuraIndicator.gameObject.SetActive(false);
        }

        public void ApplyDamageMultiplier(float factor)
        {
            if (factor <= 0f)
                return;

            _damageMultiplierFromLoot *= factor;
        }

        public void ApplyFireRateMultiplier(float factor)
        {
            if (factor <= 0f)
                return;

            _fireRateMultiplierFromLoot *= factor;
        }

        public void ApplyDamageTakenMultiplier(float factor)
        {
            if (factor <= 0f)
                return;

            _damageTakenMultiplierFromLoot *= factor;
        }

        void OnDestroy()
        {
            if (Main == this)
            {
                Main = null;
            }
        }

        void HandleSpikeAuraDamage()
        {
            bool auraEnabled = enableSpikeAura;
            if (auraEnabled)
            {
                var loot = PlayerLootController.Instance;
                auraEnabled = loot != null && loot.HasActiveSpikeAura;
            }

            if (!auraEnabled)
            {
                UpdateSpikeAuraVisual(0f, false);
                return;
            }

            float auraDps = Mathf.Max(0f, RunMetaEffects.SpikeAuraDamagePerSecond);
            if (auraDps <= 0f)
            {
                UpdateSpikeAuraVisual(0f, false);
                return;
            }

            float interval = Mathf.Max(0.05f, spikeAuraTickInterval);
            _spikeAuraTimer += Time.deltaTime;
            if (_spikeAuraTimer < interval)
                return;

            _spikeAuraTimer -= interval;

            float radius = Mathf.Max(0.1f, spikeAuraBaseRadius + RunMetaEffects.SpikeAuraRadiusBonus);
            UpdateSpikeAuraVisual(radius, true);

            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                _spikeAuraResults,
                spikeAuraLayerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
                return;

            float damagePerTarget = Mathf.Clamp(auraDps * interval, 0f, spikeAuraMaxDamagePerTick);
            int processed = 0;

            for (int i = 0; i < hitCount; i++)
            {
                var col = _spikeAuraResults[i];
                if (col == null)
                    continue;

                if (TryApplySpikeAuraDamage(col, damagePerTarget, ref processed))
                    continue;

                var parent = col.GetComponentInParent<Component>();
                if (parent != null)
                    TryApplySpikeAuraDamage(parent, damagePerTarget, ref processed);

                if (processed >= _spikeAuraProcessedIds.Length)
                    break;
            }
        }

        bool TryApplySpikeAuraDamage(Component component, float damage, ref int processedCount)
        {
            if (component == null)
                return false;

            int targetId = component.GetInstanceID();
            for (int j = 0; j < processedCount; j++)
            {
                if (_spikeAuraProcessedIds[j] == targetId)
                    return false;
            }

            bool damaged = false;
            if (component.TryGetComponent<Enemies.EnemyGroupBehaviour>(out var group))
            {
                group.TakeDamage(damage);
                damaged = true;
            }
            else if (component.TryGetComponent<Enemies.EnemyChaserGroupBehaviour>(out var chaser))
            {
                chaser.TakeDamage(damage);
                damaged = true;
            }

            if (damaged)
            {
                if (processedCount < _spikeAuraProcessedIds.Length)
                    _spikeAuraProcessedIds[processedCount++] = targetId;

                if (debugSpikeAuraHits)
                    Debug.Log($"[SpikeAura] Hit {component.name} for {damage:F1} (radius={spikeAuraBaseRadius + RunMetaEffects.SpikeAuraRadiusBonus:F2})");
            }

            return damaged;
        }

        void UpdateSpikeAuraVisual(float radius, bool active)
        {
            _currentSpikeAuraRadius = active ? radius : 0f;

            if (spikeAuraParticle != null)
            {
                var shape = spikeAuraParticle.shape;
                shape.radius = Mathf.Max(0f, _currentSpikeAuraRadius);

                if (active)
                {
                    if (!spikeAuraParticle.isPlaying)
                        spikeAuraParticle.Play();
                }
                else if (spikeAuraParticle.isPlaying)
                {
                    spikeAuraParticle.Stop();
                }
            }

            if (spikeAuraTrail != null)
            {
                spikeAuraTrail.gameObject.SetActive(active);
                if (active)
                {
                    float width = Mathf.Max(0.05f, _currentSpikeAuraRadius * 2f);
                    spikeAuraTrail.startWidth = width;
                    spikeAuraTrail.endWidth = width;
                }
            }

            if (spikeAuraIndicator != null)
            {
                spikeAuraIndicator.gameObject.SetActive(active);
                if (active)
                {
                    float diameter = Mathf.Max(0.01f, _currentSpikeAuraRadius * 2f);
                    spikeAuraIndicator.transform.localScale = new Vector3(diameter, diameter, 1f);
                    var color = spikeAuraIndicator.color;
                    color.a = Mathf.Clamp01(_currentSpikeAuraRadius > 0f ? color.a : 0f);
                    spikeAuraIndicator.color = color;
                }
            }
        }

        void Update()
        {
            if (!IsMain)
                return;

            AutoShoot();
            HandleSpikeAuraDamage();
        }

        void AutoShoot()
        {
            float effectiveFireRate = fireRate * _fireRateMultiplierFromLoot;
            if (effectiveFireRate <= 0f)
                return;

            _fireTimer += Time.deltaTime;
            float interval = 1f / effectiveFireRate;

            if (_fireTimer >= interval)
            {
                _fireTimer -= interval;
                FireOneShot();
            }
        }

        void FireOneShot()
        {
            bool validFirePoint = firePoint != null && firePoint.IsChildOf(transform);
            Vector3 origin;
            if (validFirePoint)
            {
                origin = firePoint.position + firePoint.TransformVector(projectileOffset);
            }
            else
            {
                origin = transform.position + Vector3.up * 1.0f + transform.TransformVector(projectileOffset);
            }

            Vector3 dir = transform.forward;

            float clampedBase = Mathf.Min(baseDamagePerShot, 50f);
            float powerExp = GameDifficultySettings.GetPlayerPowerDamageExponentOrDefault();
            float powerFactor = GameDifficultySettings.GetPlayerPowerDamageMultiplierFactorOrDefault();
            float powerBonus = Mathf.Pow(Mathf.Max(1, SoldierCount), powerExp) * powerDamageMultiplier * powerFactor;
            float damage = (clampedBase + powerBonus) * _damageMultiplierFromLoot;

            float rangeMultiplier = 1f + Mathf.Max(0f, RunMetaEffects.ProjectileRangeBonus);
            float effectiveMaxDistance = maxShootDistance * rangeMultiplier;
            float maxLifetimeByDistance = projectileSpeed > 0.01f
                ? (effectiveMaxDistance / projectileSpeed)
                : projectileLifetime;
            float life = Mathf.Max(0.05f, Mathf.Min(projectileLifetime, maxLifetimeByDistance));

            Projectile.SpawnProjectile(origin, dir, projectileSpeed, damage, projectileScale, life, projectilePrefab);
        }


        public void ApplyGate(GateData gate)
        {
            int before = SoldierCount;

            switch (gate.Type)
            {
                case GateType.Add:
                    SoldierCount += gate.Value;
                    break;
                case GateType.Subtract:
                    SoldierCount -= gate.Value;
                    break;
                case GateType.Multiply2:
                    SoldierCount *= 2;
                    break;
                case GateType.Multiply3:
                    SoldierCount *= 3;
                    break;
                case GateType.Multiply:
                    float multiplier = Mathf.Max(0f, gate.Value / 100f);
                    SoldierCount = Mathf.RoundToInt(SoldierCount * multiplier);
                    break;
            }

            SoldierCount = Mathf.Clamp(SoldierCount, minSoldiers, maxSoldiers);

            Debug.Log($"Gate {gate.Type} {gate.Value}: soldiers {before} -> {SoldierCount}");

            UpdateVisualSoldiers();
        }

        public void AddPower(int amount)
        {
            if (amount <= 0)
                return;

            int before = SoldierCount;
            SoldierCount = Mathf.Clamp(SoldierCount + amount, minSoldiers, maxSoldiers);
            Debug.Log($"Enemy reward: +{amount} power ({before} -> {SoldierCount})");

            UpdateVisualSoldiers();
        }

        public void TakeSoldierDamage(int loss)
        {
            if (loss <= 0)
                return;

            if (RunMetaEffects.AegisCharges > 0)
            {
                RunMetaEffects.AddAegisCharges(-1);
                if (debugAegisConsumption)
                    Debug.Log($"[Aegis] Charge consumed. Remaining={RunMetaEffects.AegisCharges}");
                return;
            }

            int before = SoldierCount;

            float scaledLoss = loss * _damageTakenMultiplierFromLoot;
            int finalLoss = Mathf.Max(1, Mathf.RoundToInt(scaledLoss));

            SoldierCount = Mathf.Clamp(SoldierCount - finalLoss, 0, maxSoldiers);
            Debug.Log($"Player damage: -{finalLoss} soldiers ({before} -> {SoldierCount})");

            if (SoldierCount <= 0)
            {
                Debug.Log("Run failed: no soldiers left.");
            }

            UpdateVisualSoldiers();
        }

        void UpdateVisualSoldiers()
        {
            if (visualRoot == null)
                return;

            // Mise à l'échelle en fonction de la "puissance" d'escouade (SoldierCount)
            // SoldierCount = 1 -> proche de minVisualScale
            // SoldierCount >= soldiersAtMaxScale -> maxVisualScale

            int power = Mathf.Max(SoldierCount, 1);

            float t = 0f;
            if (soldiersAtMaxScale > 1)
            {
                t = Mathf.Clamp01((power - 1) / (float)(soldiersAtMaxScale - 1));
            }

            float exp = Mathf.Max(0.05f, visualScaleExponent);
            t = Mathf.Pow(t, exp);

            float scaleFactor = Mathf.Lerp(minVisualScale, maxVisualScale, t);
            visualRoot.localScale = _baseVisualScale * scaleFactor;
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<Enemies.EnemyGroupBehaviour>(out var enemyGroup))
            {
                int lvl = Mathf.Max(1, RunGameController.CurrentLevelIndex);
                float perEnemy = Mathf.Max(0f, enemyCollisionLossPerEnemy);
                float perLevel = Mathf.Max(0f, enemyCollisionLossPerLevel);
                float factor = perEnemy * (1f + perLevel * (lvl - 1));
                int loss = Mathf.Max(1, Mathf.RoundToInt(enemyGroup.EnemyCount * factor));
                TakeSoldierDamage(loss);

                Destroy(enemyGroup.gameObject);
                return;
            }

            if (other.TryGetComponent<Enemies.EnemyChaserGroupBehaviour>(out var chaserGroup))
            {
                int lvl = Mathf.Max(1, RunGameController.CurrentLevelIndex);
                float perEnemy = Mathf.Max(0f, enemyCollisionLossPerEnemy);
                float perLevel = Mathf.Max(0f, enemyCollisionLossPerLevel);
                float factor = perEnemy * (1f + perLevel * (lvl - 1));
                int loss = Mathf.Max(1, Mathf.RoundToInt(chaserGroup.EnemyCount * factor));
                TakeSoldierDamage(loss);

                Destroy(chaserGroup.gameObject);
                return;
            }
        }
    }
}
