using System.Collections.Generic;
using UnityEngine;

namespace ChogZombies.Loot
{
    [CreateAssetMenu(fileName = "LootTable", menuName = "ChogZombies/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [SerializeField] List<LootItemDefinition> items = new List<LootItemDefinition>();

        public LootItemDefinition RollItem(System.Random rng)
        {
            if (items == null || items.Count == 0)
                return null;

            float totalWeight = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                    continue;
                if (item.DropWeight <= 0f)
                    continue;
                totalWeight += item.DropWeight;
            }

            if (totalWeight <= 0f)
                return null;

            double r = rng.NextDouble() * totalWeight;
            float accum = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                    continue;
                if (item.DropWeight <= 0f)
                    continue;

                accum += item.DropWeight;
                if (r <= accum)
                    return item;
            }

            return null;
        }
    }
}
