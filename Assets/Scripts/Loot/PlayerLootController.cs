using System.Collections.Generic;
using UnityEngine;
using ChogZombies.Player;

namespace ChogZombies.Loot
{
    public class PlayerLootController : MonoBehaviour
    {
        [SerializeField] PlayerCombatController player;

        readonly List<LootItemDefinition> _equippedItems = new List<LootItemDefinition>();

        public IReadOnlyList<LootItemDefinition> EquippedItems => _equippedItems;

        void Awake()
        {
            if (player == null)
                player = GetComponent<PlayerCombatController>();
        }

        public void ApplyLoot(LootItemDefinition item)
        {
            TryApplyLoot(item);
        }

        public bool TryApplyLoot(LootItemDefinition item)
        {
            if (item == null)
                return false;
            if (player == null)
                return false;

            string key = !string.IsNullOrEmpty(item.Id) ? item.Id : item.name;
            for (int i = 0; i < _equippedItems.Count; i++)
            {
                var it = _equippedItems[i];
                if (it == null)
                    continue;
                string k = !string.IsNullOrEmpty(it.Id) ? it.Id : it.name;
                if (k == key)
                    return false;
            }

            _equippedItems.Add(item);
            ApplyEffectToPlayer(item);
            return true;
        }

        void ApplyEffectToPlayer(LootItemDefinition item)
        {
            switch (item.EffectType)
            {
                case LootEffectType.DamageMultiplier:
                    player.ApplyDamageMultiplier(1f + item.EffectValue);
                    break;
                case LootEffectType.FireRateMultiplier:
                    player.ApplyFireRateMultiplier(1f + item.EffectValue);
                    break;
                case LootEffectType.ArmorDamageReduction:
                    player.ApplyDamageTakenMultiplier(1f - item.EffectValue);
                    break;
            }
        }
    }
}
