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
                iconImage.color = isOwned ? Color.white : lockedColor;
            }

            UpdateEquippedVisuals();

            if (lockedOverlay != null)
            {
                lockedOverlay.SetActive(!isOwned);
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
                bool showGlow = _isOwned && _item != null && _item.Rarity >= LootRarity.Rare;
                glowImage.gameObject.SetActive(showGlow);
                if (showGlow)
                {
                    float alpha = _isEquipped ? 0.45f : 0.3f;
                    glowImage.color = new Color(_rarityColor.r, _rarityColor.g, _rarityColor.b, alpha);
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
