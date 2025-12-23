using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.Player;
using ChogZombies.Game;
using ChogZombies.Effects;

namespace ChogZombies.Enemies
{
    public class EnemyChaserGroupBehaviour : MonoBehaviour
    {
        [SerializeField] int enemyCount;
        [SerializeField] int hpPerEnemy = 14;

        [Header("Difficulty")]
        [SerializeField] float hpPerLevel = 0.18f;

        [Header("Rewards")]
        [SerializeField] int powerRewardPerEnemy = 1;
        [SerializeField] int maxPowerReward = 1;
        [SerializeField] float goldDropChance = 0.5f;
        [SerializeField] int goldReward = 1;

        [Header("Chaser Movement")]
        [SerializeField] float lateralAlignSpeed = 0.8f;
        [SerializeField] float maxLateralStepPerFrame = 0.10f;
        [SerializeField] float targetOffsetX = 0.0f;

        int _currentHp;
        int _goldRngSeed;
        int _levelIndex;
        PlayerCombatController _player;
        bool _activated;
        [SerializeField] float activationDistanceZ = 15.0f;
        float _fixedZ;

        public int EnemyCount => enemyCount;

        public void Initialize(int count, int levelIndex)
        {
            enemyCount = count;
            _levelIndex = Mathf.Max(1, levelIndex);

            int l = _levelIndex;
            float k = GameDifficultySettings.EvaluateEnemyLevelCurveK(l);
            float hpScale = 1f + Mathf.Max(0f, hpPerLevel) * k;
            float globalMultiplier = GameDifficultySettings.GetEnemyHpMultiplierOrDefault();
            int effectiveHpPerEnemy = Mathf.Max(1, Mathf.RoundToInt(hpPerEnemy * hpScale * globalMultiplier));
            _currentHp = enemyCount * effectiveHpPerEnemy;

            _player = PlayerCombatController.Main;
            if (_player == null)
                _player = FindObjectOfType<PlayerCombatController>();

            _fixedZ = transform.position.z;

            int runSeed = 12345;
            var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
            if (run != null)
                runSeed = run.Seed;

            var p = transform.position;
            int px = Mathf.RoundToInt(p.x * 100f);
            int pz = Mathf.RoundToInt(p.z * 100f);
            _goldRngSeed = runSeed ^ (l * 19349663) ^ (px * 83492791) ^ (pz * 73856093) ^ (enemyCount * 297121507);
        }

        void Update()
        {
            var player = _player ?? PlayerCombatController.Main;
            if (player == null)
                return;

            var pos = transform.position;
            float playerZ = player.transform.position.z;

            // Tant que le joueur est loin derrière, on reste statique pour éviter un mouvement "magique" sur tout le couloir.
            if (!_activated)
            {
                if (playerZ + activationDistanceZ < pos.z)
                    return;

                _activated = true;
            }

            pos.z = _fixedZ;

            float targetX = player.transform.position.x + targetOffsetX;
            float step = Mathf.Max(0f, lateralAlignSpeed) * Time.deltaTime;
            float maxStep = Mathf.Max(0f, maxLateralStepPerFrame);
            if (maxStep > 0f)
                step = Mathf.Min(step, maxStep);
            pos.x = Mathf.MoveTowards(pos.x, targetX, step);

            transform.position = pos;
        }

        public void TakeDamage(float damage)
        {
            int dmg = Mathf.RoundToInt(damage);
            _currentHp -= dmg;

            // Hit feedback
            var hitFeedback = GetComponent<HitFeedbackController>();
            if (hitFeedback != null)
                hitFeedback.TriggerHitFeedback();

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
