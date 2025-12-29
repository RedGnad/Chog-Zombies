using System;
using System.Collections.Generic;
using UnityEngine;
using ChogZombies.Player;

namespace ChogZombies.Loot
{
    public class PlayerLootController : MonoBehaviour
    {
        [Serializable]
        struct RarityMultiplier
        {
            public LootRarity rarity;
            [Min(0f)] public float multiplier;
        }

        [SerializeField] PlayerCombatController player;
        [Header("Multiplicateurs par rareté")]
        [SerializeField] RarityMultiplier[] rarityScaling =
        {
            new RarityMultiplier{rarity = LootRarity.Common,    multiplier = 1f},
            new RarityMultiplier{rarity = LootRarity.Uncommon,  multiplier = 1.2f},
            new RarityMultiplier{rarity = LootRarity.Rare,      multiplier = 1.5f},
            new RarityMultiplier{rarity = LootRarity.Epic,      multiplier = 2f},
            new RarityMultiplier{rarity = LootRarity.Legendary, multiplier = 3f},
            new RarityMultiplier{rarity = LootRarity.Mythic,    multiplier = 4f},
        };

        [Header("Spike Aura")]
        [SerializeField, Min(0f)] float spikeAuraDamagePerEffectPoint = 14f;
        [SerializeField, Min(0f)] float spikeAuraRadiusPerEffectPoint = 2.2f;
        [SerializeField] bool debugSpikeAuraLogs = false;
        [SerializeField] bool debugGuardianLogs = false;
        [SerializeField] bool debugAegisLogs = false;

        readonly List<LootItemDefinition> _equippedItems = new List<LootItemDefinition>();
        int _activeSpikeAuraSources;

        public static PlayerLootController Instance { get; private set; }
        public IReadOnlyList<LootItemDefinition> EquippedItems => _equippedItems;
        public event Action<LootItemDefinition, bool> EquipmentChanged;
        public bool HasActiveSpikeAura => _activeSpikeAuraSources > 0 && RunMetaEffects.SpikeAuraDamagePerSecond > 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (player == null)
                player = GetComponent<PlayerCombatController>();

            _activeSpikeAuraSources = 0;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public bool TryEquip(LootItemDefinition item)
        {
            if (!CanUseItem(item))
                return false;

            string key = GetKey(item);
            if (FindIndexByKey(key) >= 0)
                return false;

            _equippedItems.Add(item);
            ApplyEffect(item, true);
            EquipmentChanged?.Invoke(item, true);
            return true;
        }

        public bool TryUnequip(LootItemDefinition item)
        {
            if (!CanUseItem(item))
                return false;

            string key = GetKey(item);
            int index = FindIndexByKey(key);
            if (index < 0)
                return false;

            var removed = _equippedItems[index];
            _equippedItems.RemoveAt(index);
            ApplyEffect(removed, false);
            EquipmentChanged?.Invoke(removed, false);
            return true;
        }

        public bool ToggleEquip(LootItemDefinition item)
        {
            if (item == null)
                return false;
            return IsEquipped(item) ? TryUnequip(item) : TryEquip(item);
        }

        public void ApplyRunOnlyItem(LootItemDefinition item)
        {
            ApplyEffect(item, true);
        }

        /// <summary>
        /// Renvoie la valeur d'effet mise à l'échelle par la rareté (sans appliquer 1+ / 1-).
        /// Utile pour afficher dans l'UI le pourcentage réel de buff.
        /// </summary>
        public float GetScaledEffectValue(LootItemDefinition item)
        {
            if (item == null)
                return 0f;
            return Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
        }

        public bool IsEquipped(LootItemDefinition item)
        {
            if (item == null)
                return false;

            string key = GetKey(item);
            return FindIndexByKey(key) >= 0;
        }

        int FindIndexByKey(string key)
        {
            for (int i = 0; i < _equippedItems.Count; i++)
            {
                var equipped = _equippedItems[i];
                if (equipped == null)
                    continue;
                if (GetKey(equipped) == key)
                    return i;
            }
            return -1;
        }

        bool CanUseItem(LootItemDefinition item)
        {
            return item != null && player != null;
        }

