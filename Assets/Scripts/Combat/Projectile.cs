using UnityEngine;
using ChogZombies.Enemies;

namespace ChogZombies.Combat
{
    public class Projectile : MonoBehaviour
    {
        float _speed;
        float _damage;
        float _lifeTime;
        Vector3 _direction;
        Rigidbody _rb;

        public float Damage => _damage;

        public static void SpawnProjectile(Vector3 origin, Vector3 direction, float speed, float damage, Vector3 scale, float lifeTime = 3f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Projectile";
            go.transform.position = origin;
            go.transform.localScale = scale;

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var projectile = go.AddComponent<Projectile>();
            projectile.Initialize(direction, speed, damage, lifeTime);

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            // Appliquer la vitesse après que le Rigidbody existe.
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = direction.normalized * speed;
#else
            rb.velocity = direction.normalized * speed;
#endif
        }

        public void Initialize(Vector3 direction, float speed, float damage, float lifeTime)
        {
            _direction = direction.normalized;
            _speed = speed;
            _damage = damage;
            _lifeTime = lifeTime;

            _rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            _lifeTime -= Time.deltaTime;
            if (_lifeTime <= 0f)
            {
                Destroy(gameObject);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<EnemyGroupBehaviour>(out var enemy))
            {
                enemy.TakeDamage(_damage);
                Destroy(gameObject);
                return;
            }

            if (other.TryGetComponent<BossBehaviour>(out var boss))
            {
                boss.TakeDamage(_damage);
                Destroy(gameObject);
                return;
            }
        }
    }
}
