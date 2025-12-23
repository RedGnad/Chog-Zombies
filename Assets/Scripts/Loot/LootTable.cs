using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChogZombies.Loot
{
    [CreateAssetMenu(fileName = "LootTable", menuName = "ChogZombies/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        struct RarityWeight
        {
            public LootRarity rarity;
            [Min(0f)] public float weight;
        }

        [SerializeField] List<LootItemDefinition> items = new List<LootItemDefinition>();
        [SerializeField] RarityWeight[] rarityWeights =
        {
            new RarityWeight{rarity = LootRarity.Common,    weight = 60f},
            new RarityWeight{rarity = LootRarity.Rare,      weight = 25f},
            new RarityWeight{rarity = LootRarity.Epic,      weight = 10f},
            new RarityWeight{rarity = LootRarity.Legendary, weight = 5f},
        };

        public LootItemDefinition RollItem(System.Random rng)
        {
            if (items == null || items.Count == 0)
                return null;

            // Regrouper les items par rareté présents dans cette table
            var byRarity = new Dictionary<LootRarity, List<LootItemDefinition>>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                    continue;

                if (!byRarity.TryGetValue(item.Rarity, out var list))
                {
                    list = new List<LootItemDefinition>();
                    byRarity[item.Rarity] = list;
                }
                list.Add(item);
            }

            if (byRarity.Count == 0)
                return null;

            // Étape 1 : tirage de la rareté selon les poids configurés
            float totalWeight = 0f;
            for (int i = 0; i < rarityWeights.Length; i++)
            {
                var rw = rarityWeights[i];
                if (rw.weight <= 0f)
                    continue;
                if (!byRarity.ContainsKey(rw.rarity))
                    continue;
                totalWeight += rw.weight;
            }

            if (totalWeight <= 0f)
                return null;

            double roll = rng.NextDouble() * totalWeight;
            float accum = 0f;
            LootRarity pickedRarity = LootRarity.Common;
            bool rarityFound = false;

            for (int i = 0; i < rarityWeights.Length; i++)
            {
                var rw = rarityWeights[i];
                if (rw.weight <= 0f)
                    continue;
                if (!byRarity.ContainsKey(rw.rarity))
                    continue;

                accum += rw.weight;
                if (roll <= accum)
                {
                    pickedRarity = rw.rarity;
                    rarityFound = true;
                    break;
                }
            }

            if (!rarityFound)
            {
                // Fallback : prendre n'importe quelle rareté disponible
                foreach (var kvp in byRarity)
                {
                    pickedRarity = kvp.Key;
                    rarityFound = true;
                    break;
                }
            }

            if (!rarityFound)
                return null;

            var candidates = byRarity[pickedRarity];
            if (candidates == null || candidates.Count == 0)
                return null;

            int index = rng.Next(0, candidates.Count);
            return candidates[index];
        }
    }
}
