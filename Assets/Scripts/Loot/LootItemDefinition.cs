using UnityEngine;

namespace ChogZombies.Loot
{
    public enum LootEffectType
    {
        DamageMultiplier,
        FireRateMultiplier,
        ArmorDamageReduction
    }

    [CreateAssetMenu(fileName = "LootItem", menuName = "ChogZombies/Loot Item")]
    public class LootItemDefinition : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] string description;
        [SerializeField] Sprite icon;
        [SerializeField] LootEffectType effectType = LootEffectType.DamageMultiplier;
        [SerializeField] float effectValue = 0.1f;
        [SerializeField] float dropWeight = 1f;

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public LootEffectType EffectType => effectType;
        public float EffectValue => effectValue;
        public float DropWeight => dropWeight;
    }
}
