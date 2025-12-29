using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ChogZombies.Loot;

namespace ChogZombies.UI
{
    /// <summary>
    /// Slot individuel dans la grille d'inventaire.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] Image iconImage;
        [SerializeField] Image frameImage;
        [SerializeField] Image glowImage;
        [SerializeField] GameObject newBadge;
        [SerializeField] GameObject equippedMarker;
        [SerializeField] GameObject lockedOverlay;
        [SerializeField] Color lockedIconTint = new Color(0.05f, 0.05f, 0.05f, 1f);
        [SerializeField, Range(0f, 1f)] float lockedOverlayAlpha = 0.4f;

        [Header("Animation")]
        [SerializeField] float hoverScale = 1.1f;
        [SerializeField] float normalScale = 1f;
        [SerializeField] float scaleSpeed = 10f;

        Button _button;
        LootItemDefinition _item;
        bool _isOwned;
        bool _isEquipped;
        Color _rarityColor = Color.white;
        Color _lockedColor = Color.gray;
        float _targetScale;
        bool _glowLayoutInitialized;

        public event Action<LootItemDefinition, bool> OnClicked;

        void Awake()
        {
            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>();
            }

            _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(HandleClick);

            _targetScale = normalScale;
        }

        void Update()
        {
            float currentScale = transform.localScale.x;
            if (Mathf.Abs(currentScale - _targetScale) > 0.001f)
            {
                float newScale = Mathf.Lerp(currentScale, _targetScale, Time.unscaledDeltaTime * scaleSpeed);
                transform.localScale = Vector3.one * newScale;
            }
        }

        public LootItemDefinition Item => _item;
        public bool IsOwned => _isOwned;
        public bool IsEquipped => _isEquipped && _isOwned;

        public void Setup(LootItemDefinition item, bool isOwned, bool isEquipped, Color rarityColor, Color lockedColor)
        {
            _item = item;
            _isOwned = isOwned;
            _isEquipped = isEquipped;
            _rarityColor = rarityColor;
            _lockedColor = lockedColor;

            if (iconImage != null)
            {
                if (item != null && item.Icon != null)
                {
                    iconImage.sprite = item.Icon;
                }

                if (isOwned)
                {
                    iconImage.color = Color.white;
                }
                else
                {
                    // Afficher l'icône en mode silhouette (teinte sombre) plutôt qu'un carré noir.
                    Color tint = lockedIconTint;
                    tint.a = 1f;
                    iconImage.color = tint;
                }
            }

            // S'assurer que le glow est derrière et légèrement plus grand que l'icône
            if (glowImage != null && !_glowLayoutInitialized)
            {
                var rt = glowImage.rectTransform;
                // Premier enfant => dessiné derrière les autres (halo en fond)
                rt.SetAsFirstSibling();

                // Légèrement plus grand que le slot pour bien dépasser
                var size = rt.sizeDelta;
                float extra = 20f;
                rt.sizeDelta = new Vector2(size.x + extra, size.y + extra);

                _glowLayoutInitialized = true;
            }

            UpdateEquippedVisuals();

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!isOwned);

                var overlayImage = lockedOverlay.GetComponent<Image>();
                if (overlayImage != null)
                {
                    if (!isOwned)
                        overlayImage.color = new Color(0f, 0f, 0f, lockedOverlayAlpha);
                    else
                        overlayImage.color = Color.clear;
                }
            }

            if (newBadge != null)
            {
                // Afficher "NEW" si c'est le dernier item acquis
                var meta = FindObjectOfType<MetaProgressionController>();
                bool isNew = meta != null && meta.LastAcquiredItem == item && isOwned;
                newBadge.SetActive(isNew);
            }
        }

        public void SetEquipped(bool equipped)
        {
            _isEquipped = equipped;
            UpdateEquippedVisuals();
        }

        void HandleClick()
        {
            OnClicked?.Invoke(_item, _isOwned);
        }

        void UpdateEquippedVisuals()
        {
            if (frameImage != null)
            {
                if (_isOwned)
                {
                    frameImage.color = _isEquipped
                        ? Color.Lerp(_rarityColor, Color.white, 0.35f)
                        : _rarityColor;
                }
                else
                {
                    frameImage.color = _lockedColor;
                }
            }

            if (glowImage != null)
            {
                bool showGlow = _isOwned
                                && _item != null
                                && LootItemDefinition.GetRarityRank(_item.Rarity) >= LootItemDefinition.GetRarityRank(LootRarity.Uncommon);
                glowImage.gameObject.SetActive(showGlow);
                if (showGlow)
                {
                    // Alpha un peu plus fort pour que le halo soit bien visible sur le fond gris
                    float alpha = _isEquipped ? 0.6f : 0.45f;
                    var c = new Color(_rarityColor.r, _rarityColor.g, _rarityColor.b, alpha);
                    glowImage.color = c;
                    Debug.Log($"[InventorySlot] Glow ON for '{_item.DisplayName}' rarity={_item.Rarity} color={c}");
                }
                else
                {
                    Debug.Log($"[InventorySlot] Glow OFF for '{(_item != null ? _item.DisplayName : "<null>")}' owned={_isOwned} rarity={(_item != null ? _item.Rarity.ToString() : "<null>")}");
                }
            }

            if (equippedMarker != null)
            {
                equippedMarker.SetActive(_isOwned && _isEquipped);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = normalScale;
        }
    }
}
