using UnityEngine;
using ChogZombies.Enemies;
using ChogZombies.CameraSystem;

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

        static void ApplyTintToHierarchy(GameObject go, Color color)
        {
            if (go == null)
                return;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            var block = new MaterialPropertyBlock();

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;

                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                r.SetPropertyBlock(block);
            }
        }

        static void StripSceneControlComponents(GameObject go)
        {
            if (go == null)
                return;

            var follows = go.GetComponentsInChildren<CameraFollow>(true);
            for (int i = 0; i < follows.Length; i++)
                Object.Destroy(follows[i]);

            var cameras = go.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
                Object.Destroy(cameras[i]);

            var listeners = go.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
                Object.Destroy(listeners[i]);

            var lights = go.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
                Object.Destroy(lights[i]);
        }

        public static void SpawnProjectile(
            Vector3 origin,
            Vector3 direction,
            float speed,
            float damage,
            Vector3 scale,
            float lifeTime = 3f,
            GameObject prefab = null)
        {
            GameObject go;
            if (prefab != null)
            {
                go = Object.Instantiate(prefab);
                go.name = "Projectile";
                go.transform.position = origin;
                StripSceneControlComponents(go);

                go.transform.localScale = Vector3.one;
                go.transform.localScale = scale;

                // Conserver le material/shader URP du prefab mais teinter l'instance.
                ApplyTintToHierarchy(go, Color.yellow);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Projectile";
                go.transform.position = origin;
                go.transform.localScale = Vector3.one;
                go.transform.localScale = scale;

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (shader == null)
                        shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    if (shader == null)
                        shader = Shader.Find("Standard");

                    if (shader != null)
                    {
                        var mat = new Material(shader);
                        if (mat.HasProperty("_BaseColor"))
                            mat.SetColor("_BaseColor", Color.yellow);
                        if (mat.HasProperty("_Color"))
                            mat.SetColor("_Color", Color.yellow);
                        renderer.material = mat;
                    }
                }
            }

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
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
