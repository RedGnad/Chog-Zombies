using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.Player;

namespace ChogZombies.Enemies
{
    public class EnemyGroupBehaviour : MonoBehaviour
    {
        [SerializeField] int enemyCount;
        [SerializeField] int hpPerEnemy = 10;

        [Header("Difficulty")]
        [SerializeField] float hpPerLevel = 0.10f;

        [Header("Rewards")]
        [SerializeField] int powerRewardPerEnemy = 1;
        [SerializeField] int maxPowerReward = 1;
        [SerializeField] float goldDropChance = 0.5f;
        [SerializeField] int goldReward = 1;

        [Header("Movement")]
        [SerializeField] bool enableSideMovement = true;
        [SerializeField] bool sweepFullWidth = true;
        [SerializeField] float sweepMargin = 0.25f;
        [SerializeField] float sideAmplitude = 3.5f;
        [SerializeField] float sideSpeed = 0.5f;
        [SerializeField] float amplitudePerLevel = 0.04f;
        [SerializeField] float speedPerLevel = 0.005f;

        int _currentHp;
        Vector3 _basePosition;
        float _phase;
        float _effectiveAmplitude;
        float _effectiveSpeed;
        float _laneCenterX;
        int _goldRngSeed;

        public int EnemyCount => enemyCount;

        public void Initialize(int count)
        {
            Initialize(count, 1);
        }

        public void Initialize(int count, int levelIndex)
        {
            enemyCount = count;
            int l = Mathf.Max(1, levelIndex);
            float hpScale = 1f + Mathf.Max(0f, hpPerLevel) * (l - 1);
            int effectiveHpPerEnemy = Mathf.Max(1, Mathf.RoundToInt(hpPerEnemy * hpScale));
            _currentHp = enemyCount * effectiveHpPerEnemy;

            // Centre de la "lane" : on garde le Z/Y de base et on fait balayer X.
            // Par défaut, les ennemis sont générés au centre (x=0), on conserve donc ce centre.
            _laneCenterX = transform.position.x;

            float baseAmplitude = sideAmplitude;
            if (sweepFullWidth && PlayerCombatController.Main != null)
            {
                var lateral = PlayerCombatController.Main.GetComponent<PlayerLateralController>();
                if (lateral != null)
                {
                    baseAmplitude = Mathf.Max(0.5f, lateral.MaxOffsetX - sweepMargin);
                    _laneCenterX = 0f;
                }
            }

            _effectiveAmplitude = baseAmplitude * (1f + amplitudePerLevel * (l - 1));
            _effectiveSpeed = sideSpeed * (1f + speedPerLevel * (l - 1));

            _basePosition = transform.position;
            _phase = Random.Range(0f, Mathf.PI * 2f);

            int runSeed = 12345;
            var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
            if (run != null)
                runSeed = run.Seed;

            int px = Mathf.RoundToInt(_basePosition.x * 100f);
            int pz = Mathf.RoundToInt(_basePosition.z * 100f);
            _goldRngSeed = runSeed ^ (l * 19349663) ^ (px * 83492791) ^ (pz * 73856093) ^ (enemyCount * 297121507);
        }

        void Update()
        {
            if (!enableSideMovement)
                return;

            float xOffset = Mathf.Sin(Time.time * _effectiveSpeed + _phase) * _effectiveAmplitude;
            var pos = _basePosition;
            pos.x = _laneCenterX + xOffset;
            transform.position = pos;
        }

        public void TakeDamage(float damage)
        {
            int dmg = Mathf.RoundToInt(damage);
            _currentHp -= dmg;

            if (_currentHp <= 0)
            {
                int reward = Mathf.Clamp(enemyCount * powerRewardPerEnemy, 0, maxPowerReward);
                if (reward > 0 && PlayerCombatController.Main != null)
                {
                    PlayerCombatController.Main.AddPower(reward);
                }

                if (goldReward > 0 && goldDropChance > 0f)
                {
                    var rng = new System.Random(_goldRngSeed);
                    if (rng.NextDouble() < goldDropChance)
                    {
                        var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
                        if (run != null)
                            run.AddGold(goldReward);
                    }
                }
                Destroy(gameObject);
            }
            else
            {
                // Pas de shrink : on garde l'échelle constante pour éviter un rendu confus.
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
