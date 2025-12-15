using UnityEngine;
using ChogZombies.Combat;
using ChogZombies.Player;

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

        [Header("Chase Movement (Z)")]
        [SerializeField] float followSpeedBase = 6.0f;
        [SerializeField] float followSpeedPerLevel = 0.08f;
        [SerializeField] float targetOffsetZ = 0.0f;

        [Header("Chase Movement (X)")]
        [SerializeField] bool chasePlayerX = true;
        [SerializeField] float followXSpeedBase = 7.0f;
        [SerializeField] float followXSpeedPerLevel = 0.06f;
        [SerializeField] float targetOffsetX = 0.0f;

        [Header("Side Movement (X)")]
        [SerializeField] bool enableSideMovement = true;
        [SerializeField] float sideAmplitude = 2.5f;
        [SerializeField] float sideSpeed = 0.8f;

        int _currentHp;
        Vector3 _basePosition;
        float _phase;
        float _baseX;
        int _goldRngSeed;
        int _levelIndex;
        PlayerCombatController _player;
        bool _activated;
        [SerializeField] float activationDistanceZ = 15.0f;

        public int EnemyCount => enemyCount;

        public void Initialize(int count, int levelIndex)
        {
            enemyCount = count;
            _levelIndex = Mathf.Max(1, levelIndex);

            // Important: pour pouvoir toucher le joueur, le chaser ne doit pas viser une position "devant" le joueur.
            // Les anciennes scènes/prefabs peuvent avoir gardé une valeur positive (ex: 10). On la clamp à 0.
            targetOffsetZ = Mathf.Min(targetOffsetZ, 0f);

            int l = _levelIndex;
            float hpScale = 1f + Mathf.Max(0f, hpPerLevel) * (l - 1);
            int effectiveHpPerEnemy = Mathf.Max(1, Mathf.RoundToInt(hpPerEnemy * hpScale));
            _currentHp = enemyCount * effectiveHpPerEnemy;

            _player = PlayerCombatController.Main;
            if (_player == null)
                _player = FindObjectOfType<PlayerCombatController>();

            _basePosition = transform.position;
            _baseX = _basePosition.x;
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

            float l = Mathf.Max(1, _levelIndex);
            float followSpeed = followSpeedBase * (1f + followSpeedPerLevel * (l - 1));
            float targetZ = playerZ + targetOffsetZ;
            pos.z = Mathf.MoveTowards(pos.z, targetZ, followSpeed * Time.deltaTime);

            if (chasePlayerX)
            {
                float followXSpeed = followXSpeedBase * (1f + followXSpeedPerLevel * (l - 1));
                float targetX = player.transform.position.x + targetOffsetX;
                pos.x = Mathf.MoveTowards(pos.x, targetX, followXSpeed * Time.deltaTime);
            }
            else if (enableSideMovement)
            {
                float xOffset = Mathf.Sin(Time.time * sideSpeed + _phase) * sideAmplitude;
                pos.x = _baseX + xOffset;
            }

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
