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
        [SerializeField] bool allowSpikeAuraDamage = false;
        [Header("Boss Fight")]
        [SerializeField] float attackInterval = 0.5f;
        [SerializeField] float damageToSoldiersFactor = 0.3f;
        [SerializeField] float engageDistance = 12f;

        [Header("Animation")]
        [SerializeField] Animator animator;
        [SerializeField] string attackTriggerName = "Attack";
        [SerializeField] float attackAnimDuration = 0.5f;
        [SerializeField] float attackAnimLeadTime = 0.5f;
        [SerializeField] GameObject deathVfxPrefab;
        [SerializeField] AudioClip deathSfxClip;
        [SerializeField, Range(0f, 1f)] float deathSfxVolume = 1f;

        int _currentHp;
        BossData _data;
        bool _fightStarted;
        float _attackTimer;
        float _effectiveAttackInterval;
        PlayerCombatController _player;
        float _spikeAuraAccumulatedDamage;

        static BossBehaviour _activeInstance;

        public static BossBehaviour ActiveInstance => _activeInstance;

        public int CurrentHp => _currentHp;
        public int MaxHp => maxHp;
        public float EngageDistance
        {
            get => engageDistance;
            private set => engageDistance = Mathf.Max(0f, value);
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

            // S'assurer que le SFX de mort est prêt à être lu (important en WebGL
            // où les clips peuvent être chargés en arrière-plan).
            if (deathSfxClip != null && !deathSfxClip.preloadAudioData)
            {
                deathSfxClip.LoadAudioData();
            }

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

            ApplySpikeAuraDamage();
            // 2) Gérer l'intervalle entre le début de deux télégraphes
            _attackTimer += Time.deltaTime;
            float interval = _effectiveAttackInterval;
            if (_attackTimer >= interval)
            {
                _attackTimer -= interval;
                TriggerAttackAnimation();
            }
        }

        void ApplySpikeAuraDamage()
        {
            if (!allowSpikeAuraDamage)
                return;

            var player = _player != null ? _player : PlayerCombatController.Main;
            if (player == null)
                return;

            float radius;
            if (!player.TryGetActiveSpikeAuraRadius(out radius))
                return;

            float auraDps = Mathf.Max(0f, Loot.RunMetaEffects.SpikeAuraDamagePerSecond);
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
