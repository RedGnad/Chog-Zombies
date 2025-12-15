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

            int rngSeed = seed ^ (levelIndex * 73856093) ^ unchecked((int)0x9e3779b9);
            var rng = new System.Random(rngSeed);
            var level = new LevelData
            {
                LevelIndex = levelIndex,
                Boss = new BossData()
            };

            // Niveau un peu plus long : plus de segments au fur et à mesure
            int segments = 6 + (levelIndex - 1) / 4;
            segments = Mathf.Clamp(segments, 6, 10);

            for (int j = 0; j < segments; j++)
            {
                var segment = new SegmentData
                {
                    LeftGate = GenerateGate(rng, levelIndex),
                    RightGate = GenerateGate(rng, levelIndex)
                };

                int baseEnemyCount = BaseEnemyCount + (levelIndex - 1) / 3;
                int delta = rng.Next(0, 4);
                segment.EnemyCount = baseEnemyCount + delta;

                level.Segments.Add(segment);
            }

            GenerateBoss(levelIndex, rng, level.Boss);

            return level;
        }

        static GateData GenerateGate(System.Random rng, int levelIndex)
        {
            int t = rng.Next(0, 1000);

            int l = Math.Max(1, levelIndex);
            int addMax = Mathf.Clamp(4 + (l - 1) / 4, 4, 8);
            int subMax = Mathf.Clamp(4 + (l - 1) / 3, 4, 10);

            int addThreshold = Mathf.Clamp(430 - (l - 1) * 8, 250, 430);
            int subThreshold = addThreshold + Mathf.Clamp(320 + (l - 1) * 8, 320, 520);
            int mulThreshold = subThreshold + Mathf.Clamp(200 - (l - 1) * 4, 80, 200);

            if (t < addThreshold)
            {
                return new GateData
                {
                    Type = GateType.Add,
                    Value = 1 + (t % addMax)
                };
            }

            if (t < subThreshold)
            {
                return new GateData
                {
                    Type = GateType.Subtract,
                    Value = 1 + (t % subMax)
                };
            }

            if (t < mulThreshold)
            {
                int roll = rng.Next(0, 1000);

                int pct;
                if (roll < 450) pct = 110;
                else if (roll < 750) pct = 120;
                else if (roll < 900) pct = 130;
                else if (roll < 980) pct = 140;
                else pct = 150;

                return new GateData
                {
                    Type = GateType.Multiply,
                    Value = pct
                };
            }

            return new GateData
            {
                Type = GateType.Multiply,
                Value = 120
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

            float enemyHpScale = 1.0f + 0.04f * (levelIndex - 1);
            float enemyDamageScale = 1.0f + 0.03f * (levelIndex - 1);

            float l = Mathf.Max(1, levelIndex);
            float k = (l - 1f);
            float bossHpScale = 1.0f + 0.09f * k + 0.004f * k * k;

            float bossDamageScale = 1.0f + 0.045f * k;

            boss.Pattern = pattern;
            boss.Hp = Mathf.RoundToInt(BaseBossHp * bossHpScale * hpMultiplier);
            boss.Damage = Mathf.RoundToInt(BaseBossDamage * bossDamageScale * damageMultiplier);
        }
    }
}
