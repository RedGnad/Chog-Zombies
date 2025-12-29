using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ChogZombies.Loot;

namespace ChogZombies.UI
{
    /// <summary>
    /// UI d'inventaire affichant les items possédés et leurs détails.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Grid")]
        [SerializeField] Transform itemGridParent;
        [SerializeField] GameObject itemSlotPrefab;

        [Header("Details Panel")]
        [SerializeField] GameObject detailsPanel;
        [SerializeField] Image detailIcon;
        [SerializeField] Image detailFrame;
        [SerializeField] TMP_Text detailName;
        [SerializeField] TMP_Text detailRarity;
        [SerializeField] TMP_Text detailDescription;
        [SerializeField] TMP_Text detailEffect;
        [SerializeField] TMP_Text detailEquippedStatus;

        [Header("Header")]
        [SerializeField] TMP_Text collectionProgressText;
        [SerializeField] Button closeButton;

        [Header("Rarity Colors")]
        [SerializeField] Color commonColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] Color uncommonColor = new Color(0.4f, 0.8f, 0.4f);
        [SerializeField] Color rareColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] Color epicColor = new Color(0.6f, 0.2f, 0.8f);
        [SerializeField] Color legendaryColor = new Color(1f, 0.7f, 0.2f);
        [SerializeField] Color mythicColor = new Color(1f, 0.3f, 0.9f);
        [SerializeField] Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

        [Header("Animation")]
        [SerializeField] float fadeInDuration = 0.2f;
        [SerializeField] float fadeOutDuration = 0.2f;

        MetaProgressionController _meta;
        PlayerLootController _loot;
        readonly List<InventorySlot> _slots = new List<InventorySlot>();
        readonly Dictionary<LootItemDefinition, InventorySlot> _slotLookup = new Dictionary<LootItemDefinition, InventorySlot>();
        LootItemDefinition _selectedItem;
        bool _equipmentSubscribed;

        public static InventoryUI Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (panelRoot == null)
                panelRoot = gameObject;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (_loot != null && _equipmentSubscribed)
            {
                _loot.EquipmentChanged -= HandleEquipmentChanged;
                _equipmentSubscribed = false;
            }
        }

        public void Show()
        {
            EnsureDependencies();
            RegisterEquipmentListener();

            if (panelRoot != null)
                panelRoot.SetActive(true);

            RefreshGrid();
            UpdateCollectionProgress();
            ClearDetails();

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeIn());
            }
        }

        public void Hide()
        {
            _selectedItem = null;

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOut());
            }
            else if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (panelRoot != null && panelRoot.activeSelf)
                Hide();
            else
                Show();
        }

        void RefreshGrid()
        {
            foreach (var slot in _slots)
            {
                if (slot != null && slot.gameObject != null)
                    Destroy(slot.gameObject);
            }
            _slots.Clear();
            _slotLookup.Clear();

            if (_meta == null || itemGridParent == null || itemSlotPrefab == null)
            {
                Debug.LogWarning($"[InventoryUI] RefreshGrid aborted. meta={_meta != null}, itemGridParent={itemGridParent != null}, itemSlotPrefab={itemSlotPrefab != null}");
                return;
            }

            var allItems = _meta.AllLootItems;
            Debug.Log($"[InventoryUI] RefreshGrid: allItems={allItems?.Count ?? 0}, owned={_meta.OwnedCount}/{_meta.TotalCount}");

            var families = new Dictionary<string, List<LootItemDefinition>>();
            if (allItems != null)
            {
                for (int i = 0; i < allItems.Count; i++)
                {
                    var item = allItems[i];
                    if (item == null)
                        continue;

                    string family = item.FamilyId;
                    if (string.IsNullOrEmpty(family))
                        family = MetaProgressionController.GetKey(item);

                    if (!families.TryGetValue(family, out var list))
                    {
                        list = new List<LootItemDefinition>();
                        families[family] = list;
                    }
                    list.Add(item);
                }
            }

            foreach (var kvp in families)
            {
                var list = kvp.Value;
                if (list == null || list.Count == 0)
                    continue;

                LootItemDefinition templateItem = null;
                LootItemDefinition bestOwned = null;

                for (int i = 0; i < list.Count; i++)
                {
                    var candidate = list[i];
                    if (candidate == null)
                        continue;

                    if (templateItem == null)
                        templateItem = candidate;

                    if (_meta.IsOwned(candidate))
                    {
                        if (bestOwned == null || LootItemDefinition.GetRarityRank(candidate.Rarity) > LootItemDefinition.GetRarityRank(bestOwned.Rarity))
                            bestOwned = candidate;
                    }
                }

                var displayItem = bestOwned != null ? bestOwned : templateItem;
                if (displayItem == null)
                    continue;

                var slotGO = Instantiate(itemSlotPrefab, itemGridParent);
                var slotTransform = slotGO.transform as RectTransform;
                if (slotTransform == null)
                {
                    slotTransform = slotGO.AddComponent<RectTransform>();
                    slotTransform.sizeDelta = new Vector2(100f, 100f);
                }
                slotTransform.anchorMin = new Vector2(0.5f, 0.5f);
                slotTransform.anchorMax = new Vector2(0.5f, 0.5f);
                slotTransform.pivot = new Vector2(0.5f, 0.5f);
                slotTransform.localScale = Vector3.one;
                slotTransform.localRotation = Quaternion.identity;
                slotTransform.anchoredPosition = Vector2.zero;

                var nestedCanvases = slotGO.GetComponentsInChildren<Canvas>(true);
                for (int i = 0; i < nestedCanvases.Length; i++)
                {
                    var canvasRect = nestedCanvases[i].transform as RectTransform;
                    if (canvasRect != null && canvasRect.localScale == Vector3.zero)
                        canvasRect.localScale = Vector3.one;
                }
                var slot = slotGO.GetComponent<InventorySlot>();
                if (slot == null)
                    slot = slotGO.AddComponent<InventorySlot>();

                bool isOwned = bestOwned != null;
                bool isEquipped = isOwned && bestOwned != null && _meta.IsEquipped(bestOwned);
                Debug.Log($"[InventoryUI]   Slot for family '{kvp.Key}' using '{displayItem.DisplayName}' (owned={isOwned}, icon={(displayItem.Icon != null ? "yes" : "no")})");
                slot.Setup(displayItem, isOwned, isEquipped, GetRarityColor(displayItem.Rarity), lockedColor);
                slot.OnClicked += OnSlotClicked;

                _slots.Add(slot);
                if (!_slotLookup.ContainsKey(displayItem))
                    _slotLookup.Add(displayItem, slot);
            }

            var gridRect = itemGridParent as RectTransform;
            if (gridRect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
            }
        }

        void UpdateCollectionProgress()
        {
            if (collectionProgressText == null || _meta == null)
                return;

            int owned = _meta.OwnedCount;
            int total = _meta.TotalCount;
            float percent = total > 0 ? (owned / (float)total) * 100f : 0f;

            collectionProgressText.text = $"Collection: {owned}/{total} ({percent:F0}%)";
        }

        void OnSlotClicked(LootItemDefinition item, bool isOwned)
        {
            if (item == null)
                return;

            bool isEquipped = _meta != null && item != null && _meta.IsEquipped(item);
            bool toggled = false;

            if (isOwned && _meta != null)
            {
                var loot = EnsureLootController();
                if (loot != null)
                {
                    bool targetEquipped = !isEquipped;
                    if (_meta.SetEquipped(item, targetEquipped))
                    {
                        toggled = true;
                        isEquipped = targetEquipped;

                        if (!item.IsRunOnly)
                        {
                            if (targetEquipped)
                                loot.TryEquip(item);
                            else
                                loot.TryUnequip(item);
                        }

                        // Pousser le nouvel état d'équipement vers le backend (owned/equipped)
                        try
                        {
                            var lootBackend = FindObjectOfType<ChogZombies.Reown.LootBackendSync>();
                            lootBackend?.PushForCurrentWallet();
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[InventoryUI] Failed to push loot state to backend: {e.Message}");
                        }

                        if (_slotLookup.TryGetValue(item, out var slot) && slot != null)
                            slot.SetEquipped(isEquipped && isOwned);
                    }
                }
            }

            _selectedItem = item;
            ShowDetails(item, isOwned, isEquipped);

            if (!toggled && !isOwned)
                Debug.Log($"[InventoryUI] '{item.DisplayName}' non possédé : affichage des détails uniquement.");
        }

        void ShowDetails(LootItemDefinition item, bool isOwned, bool isEquipped)
        {
            Color rarityColor = GetRarityColor(item.Rarity);

            if (detailIcon != null)
            {
                detailIcon.sprite = item.Icon;
                detailIcon.color = isOwned ? Color.white : lockedColor;
            }

            if (detailFrame != null)
                detailFrame.color = isOwned ? rarityColor : lockedColor;

            if (detailName != null)
            {
                detailName.text = isOwned ? item.DisplayName : "???";
                detailName.color = isOwned ? rarityColor : lockedColor;
            }

            if (detailRarity != null)
            {
                detailRarity.text = GetRarityDisplayName(item.Rarity);
                detailRarity.color = isOwned ? rarityColor : lockedColor;
            }

            if (detailDescription != null)
            {
                detailDescription.text = isOwned ? item.Description : "Item not discovered";
            }

            if (detailEffect != null)
            {
                if (isOwned)
                {
                    string effectText = GetEffectDescription(item);
                    detailEffect.text = effectText;
                    detailEffect.color = Color.green;
                }
                else
                {
                    detailEffect.text = "";
                }
            }

            if (detailEquippedStatus != null)
            {
                if (isOwned)
                {
                    detailEquippedStatus.gameObject.SetActive(true);
                    detailEquippedStatus.text = isEquipped ? "EQUIPPED" : "Available";
                    detailEquippedStatus.color = isEquipped ? Color.green : Color.white;
                }
                else
                {
                    detailEquippedStatus.gameObject.SetActive(true);
                    detailEquippedStatus.text = "Not owned";
                    detailEquippedStatus.color = lockedColor;
                }
            }

        }

        void ClearDetails()
        {
            _selectedItem = null;

            if (detailIcon != null)
                detailIcon.sprite = null;
            if (detailName != null)
                detailName.text = "Select an item";
            if (detailRarity != null)
                detailRarity.text = string.Empty;
            if (detailDescription != null)
                detailDescription.text = string.Empty;
            if (detailEffect != null)
                detailEffect.text = string.Empty;
            if (detailEquippedStatus != null)
            {
                detailEquippedStatus.gameObject.SetActive(false);
                detailEquippedStatus.text = string.Empty;
            }
        }

        string GetEffectDescription(LootItemDefinition item)
        {
            if (item == null)
                return string.Empty;

            // Utiliser la même logique de scaling que le gameplay (PlayerLootController)
            float scaled = 0f;
            var loot = PlayerLootController.Instance;
            if (loot != null)
                scaled = loot.GetScaledEffectValue(item);
            else
                scaled = Mathf.Max(0f, item.EffectValue);

            float percent = scaled * 100f;

            return item.EffectType switch
            {
                LootEffectType.DamageMultiplier => $"+{percent:F0}% Damage",
                LootEffectType.FireRateMultiplier => $"+{percent:F0}% Fire rate",
                LootEffectType.ArmorDamageReduction => $"-{percent:F0}% Damage taken",
                LootEffectType.CoinDropChanceOnKill => $"+{percent:F0}% coin drop chance",
                LootEffectType.RangeDamageBonus => $"+{percent:F0}% range",
                LootEffectType.ExtraCoinsOnMap => $"+{percent:F0}% extra coins on map",
                LootEffectType.StartRunPowerBoost => $"+{percent:F0}% start power",
                LootEffectType.PersistentStartPower => $"+{percent:F0}% permanent start power",
                _ => $"+{percent:F0}%"
            };
        }

        Color GetRarityColor(LootRarity rarity)
        {
            switch (rarity)
            {
                case LootRarity.Common:
                    return commonColor;
                case LootRarity.Uncommon:
                    return uncommonColor;
                case LootRarity.Rare:
                    return rareColor;
                case LootRarity.Epic:
                    return epicColor;
                case LootRarity.Legendary:
                    return legendaryColor;
                case LootRarity.Mythic:
                    return mythicColor;
                default:
                    return commonColor;
            }
        }

        string GetRarityDisplayName(LootRarity rarity)
        {
            switch (rarity)
            {
                case LootRarity.Common:
                    return "COMMON";
                case LootRarity.Uncommon:
                    return "UNCOMMON";
                case LootRarity.Rare:
                    return "RARE";
                case LootRarity.Epic:
                    return "EPIC";
                case LootRarity.Legendary:
                    return "LEGENDARY";
                case LootRarity.Mythic:
                    return "MYTHIC";
                default:
                    return "COMMON";
            }
        }

        void HandleEquipmentChanged(LootItemDefinition item, bool equipped)
        {
            if (item == null)
                return;

            if (_slotLookup.TryGetValue(item, out var slot) && slot != null)
                slot.SetEquipped(equipped);

            if (_selectedItem == item)
                ShowDetails(item, _meta != null && _meta.IsOwned(item), equipped);
        }

        void RegisterEquipmentListener()
        {
            var loot = EnsureLootController();
            if (loot == null || _equipmentSubscribed)
                return;

            loot.EquipmentChanged += HandleEquipmentChanged;
            _equipmentSubscribed = true;
        }

        void EnsureDependencies()
        {
            if (_meta == null)
                _meta = FindObjectOfType<MetaProgressionController>();
            EnsureLootController();
        }

        PlayerLootController EnsureLootController()
        {
            if (_loot != null)
                return _loot;

            _loot = PlayerLootController.Instance ?? FindObjectOfType<PlayerLootController>();
            return _loot;
        }

        System.Collections.IEnumerator FadeIn()
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        System.Collections.IEnumerator FadeOut()
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                yield return null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (panelRoot != null)
                panelRoot.SetActive(false);
        }
    }
}
