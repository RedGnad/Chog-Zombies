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
            if (item == null)
                return;
            if (player == null)
                return;

            _equippedItems.Add(item);
            ApplyEffectToPlayer(item);
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
