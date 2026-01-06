using System;

namespace ChogZombies.VRF
{
    // Example, standalone model of how shop randomness is derived from the run seed.
    // This file is not used by the Unity runtime; it is provided for external verification.
    public static class VrfShopVerificationExample
    {
        public static int DeriveLevelSeed(int runBaseSeed, int levelIndex)
        {
            unchecked
            {
                int x = runBaseSeed;
                x ^= levelIndex * 73856093;
                x ^= (x << 13);
                x ^= (x >> 17);
                x ^= (x << 5);
                return x;
            }
        }

        public static int ComputeShopRngSeedFromLevelSeed(int levelSeed, int levelIndex, int rerollCount)
        {
            unchecked
            {
                int baseSeed = levelSeed;
                int rngSeed = baseSeed ^ (levelIndex * 19349663) ^ 0x1234abcd ^ (rerollCount * unchecked((int)0x9e3779b9));
                return rngSeed;
            }
        }

        public static int ComputeShopRngSeedFromRunBaseSeed(int runBaseSeed, int levelIndex, int rerollCount)
        {
            int levelSeed = DeriveLevelSeed(runBaseSeed, levelIndex);
            return ComputeShopRngSeedFromLevelSeed(levelSeed, levelIndex, rerollCount);
        }

        public static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: VrfShopVerificationExample <runBaseSeed> <levelIndex> <rerollCount>");
                return;
            }

            int runBaseSeed = int.Parse(args[0]);
            int levelIndex = int.Parse(args[1]);
            int rerollCount = int.Parse(args[2]);

            int levelSeed = DeriveLevelSeed(runBaseSeed, levelIndex);
            int shopSeed = ComputeShopRngSeedFromLevelSeed(levelSeed, levelIndex, rerollCount);

            Console.WriteLine($"runBaseSeed={runBaseSeed}");
            Console.WriteLine($"levelIndex={levelIndex}");
            Console.WriteLine($"rerollCount={rerollCount}");
            Console.WriteLine($"levelSeed={levelSeed}");
            Console.WriteLine($"shopRngSeed={shopSeed}");

            var rng = new Random(shopSeed);
            Console.WriteLine("First 5 raw NextDouble() values from shop RNG:");
            for (int i = 0; i < 5; i++)
            {
                Console.WriteLine($"  NextDouble[{i}] = {rng.NextDouble()}");
            }
        }
    }
}
