using UnityEngine;

namespace ChogZombies.Loot
{
    public enum LootEffectType
    {
        DamageMultiplier,
        FireRateMultiplier,
        ArmorDamageReduction
    }

    public enum LootRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [CreateAssetMenu(fileName = "LootItem", menuName = "ChogZombies/Loot Item")]
    public class LootItemDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string familyId;
        [SerializeField] string displayName;
        [SerializeField] string description;
        [SerializeField] Sprite icon;
        [SerializeField] LootRarity rarity = LootRarity.Common;
        [SerializeField] LootEffectType effectType = LootEffectType.DamageMultiplier;
        [SerializeField] float effectValue = 0.1f;
        [SerializeField] float dropWeight = 1f;

        public string Id => id;
        public string FamilyId => string.IsNullOrEmpty(familyId)
            ? (!string.IsNullOrEmpty(id) ? id : name)
            : familyId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public LootRarity Rarity => rarity;
        public LootEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public float DropWeight => dropWeight;
    }
}
