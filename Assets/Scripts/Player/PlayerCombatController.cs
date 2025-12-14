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
        [SerializeField] Transform firePoint;
        [SerializeField] Vector3 projectileScale = new Vector3(0.25f, 0.25f, 0.25f);
        [SerializeField] float projectileLifetime = 1.0f;

        [Header("Shooting Tuning")]
        [SerializeField] float maxShootDistance = 20f;
        [SerializeField] float targetDetectionRadius = 1.0f;

        [Header("Visual Crowd")]
        [SerializeField] Transform visualRoot;
        [SerializeField] float minVisualScale = 0.5f;
        [SerializeField] float maxVisualScale = 3f;
        [SerializeField] int soldiersAtMaxScale = 50;

        float _fireTimer;

        Vector3 _baseVisualScale;

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
            if (fireRate <= 0f)
                return;

            _fireTimer += Time.deltaTime;
            float interval = 1f / fireRate;

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
            float damage = clampedBase + SoldierCount;

            Projectile.SpawnProjectile(origin, dir, projectileSpeed, damage, projectileScale, projectileLifetime);
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
            }

            SoldierCount = Mathf.Clamp(SoldierCount, minSoldiers, maxSoldiers);

            Debug.Log($"Gate {gate.Type} {gate.Value}: soldiers {before} -> {SoldierCount}");

            UpdateVisualSoldiers();
        }

        public void TakeSoldierDamage(int loss)
        {
            if (loss <= 0)
                return;

            int before = SoldierCount;
            SoldierCount = Mathf.Clamp(SoldierCount - loss, 0, maxSoldiers);
            Debug.Log($"Player damage: -{loss} soldiers ({before} -> {SoldierCount})");

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
