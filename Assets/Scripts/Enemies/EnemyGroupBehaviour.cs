using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.Player;

namespace ChogZombies.Enemies
{
    public class EnemyGroupBehaviour : MonoBehaviour
    {
        [SerializeField] int enemyCount;
        [SerializeField] int hpPerEnemy = 10;

        [Header("Movement")]
        [SerializeField] bool enableSideMovement = true;
        [SerializeField] float sideAmplitude = 2f;
        [SerializeField] float sideSpeed = 1.5f;

        int _currentHp;
        Vector3 _basePosition;
        float _phase;

        public int EnemyCount => enemyCount;

        public void Initialize(int count)
        {
            enemyCount = count;
            _currentHp = enemyCount * hpPerEnemy;

            _basePosition = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);
        }

        void Update()
        {
            if (!enableSideMovement)
                return;

            float xOffset = Mathf.Sin(Time.time * sideSpeed + _phase) * sideAmplitude;
            var pos = _basePosition;
            pos.x += xOffset;
            transform.position = pos;
        }

        public void TakeDamage(float damage)
        {
            int dmg = Mathf.RoundToInt(damage);
            _currentHp -= dmg;

            if (_currentHp <= 0)
            {
                Destroy(gameObject);
            }
            else
            {
                // Feedback simple: on réduit légèrement la taille du groupe
                transform.localScale = new Vector3(
                    transform.localScale.x * 0.95f,
                    transform.localScale.y,
                    transform.localScale.z
                );
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
