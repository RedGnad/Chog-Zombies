using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChogZombies.LevelGen
{
    public static class LevelGenerator
    {
        const int BaseEnemyHp = 50;
        const int BaseEnemyDamage = 5;
        const int BaseEnemyCount = 3;

        const int BaseBossHp = 300;
        const int BaseBossDamage = 15;

        public static LevelData Generate(int levelIndex, int seed)
        {
            if (levelIndex < 1)
            {
                levelIndex = 1;
            }

            var rng = new System.Random(seed);
            var level = new LevelData
            {
                LevelIndex = levelIndex,
                Boss = new BossData()
            };

            // Niveau un peu plus long : plus de segments au fur et à mesure
            int segments = 4 + (levelIndex - 1) / 3;
            segments = Mathf.Clamp(segments, 4, 9);

            for (int j = 0; j < segments; j++)
            {
                var segment = new SegmentData
                {
                    LeftGate = GenerateGate(rng),
                    RightGate = GenerateGate(rng)
                };

                int baseEnemyCount = BaseEnemyCount + (levelIndex - 1) / 2;
                int delta = rng.Next(0, 3);
                segment.EnemyCount = baseEnemyCount + delta;

                level.Segments.Add(segment);
            }

            GenerateBoss(levelIndex, rng, level.Boss);

            return level;
        }

        static GateData GenerateGate(System.Random rng)
        {
            int t = rng.Next(0, 1000);

            // Portes plus modestes: petites additions/soustractions fréquentes,
            // multiplicateurs plus rares pour éviter une explosion du nombre de soldats.
            if (t < 400)
            {
                return new GateData
                {
                    Type = GateType.Add,
                    Value = 1 + (t % 5) // +1 à +5
                };
            }

            if (t < 700)
            {
                return new GateData
                {
                    Type = GateType.Subtract,
                    Value = 1 + (t % 5) // -1 à -5
                };
            }

            if (t < 900)
            {
                return new GateData
                {
                    Type = GateType.Multiply2,
                    Value = 2
                };
            }

            return new GateData
            {
                Type = GateType.Multiply3,
                Value = 3
            };
        }

        static void GenerateBoss(int levelIndex, System.Random rng, BossData boss)
        {
            int t = rng.Next(0, 1000);
            BossPatternType pattern;
            float hpMultiplier;
            float damageMultiplier;

            if (t < 500)
            {
                pattern = BossPatternType.A;
                hpMultiplier = 0.9f;
                damageMultiplier = 0.9f;
            }
            else if (t < 800)
            {
                pattern = BossPatternType.B;
                hpMultiplier = 1.0f;
                damageMultiplier = 1.0f;
            }
            else if (t < 950)
            {
                pattern = BossPatternType.C;
                hpMultiplier = 1.2f;
                damageMultiplier = 1.1f;
            }
            else
            {
                pattern = BossPatternType.D;
                hpMultiplier = 1.4f;
                damageMultiplier = 1.2f;
            }

            float enemyHpScale = 1.0f + 0.06f * (levelIndex - 1);
            float enemyDamageScale = 1.0f + 0.04f * (levelIndex - 1);

            float bossHpScale = 1.0f + 0.08f * (levelIndex - 1);
            float bossDamageScale = 1.0f + 0.05f * (levelIndex - 1);

            boss.Pattern = pattern;
            boss.Hp = Mathf.RoundToInt(BaseBossHp * bossHpScale * hpMultiplier);
            boss.Damage = Mathf.RoundToInt(BaseBossDamage * bossDamageScale * damageMultiplier);
        }
    }
}
