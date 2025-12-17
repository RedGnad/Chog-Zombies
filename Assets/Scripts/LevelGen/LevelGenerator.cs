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

        const int BaseBossHp = 240;
        const int BaseBossDamage = 15;

        const int AssumedStartingSoldiers = 1;
        const int AssumedMaxSoldiers = 500;
        const float AssumedPlayerFireRate = 3f;
        const float AssumedPlayerBaseDamagePerShot = 5f;
        const float AssumedPlayerPowerDamageMultiplier = 1.2f;

        const float BossAttackIntervalDefault = 0.6f;
        const float BossDamageToSoldiersFactorDefault = 0.35f;

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

                EnforceNoDoubleSubtractBeforeLevel8(rng, levelIndex, segment);

                int baseEnemyCount = BaseEnemyCount + (levelIndex - 1) / 3;
                int delta = rng.Next(0, 4);
                segment.EnemyCount = baseEnemyCount + delta;

                level.Segments.Add(segment);
            }

            // Garde-fou de viabilité : on s'assure qu'il existe au moins un chemin de portes
            // qui donne suffisamment de soldats pour pouvoir battre le boss (modèle simplifié DPS vs dégâts).
            int requiredForBoss = EstimateMinSoldiersToDefeatBossWorstCase(levelIndex);
            int bestFinal = SimulateBestPathSoldiers(level.Segments, AssumedStartingSoldiers);

            int safetyIterations = 0;
            const int maxIterations = 24;

            while (bestFinal < requiredForBoss && safetyIterations < maxIterations && level.Segments.Count > 0)
            {
                int segIndex = rng.Next(0, level.Segments.Count);
                bool rerollLeft = rng.Next(0, 2) == 0;

                if (rerollLeft)
                {
                    level.Segments[segIndex].LeftGate = GenerateGate(rng, levelIndex);
                }
                else
                {
                    level.Segments[segIndex].RightGate = GenerateGate(rng, levelIndex);
                }

                EnforceNoDoubleSubtractBeforeLevel8(rng, levelIndex, level.Segments[segIndex]);

                bestFinal = SimulateBestPathSoldiers(level.Segments, AssumedStartingSoldiers);
                safetyIterations++;
            }

            GenerateBoss(levelIndex, rng, level.Boss);

            return level;
        }

        static void EnforceNoDoubleSubtractBeforeLevel8(System.Random rng, int levelIndex, SegmentData segment)
        {
            if (segment == null)
                return;

            if (levelIndex >= 8)
                return;

            if (segment.LeftGate == null || segment.RightGate == null)
                return;

            if (segment.LeftGate.Type != GateType.Subtract || segment.RightGate.Type != GateType.Subtract)
                return;

            // Avant le niveau 8, on évite les segments "double rouge" pour une montée en difficulté plus lisible.
            bool rerollLeft = rng.Next(0, 2) == 0;
            for (int i = 0; i < 6; i++)
            {
                if (segment.LeftGate.Type != GateType.Subtract || segment.RightGate.Type != GateType.Subtract)
                    return;

                if (rerollLeft)
                    segment.LeftGate = GenerateGate(rng, levelIndex);
                else
                    segment.RightGate = GenerateGate(rng, levelIndex);

                rerollLeft = !rerollLeft;
            }
        }

        static int EstimateMinSoldiersToDefeatBossWorstCase(int levelIndex)
        {
            float l = Mathf.Max(1, levelIndex);
            float k = (l - 1f);

            float earlyRamp = Mathf.Clamp01(k / 8f);
            float bossEarlyEaseHp = Mathf.Lerp(0.75f, 1.0f, earlyRamp);
            float bossEarlyEaseDamage = Mathf.Lerp(0.85f, 1.0f, earlyRamp);

            // Doit rester cohérent avec GenerateBoss (même tendance), mais on prend le pire multiplicateur.
            float bossHpScale = 1.0f + 0.08f * k + 0.0035f * k * k;
            float bossDamageScale = 1.0f + 0.045f * k;

            float bossHpWorst = BaseBossHp * bossHpScale * bossEarlyEaseHp * 1.12f;
            float bossDamageWorst = BaseBossDamage * bossDamageScale * bossEarlyEaseDamage * 1.10f;

            int bossDamageInt = Mathf.RoundToInt(bossDamageWorst);
            int soldierLossPerHit = Mathf.Max(1, Mathf.RoundToInt(bossDamageInt * BossDamageToSoldiersFactorDefault));
            float soldierLossPerSecond = soldierLossPerHit / Mathf.Max(0.05f, BossAttackIntervalDefault);

            for (int soldiers = 1; soldiers <= AssumedMaxSoldiers; soldiers++)
            {
                float powerBonus = Mathf.Pow(Mathf.Max(1, soldiers), 0.4f) * AssumedPlayerPowerDamageMultiplier;
                float shotDamage = Mathf.Min(AssumedPlayerBaseDamagePerShot, 50f) + powerBonus;
                float dps = AssumedPlayerFireRate * Mathf.Max(0.1f, shotDamage);

                float timeToKill = bossHpWorst / dps;
                float timeToDie = soldiers / Mathf.Max(0.1f, soldierLossPerSecond);

                // marge pour éviter les cas "pile poil" impossibles en vrai.
                if (timeToKill <= timeToDie * 0.9f)
                    return soldiers;
            }

            return AssumedMaxSoldiers;
        }

        static int SimulateApplyGate(int soldiers, GateData gate)
        {
            if (gate == null)
                return soldiers;

            int result = soldiers;

            switch (gate.Type)
            {
                case GateType.Add:
                    result += gate.Value;
                    break;
                case GateType.Subtract:
                    result -= gate.Value;
                    break;
                case GateType.Multiply2:
                    result *= 2;
                    break;
                case GateType.Multiply3:
                    result *= 3;
                    break;
                case GateType.Multiply:
                    {
                        float multiplier = Mathf.Max(0f, gate.Value / 100f);
                        result = Mathf.RoundToInt(result * multiplier);
                        break;
                    }
            }

            return Mathf.Max(1, result);
        }

        static int SimulateBestPathSoldiers(System.Collections.Generic.List<SegmentData> segments, int initialSoldiers)
        {
            if (segments == null || segments.Count == 0)
                return initialSoldiers;

            int segmentCount = Mathf.Min(segments.Count, 16);
            int pathCount = 1 << segmentCount;

            int bestFinal = initialSoldiers;

            for (int mask = 0; mask < pathCount; mask++)
            {
                int soldiers = initialSoldiers;

                for (int i = 0; i < segmentCount; i++)
                {
                    var segment = segments[i];
                    bool useRight = (mask & (1 << i)) != 0;
                    var gate = useRight ? segment.RightGate : segment.LeftGate;
                    soldiers = SimulateApplyGate(soldiers, gate);
                }

                if (soldiers > bestFinal)
                    bestFinal = soldiers;
            }

            return bestFinal;
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
                hpMultiplier = 0.95f;
                damageMultiplier = 0.95f;
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
                hpMultiplier = 1.05f;
                damageMultiplier = 1.05f;
            }
            else
            {
                pattern = BossPatternType.D;
                hpMultiplier = 1.12f;
                damageMultiplier = 1.10f;
            }

            float enemyHpScale = 1.0f + 0.04f * (levelIndex - 1);
            float enemyDamageScale = 1.0f + 0.03f * (levelIndex - 1);

            float l = Mathf.Max(1, levelIndex);
            float k = (l - 1f);

            float earlyRamp = Mathf.Clamp01(k / 8f);
            float bossEarlyEaseHp = Mathf.Lerp(0.75f, 1.0f, earlyRamp);
            float bossEarlyEaseDamage = Mathf.Lerp(0.85f, 1.0f, earlyRamp);
            float bossHpScale = 1.0f + 0.08f * k + 0.0035f * k * k;

            float bossDamageScale = 1.0f + 0.045f * k;

            boss.Pattern = pattern;
            boss.Hp = Mathf.RoundToInt(BaseBossHp * bossHpScale * bossEarlyEaseHp * hpMultiplier);
            boss.Damage = Mathf.RoundToInt(BaseBossDamage * bossDamageScale * bossEarlyEaseDamage * damageMultiplier);
        }
    }
}
