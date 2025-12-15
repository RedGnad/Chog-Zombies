using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.LevelGen;
using ChogZombies.Enemies;

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
        [SerializeField] int maxSoldiers = 200;

        [Header("Shooting")]
        [SerializeField] float fireRate = 3f; // bullets per second
        [SerializeField] float projectileSpeed = 20f;
        [SerializeField] float baseDamagePerShot = 5f;
        [SerializeField] float powerDamageMultiplier = 1.2f;
        [SerializeField] Transform firePoint;
        [SerializeField] Vector3 projectileScale = new Vector3(0.25f, 0.25f, 0.25f);
        [SerializeField] float projectileLifetime = 0.6f;
        [SerializeField] GameObject projectilePrefab;

        [Header("Shooting Tuning")]
        [SerializeField] float maxShootDistance = 12f;
        [SerializeField] float targetDetectionRadius = 1.0f;

        [Header("Visual Crowd")]
        [SerializeField] Transform visualRoot;
        [SerializeField] float minVisualScale = 0.85f;
        [SerializeField] float maxVisualScale = 2.2f;
        [SerializeField] int soldiersAtMaxScale = 70;
        [SerializeField] float visualScaleExponent = 0.75f;

        float _fireTimer;

        Vector3 _baseVisualScale;

        float _damageMultiplierFromLoot = 1f;
        float _fireRateMultiplierFromLoot = 1f;
        float _damageTakenMultiplierFromLoot = 1f;

        public int SoldierCount { get; private set; }

        public bool IsAlive => SoldierCount > 0;

        public bool IsMain => Main == this;

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

        void Update()
        {
            if (!IsMain)
                return;

            AutoShoot();
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
            Vector3 origin = validFirePoint
                ? firePoint.position
                : transform.position + Vector3.up * 1.0f;

            Vector3 dir = transform.forward;

            float clampedBase = Mathf.Min(baseDamagePerShot, 50f);
            float powerBonus = Mathf.Pow(Mathf.Max(1, SoldierCount), 0.4f) * powerDamageMultiplier;
            float damage = (clampedBase + powerBonus) * _damageMultiplierFromLoot;

            float maxLifetimeByDistance = projectileSpeed > 0.01f
                ? (maxShootDistance / projectileSpeed)
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
                int loss = Mathf.Max(1, enemyGroup.EnemyCount / 2);
                TakeSoldierDamage(loss);

                Destroy(enemyGroup.gameObject);
                return;
            }
        }
    }
}
