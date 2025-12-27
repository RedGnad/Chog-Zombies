using UnityEngine;
using ChogZombies.LevelGen;
using ChogZombies.Combat;
using ChogZombies.Player;
using ChogZombies.Game;
using ChogZombies.Effects;

namespace ChogZombies.Enemies
{
    public class BossBehaviour : MonoBehaviour
    {
        [SerializeField] int maxHp;
        [Header("Boss Fight")]
        [SerializeField] float attackInterval = 0.5f;
        [SerializeField] float damageToSoldiersFactor = 0.3f;

        [Header("Animation")]
        [SerializeField] Animator animator;
        [SerializeField] string attackTriggerName = "Attack";
        [SerializeField] float attackAnimDuration = 0.5f;
        [SerializeField] float attackAnimLeadTime = 0.5f;

        int _currentHp;
        BossData _data;
        bool _fightStarted;
        float _attackTimer;
        float _effectiveAttackInterval;
        PlayerCombatController _player;
        bool _attackAnimQueued;
        float _attackLeadTimeEffective;

        public int CurrentHp => _currentHp;
        public int MaxHp => maxHp;

        public void Initialize(BossData data)
        {
            _data = data;
            maxHp = data.Hp;
            _currentHp = maxHp;
            _effectiveAttackInterval = Mathf.Max(0.05f, attackInterval);
            _attackLeadTimeEffective = Mathf.Min(attackAnimLeadTime, _effectiveAttackInterval);
        }

        public void TakeDamage(float damage)
        {
            int dmg = Mathf.RoundToInt(damage);
            _currentHp -= dmg;

            // Hit feedback + camera shake
            var hitFeedback = GetComponent<HitFeedbackController>();
            if (hitFeedback != null)
                hitFeedback.TriggerHitFeedback();

            if (_currentHp <= 0)
            {
                Debug.Log("Boss defeated!");
                Destroy(gameObject);
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
            float interval = _effectiveAttackInterval;

            float lead = Mathf.Clamp(_attackLeadTimeEffective, 0f, interval);
            if (!_attackAnimQueued && animator != null && lead > 0f && _attackTimer >= interval - lead)
            {
                if (!string.IsNullOrEmpty(attackTriggerName))
                    animator.SetTrigger(attackTriggerName);
                _attackAnimQueued = true;
            }

            if (_attackTimer >= interval)
            {
                _attackTimer -= interval;
                _attackAnimQueued = false;

                float factor = Mathf.Max(0.01f, damageToSoldiersFactor);
                int dmg = Mathf.Max(1, Mathf.RoundToInt(_data.Damage * factor));
                _player.TakeSoldierDamage(dmg);

                CameraShakeController.TriggerShake(0.06f, 0.15f);
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
                float speedMultiplier = GameDifficultySettings.GetBossAttackIntervalMultiplierOrDefault();
                _effectiveAttackInterval = Mathf.Max(0.05f, attackInterval / speedMultiplier);

                _attackLeadTimeEffective = Mathf.Min(attackAnimLeadTime, _effectiveAttackInterval);

                if (animator != null && attackAnimDuration > 0.01f && _attackLeadTimeEffective > 0.01f)
                {
                    float targetDuration = _attackLeadTimeEffective;
                    float speedFactor = attackAnimDuration / targetDuration;
                    animator.speed = speedFactor;
                }

                _attackAnimQueued = false;

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
