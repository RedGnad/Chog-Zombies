using UnityEngine;
using ChogZombies.LevelGen;
using ChogZombies.Combat;
using ChogZombies.Player;

namespace ChogZombies.Enemies
{
    public class BossBehaviour : MonoBehaviour
    {
        [SerializeField] int maxHp;
        [Header("Boss Fight")]
        [SerializeField] float attackInterval = 0.75f;

        int _currentHp;
        BossData _data;
        bool _fightStarted;
        float _attackTimer;
        PlayerCombatController _player;

        public int CurrentHp => _currentHp;
        public int MaxHp => maxHp;

        public void Initialize(BossData data)
        {
            _data = data;
            maxHp = data.Hp;
            _currentHp = maxHp;
        }

        public void TakeDamage(float damage)
        {
            int dmg = Mathf.RoundToInt(damage);
            _currentHp -= dmg;

            if (_currentHp <= 0)
            {
                Debug.Log("Boss defeated!");
                Destroy(gameObject);
            }
            else
            {
                // Feedback simple: on change légèrement la couleur
                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    float ratio = Mathf.Clamp01((float)_currentHp / maxHp);
                    renderer.material.color = Color.Lerp(Color.black, renderer.material.color, ratio);
                }
            }
        }

        void Update()
        {
            if (!_fightStarted)
                return;

            if (_player == null)
                return;

            if (!_player.IsAlive)
            {
                // Défaite: on ne détruit pas le boss ici, on stop juste la boucle.
                _fightStarted = false;
                Debug.Log("Run failed: player defeated by boss.");
                return;
            }

            _attackTimer += Time.deltaTime;
            if (_attackTimer >= attackInterval)
            {
                _attackTimer -= attackInterval;
                int dmg = Mathf.Max(1, _data.Damage / 4);
                _player.TakeSoldierDamage(dmg);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<Projectile>(out var projectile))
            {
                TakeDamage(projectile.Damage);
                Destroy(projectile.gameObject);
                return;
            }

            if (_fightStarted)
                return;

            var player = other.GetComponentInParent<PlayerCombatController>();
            if (player != null)
            {
                _fightStarted = true;
                _player = player;
                _attackTimer = 0f;

                var runner = player.GetComponent<ChogZombies.Player.AutoRunner>();
                if (runner != null)
                {
                    runner.Stop();
                }

                Debug.Log("Boss fight started: scrolling stopped.");
            }
        }
    }
}