        void ApplyEffect(LootItemDefinition item, bool apply)
        {
            if (item == null)
                return;

            switch (item.EffectType)
            {
                case LootEffectType.DamageMultiplier:
                case LootEffectType.FireRateMultiplier:
                case LootEffectType.ArmorDamageReduction:
                {
                    float factor = Mathf.Max(0.0001f, GetEffectFactor(item));
                    float value = apply ? factor : 1f / factor;

                    switch (item.EffectType)
                    {
                        case LootEffectType.DamageMultiplier:
                            player.ApplyDamageMultiplier(value);
                            break;
                        case LootEffectType.FireRateMultiplier:
                            player.ApplyFireRateMultiplier(value);
                            break;
                        case LootEffectType.ArmorDamageReduction:
                            player.ApplyDamageTakenMultiplier(value);
                            break;
                    }
                    break;
                }
                case LootEffectType.CoinDropChanceOnKill:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddCoinDropChance(delta);
                    break;
                }
                case LootEffectType.ExtraCoinsOnMap:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddExpectedExtraCoinsOnMap(delta);
                    break;
                }
                case LootEffectType.RangeDamageBonus:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddProjectileRangeBonus(delta);
                    break;
                }
                case LootEffectType.StartRunPowerBoost:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddStartRunPower(delta);
                    break;
                }
                case LootEffectType.PersistentStartPower:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddPersistentStartPower(delta);
                    break;
                }
                case LootEffectType.CoinMagnetRadius:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddCoinMagnetRadius(delta);
                    break;
                }
                case LootEffectType.PersistentCoinPickupRadius:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddPersistentCoinPickupRadius(delta);
                    break;
                }
                case LootEffectType.RunLootLuck:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddRunLootLuck(delta);
                    break;
                }
                case LootEffectType.PersistentLootLuck:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddPersistentLootLuck(delta);
                    break;
                }
                case LootEffectType.ContactDamage:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    bool hasEffect = scaled > 0f;
                    float damageDelta = ComputeSpikeAuraDamageFromValue(scaled);
                    float radiusDelta = ComputeSpikeAuraRadiusFromValue(scaled);
                    if (!apply)
                    {
                        damageDelta = -damageDelta;
                        radiusDelta = -radiusDelta;
                    }

                    if (hasEffect)
                    {
                        if (apply)
                            _activeSpikeAuraSources++;
                        else
                            _activeSpikeAuraSources = Mathf.Max(0, _activeSpikeAuraSources - 1);

                        if (debugSpikeAuraLogs)
                        {
                            Debug.Log($"[SpikeAura] {(apply ? "Equip" : "Unequip")} '{item.DisplayName}' scaled={scaled:F2} dmgDelta={damageDelta:F2} radiusDelta={radiusDelta:F2} activeSources={_activeSpikeAuraSources} totalDps={RunMetaEffects.SpikeAuraDamagePerSecond + damageDelta:F2}");
                        }
                    }

                    RunMetaEffects.AddSpikeAuraDamage(damageDelta);
                    RunMetaEffects.AddSpikeAuraRadius(radiusDelta);
                    break;
                }
                case LootEffectType.GuardianDrone:
                {
                    float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
                    float delta = apply ? scaled : -scaled;
                    RunMetaEffects.AddGuardianDronePower(delta);
                    if (debugGuardianLogs && Mathf.Abs(delta) > Mathf.Epsilon)
                    {
                        Debug.Log($"[GuardianDrone] {(apply ? "Equip" : "Unequip")} '{item.DisplayName}' delta={delta:F2} total={RunMetaEffects.GuardianDronePower:F2}");
                    }
                    break;
                }
                case LootEffectType.AegisCharges:
                {
                    int amount = Mathf.RoundToInt(Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity));
                    int delta = apply ? amount : -amount;
                    RunMetaEffects.AddAegisCharges(delta);
                    if (debugAegisLogs && delta != 0)
                    {
                        Debug.Log($"[Aegis] {(apply ? "Equip" : "Unequip")} '{item.DisplayName}' delta={delta} total={RunMetaEffects.AegisCharges}");
                    }
                    break;
                }
            }
        }

        float GetEffectFactor(LootItemDefinition item)
        {
            float scaled = Mathf.Max(0f, item.EffectValue) * GetRarityMultiplier(item.Rarity);
            switch (item.EffectType)
            {
                case LootEffectType.DamageMultiplier:
                case LootEffectType.FireRateMultiplier:
                    return 1f + scaled;
                case LootEffectType.ArmorDamageReduction:
                    return Mathf.Clamp01(1f - scaled);
                default:
                    return 1f + scaled;
            }
        }

        float GetRarityMultiplier(LootRarity rarity)
        {
            if (rarityScaling != null)
            {
                for (int i = 0; i < rarityScaling.Length; i++)
                {
                    if (rarityScaling[i].rarity == rarity)
                        return Mathf.Max(0f, rarityScaling[i].multiplier);
                }
            }
            return 1f;
        }

        float ComputeSpikeAuraDamageFromValue(float effectPoints)
        {
            return Mathf.Max(0f, effectPoints) * Mathf.Max(0f, spikeAuraDamagePerEffectPoint);
        }

        float ComputeSpikeAuraRadiusFromValue(float effectPoints)
        {
            return Mathf.Max(0f, effectPoints) * Mathf.Max(0f, spikeAuraRadiusPerEffectPoint);
        }

        static string GetKey(LootItemDefinition item)
        {
            if (item == null)
                return string.Empty;
            return !string.IsNullOrEmpty(item.Id) ? item.Id : item.name;
        }
    }
}
