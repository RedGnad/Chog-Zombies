using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ChogZombies.Loot;

namespace ChogZombies.UI
{
    /// <summary>
    /// UI de révélation de loot avec animations et effets selon la rareté.
    /// </summary>
    public class LootRevealUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] GameObject overlayRoot;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Item Display")]
        [SerializeField] Image itemIcon;
        [SerializeField] Image itemFrame;
        [SerializeField] Image itemGlow;
        [SerializeField] TMP_Text itemNameText;
        [SerializeField] TMP_Text itemDescriptionText;
        [SerializeField] TMP_Text rarityText;

        [Header("Particles")]
        [SerializeField] ParticleSystem revealParticles;
        [SerializeField] ParticleSystem glowParticles;

        [Header("Buttons")]
        [SerializeField] Button continueButton;
        [SerializeField] Button equipButton;

        [Header("Rarity Colors")]
        [SerializeField] Color commonColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] Color rareColor = new Color(0.2f, 0.5f, 1f);
        [SerializeField] Color epicColor = new Color(0.6f, 0.2f, 0.8f);
        [SerializeField] Color legendaryColor = new Color(1f, 0.7f, 0.2f);

        [Header("Animation")]
        [SerializeField] float fadeInDuration = 0.3f;
        [SerializeField] float revealDelay = 0.5f;
        [SerializeField] float iconScaleStart = 0.3f;
        [SerializeField] float iconScalePop = 1.2f;
        [SerializeField] float iconScaleFinal = 1f;
        [SerializeField] float scaleUpDuration = 0.3f;
        [SerializeField] float scaleDownDuration = 0.15f;
        [SerializeField] AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Header("Audio")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip commonRevealSfx;
        [SerializeField] AudioClip rareRevealSfx;
        [SerializeField] AudioClip epicRevealSfx;
        [SerializeField] AudioClip legendaryRevealSfx;

        public event Action OnContinueClicked;
        public event Action<LootItemDefinition> OnEquipClicked;

        public static LootRevealUI Instance { get; private set; }

        LootItemDefinition _currentItem;
        bool _isRevealing;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (overlayRoot == null)
                overlayRoot = gameObject;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (continueButton != null)
                continueButton.onClick.AddListener(HandleContinue);

            if (equipButton != null)
                equipButton.onClick.AddListener(HandleEquip);

            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Show(LootItemDefinition item)
        {
            if (item == null)
            {
                Debug.LogWarning("[LootRevealUI] Cannot show null item.");
                return;
            }

            _currentItem = item;
            _isRevealing = true;

            if (overlayRoot != null)
                overlayRoot.SetActive(true);

            SetupItemDisplay(item);
            StartCoroutine(RevealSequence(item));
        }

        public void Hide()
        {
            _isRevealing = false;
            _currentItem = null;

            StopAllCoroutines();

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }

        void SetupItemDisplay(LootItemDefinition item)
        {
            Color rarityColor = GetRarityColor(item.Rarity);

            if (itemIcon != null)
            {
                itemIcon.sprite = item.Icon;
                itemIcon.transform.localScale = Vector3.one * iconScaleStart;
                itemIcon.color = new Color(1, 1, 1, 0);
            }

            if (itemFrame != null)
                itemFrame.color = rarityColor;

            if (itemGlow != null)
            {
                itemGlow.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.5f);
                itemGlow.gameObject.SetActive(false);
            }

            if (itemNameText != null)
            {
                itemNameText.text = item.DisplayName;
                itemNameText.color = rarityColor;
                itemNameText.alpha = 0f;
            }

            if (itemDescriptionText != null)
            {
                itemDescriptionText.text = item.Description;
                itemDescriptionText.alpha = 0f;
            }

            if (rarityText != null)
            {
                rarityText.text = GetRarityDisplayName(item.Rarity);
                rarityText.color = rarityColor;
                rarityText.alpha = 0f;
            }

            if (continueButton != null)
                continueButton.gameObject.SetActive(false);

            if (equipButton != null)
                equipButton.gameObject.SetActive(false);
        }

        IEnumerator RevealSequence(LootItemDefinition item)
        {
            // Fade in overlay
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

            // Delay before reveal
            yield return new WaitForSecondsRealtime(revealDelay);

            // Play SFX
            PlayRevealSfx(item.Rarity);

            // Start particles
            if (revealParticles != null)
            {
                var main = revealParticles.main;
                main.startColor = GetRarityColor(item.Rarity);
                revealParticles.Play();
            }

            // Icon fade in + scale up
            elapsed = 0f;
            while (elapsed < scaleUpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = scaleCurve.Evaluate(elapsed / scaleUpDuration);

                if (itemIcon != null)
                {
                    float scale = Mathf.Lerp(iconScaleStart, iconScalePop, t);
                    itemIcon.transform.localScale = Vector3.one * scale;
                    itemIcon.color = new Color(1, 1, 1, t);
                }

                yield return null;
            }

            // Enable glow
            if (itemGlow != null)
                itemGlow.gameObject.SetActive(true);

            // Scale down to final
            elapsed = 0f;
            while (elapsed < scaleDownDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / scaleDownDuration;

                if (itemIcon != null)
                {
                    float scale = Mathf.Lerp(iconScalePop, iconScaleFinal, t);
                    itemIcon.transform.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            if (itemIcon != null)
                itemIcon.transform.localScale = Vector3.one * iconScaleFinal;

            // Fade in text
            float textFadeDuration = 0.3f;
            elapsed = 0f;
            while (elapsed < textFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / textFadeDuration;

                if (rarityText != null)
                    rarityText.alpha = t;
                if (itemNameText != null)
                    itemNameText.alpha = t;
                if (itemDescriptionText != null)
                    itemDescriptionText.alpha = t;

                yield return null;
            }

            // Start glow particles for rare+
            if (glowParticles != null && item.Rarity >= LootRarity.Rare)
            {
                var main = glowParticles.main;
                main.startColor = GetRarityColor(item.Rarity);
                glowParticles.Play();
            }

            // Show buttons
            yield return new WaitForSecondsRealtime(0.2f);

            if (continueButton != null)
                continueButton.gameObject.SetActive(true);

            if (equipButton != null)
                equipButton.gameObject.SetActive(true);

            _isRevealing = false;
        }

        void PlayRevealSfx(LootRarity rarity)
        {
            if (audioSource == null)
                return;

            AudioClip clip = rarity switch
            {
                LootRarity.Common => commonRevealSfx,
                LootRarity.Rare => rareRevealSfx,
                LootRarity.Epic => epicRevealSfx,
                LootRarity.Legendary => legendaryRevealSfx,
                _ => commonRevealSfx
            };

            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        Color GetRarityColor(LootRarity rarity)
        {
            return rarity switch
            {
                LootRarity.Common => commonColor,
                LootRarity.Rare => rareColor,
                LootRarity.Epic => epicColor,
                LootRarity.Legendary => legendaryColor,
                _ => commonColor
            };
        }

        string GetRarityDisplayName(LootRarity rarity)
        {
            return rarity switch
            {
                LootRarity.Common => "COMMUN",
                LootRarity.Rare => "RARE",
                LootRarity.Epic => "ÉPIQUE",
                LootRarity.Legendary => "LÉGENDAIRE",
                _ => "COMMUN"
            };
        }

        void HandleContinue()
        {
            if (_isRevealing)
                return;

            Hide();
            OnContinueClicked?.Invoke();
        }

        void HandleEquip()
        {
            if (_isRevealing)
                return;

            var item = _currentItem;
            Hide();
            OnEquipClicked?.Invoke(item);
        }

        /// <summary>
        /// Méthode statique pour afficher rapidement un loot.
        /// </summary>
        public static void ShowLoot(LootItemDefinition item)
        {
            if (Instance != null)
                Instance.Show(item);
            else
                Debug.LogWarning("[LootRevealUI] No instance found. Cannot show loot.");
        }
    }
}
