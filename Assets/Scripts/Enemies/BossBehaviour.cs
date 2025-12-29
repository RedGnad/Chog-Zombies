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
        [SerializeField] float engageDistance = 12f;

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

        static BossBehaviour _activeInstance;

        public static BossBehaviour ActiveInstance => _activeInstance;

        public int CurrentHp => _currentHp;
        public int MaxHp => maxHp;
        public float EngageDistance
        {
            get => engageDistance;
            private set => engageDistance = Mathf.Max(0f, value);
        }

        void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
        }

        void OnDestroy()
        {
            if (_activeInstance == this)
                _activeInstance = null;
        }

        public void Initialize(BossData data)
        {
            _data = data;
            maxHp = data.Hp;
            _currentHp = maxHp;
            _effectiveAttackInterval = Mathf.Max(0.05f, attackInterval);
            // Le télégraphe doit durer la longueur complète du clip d'attaque,
            // bornée par l'intervalle effectif.
            _activeInstance = this;

            if (_data != null)
            {
                float factor = Mathf.Max(0.01f, damageToSoldiersFactor);
                int soldiersPerHit = Mathf.Max(1, Mathf.RoundToInt(_data.Damage * factor));
                Debug.Log($"[BossInit] level={Game.RunGameController.CurrentLevelIndex} bossHp={maxHp} bossDamage={_data.Damage} soldiersPerHit={soldiersPerHit}");
            }
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
            {
                TryAutoEngage();
                if (!_fightStarted)
                    return;
            }

            if (_player == null)
                return;

            if (!_player.IsAlive)
            {
                // Défaite: on ne détruit pas le boss ici, on stop juste la boucle.
                _fightStarted = false;
                Debug.Log("Run failed: player defeated by boss.");
                return;
            }
            // 2) Gérer l'intervalle entre le début de deux télégraphes
            _attackTimer += Time.deltaTime;
            float interval = _effectiveAttackInterval;
            if (_attackTimer >= interval)
            {
                _attackTimer -= interval;
                TriggerAttackAnimation();
            }
        }

        void TriggerAttackAnimation()
        {
            Debug.Log($"[BossAttack] TriggerAttackAnimation at t={Time.time:F3}, interval={_effectiveAttackInterval:F3}");
            if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
            {
                animator.SetTrigger(attackTriggerName);
            }
        }

        public void OnAttackAnimationImpact()
        {
            Debug.Log($"[BossAttack] OnAttackAnimationImpact (Animation Event) at t={Time.time:F3}");

            var boss = _activeInstance ?? this;
            if (boss == null)
                return;

            if (!boss._fightStarted)
                return;

            if (boss._player == null || !boss._player.IsAlive)
                return;

            if (boss._data == null)
                return;

            float factor = Mathf.Max(0.01f, boss.damageToSoldiersFactor);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(boss._data.Damage * factor));
            boss._player.TakeSoldierDamage(dmg);

            CameraShakeController.TriggerShake(0.06f, 0.15f);
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
            if (player == null)
                return;

            if (!_fightStarted)
            {
                TryStartFight(player, true);
            }
            else
            {
                StopRunner(player);
            }
        }

        void TryAutoEngage()
        {
            if (engageDistance <= 0f)
                return;

            var player = _player != null ? _player : PlayerCombatController.Main;
            if (player == null)
                return;

            float distanceSqr = (player.transform.position - transform.position).sqrMagnitude;
            if (distanceSqr <= engageDistance * engageDistance)
            {
                TryStartFight(player, false);
            }
        }

        void TryStartFight(PlayerCombatController player, bool stopRunner)
        {
            if (player == null || _fightStarted)
                return;

            _fightStarted = true;
            _player = player;
            _attackTimer = 0f;
            float speedMultiplier = GameDifficultySettings.GetBossAttackIntervalMultiplierOrDefault();
            _effectiveAttackInterval = Mathf.Max(0.05f, attackInterval / speedMultiplier);
            if (animator != null && attackAnimDuration > 0.01f)
                TriggerAttackAnimation();

            if (stopRunner)
                StopRunner(player);

            Debug.Log("Boss fight started.");
        }

        void StopRunner(PlayerCombatController player)
        {
            if (player == null)
                return;

            var runner = player.GetComponent<ChogZombies.Player.AutoRunner>();
            if (runner != null)
                runner.Stop();
        }

        public void SetEngageDistance(float distance)
        {
            EngageDistance = distance;
        }
    }
}
