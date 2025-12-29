using UnityEngine;

namespace ChogZombies.Loot
{
    public enum LootEffectType
    {
        DamageMultiplier,
        FireRateMultiplier,
        ArmorDamageReduction,
        CoinDropChanceOnKill,
        ExtraCoinsOnMap,
        RangeDamageBonus,
        StartRunPowerBoost,
        PersistentStartPower,
        CoinMagnetRadius,
        PersistentCoinPickupRadius,
        RunLootLuck,
        PersistentLootLuck,
        ContactDamage,
        GuardianDrone,
        AegisCharges
    }

    public enum LootRarity
    {
        // Garder les valeurs int existantes pour ne pas casser les assets déjà sérialisés.
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3,

        // Nouveaux tiers ajoutés sans modifier les indices existants.
        Uncommon = 4,
        Mythic = 5
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
        [SerializeField] bool runOnly;

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
        public bool IsRunOnly => runOnly;

        public static int GetRarityRank(LootRarity rarity)
        {
            switch (rarity)
            {
                case LootRarity.Common:
                    return 0;
                case LootRarity.Uncommon:
                    return 1;
                case LootRarity.Rare:
                    return 2;
                case LootRarity.Epic:
                    return 3;
                case LootRarity.Legendary:
                    return 4;
                case LootRarity.Mythic:
                    return 5;
                default:
                    return 0;
            }
        }
    }
}
