using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ChogZombies.UI
{
    /// <summary>
    /// Overlay UI pour afficher la progression des appels VRF (seed, loot).
    /// Affiche un spinner, une barre de progression, et des messages d'état.
    /// </summary>
    public class VRFLoadingUI : MonoBehaviour
    {
        public enum VRFState
        {
            Hidden,
            WaitingWallet,
            SigningTransaction,
            TransactionPending,
            WaitingSettlement,
            ResolvingRandomness,
            Settling,
            Complete,
            Error
        }

        public enum VRFContext
        {
            RunSeed,
            BossLoot,
            RerollSeed
        }

        [Header("Root")]
        [SerializeField] GameObject overlayRoot;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Spinner")]
        [SerializeField] GameObject spinnerObject;
        [SerializeField] float spinnerSpeed = 180f;

        [Header("Progress")]
        [SerializeField] Slider progressBar;
        [SerializeField] Image progressFill;

        [Header("Text")]
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text statusText;
        [SerializeField] TMP_Text detailsText;

        [Header("Buttons")]
        [SerializeField] Button retryButton;
        [SerializeField] Button cancelButton;
        [SerializeField] Button detailsButton;

        [Header("Colors")]
        [SerializeField] Color normalColor = new Color(0.2f, 0.6f, 1f);
        [SerializeField] Color successColor = new Color(0.2f, 0.8f, 0.2f);
        [SerializeField] Color errorColor = new Color(1f, 0.3f, 0.3f);

        [Header("Settings")]
        [SerializeField] float fadeInDuration = 0.2f;
        [SerializeField] float fadeOutDuration = 0.3f;
        [SerializeField] float autoHideDelay = 1.5f;

        VRFState _currentState = VRFState.Hidden;
        VRFContext _currentContext = VRFContext.RunSeed;
        float _targetProgress;
        float _currentProgress;
        float _settlementStartTime;
        float _settlementDuration;
        string _lastTxHash;
        string _lastError;

        public event Action OnRetryClicked;
        public event Action OnCancelClicked;

        public static VRFLoadingUI Instance { get; private set; }

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

            if (retryButton != null)
                retryButton.onClick.AddListener(() => OnRetryClicked?.Invoke());

            if (cancelButton != null)
                cancelButton.onClick.AddListener(() => OnCancelClicked?.Invoke());

            if (detailsButton != null)
                detailsButton.onClick.AddListener(OnDetailsClicked);

            Hide();
        }

        void Update()
        {
            if (_currentState == VRFState.Hidden)
                return;

            // Spinner rotation
            if (spinnerObject != null && spinnerObject.activeSelf)
            {
                spinnerObject.transform.Rotate(0, 0, -spinnerSpeed * Time.deltaTime);
            }

            // Smooth progress bar
            if (progressBar != null)
            {
                // Pendant WaitingSettlement, on fait une progression automatique basée sur le temps
                if (_currentState == VRFState.WaitingSettlement && _settlementDuration > 0)
                {
                    float elapsed = Time.time - _settlementStartTime;
                    _targetProgress = Mathf.Clamp01(elapsed / _settlementDuration) * 0.9f + 0.1f; // 10% à 100%
                }

                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.deltaTime * 5f);
                progressBar.value = _currentProgress;
            }
        }

        public void Show(VRFContext context, string customTitle = null)
        {
            _currentContext = context;
            _lastError = null;
            _lastTxHash = null;

            if (overlayRoot != null)
                overlayRoot.SetActive(true);

            if (titleText != null)
            {
                if (!string.IsNullOrEmpty(customTitle))
                    titleText.text = customTitle;
                else
                    titleText.text = GetDefaultTitle(context);
            }

            SetState(VRFState.WaitingWallet);
            SetProgress(0f);
            UpdateButtonsVisibility();

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeIn());
            }
        }

        public void Hide()
        {
            _currentState = VRFState.Hidden;

            if (canvasGroup != null)
            {
                StopAllCoroutines();
                StartCoroutine(FadeOut());
            }
            else if (overlayRoot != null)
            {
                overlayRoot.SetActive(false);
            }
        }

        public void SetState(VRFState state, string customMessage = null)
        {
            _currentState = state;

            if (statusText != null)
            {
                statusText.text = customMessage ?? GetDefaultStatusMessage(state);
            }

            // Update progress par état
            switch (state)
            {
                case VRFState.WaitingWallet:
                    SetProgress(0.05f);
                    break;
                case VRFState.SigningTransaction:
                    SetProgress(0.1f);
                    break;
                case VRFState.TransactionPending:
                    SetProgress(0.2f);
                    break;
                case VRFState.WaitingSettlement:
                    // Progress géré automatiquement par Update()
                    break;
                case VRFState.ResolvingRandomness:
                    SetProgress(0.85f);
                    break;
                case VRFState.Settling:
                    SetProgress(0.95f);
                    break;
                case VRFState.Complete:
                    SetProgress(1f);
                    SetProgressColor(successColor);
                    StartCoroutine(AutoHideAfterDelay());
                    break;
                case VRFState.Error:
                    SetProgressColor(errorColor);
                    break;
            }

            // Spinner visible sauf Complete/Error
            if (spinnerObject != null)
            {
                spinnerObject.SetActive(state != VRFState.Complete && state != VRFState.Error && state != VRFState.Hidden);
            }

            UpdateButtonsVisibility();
        }

        public void SetSettlementWait(float durationSeconds)
        {
            _settlementStartTime = Time.time;
            _settlementDuration = durationSeconds;
            SetState(VRFState.WaitingSettlement);
        }

        public void SetTxHash(string txHash)
        {
            _lastTxHash = txHash;
            UpdateDetailsVisibility();
        }

        public void SetError(string errorMessage)
        {
            _lastError = errorMessage;
            SetState(VRFState.Error, $"Erreur: {TruncateMessage(errorMessage, 80)}");
        }

        public void SetComplete(string message = null)
        {
            SetState(VRFState.Complete, message ?? GetCompletionMessage(_currentContext));
        }

        void SetProgress(float value)
        {
            _targetProgress = Mathf.Clamp01(value);
            if (progressFill != null && _currentState != VRFState.Error && _currentState != VRFState.Complete)
            {
                progressFill.color = normalColor;
            }
        }

        void SetProgressColor(Color color)
        {
            if (progressFill != null)
                progressFill.color = color;
        }

        void UpdateButtonsVisibility()
        {
            bool showRetry = _currentState == VRFState.Error;
            bool showCancel = _currentState != VRFState.Complete && _currentState != VRFState.Hidden;

            if (retryButton != null)
                retryButton.gameObject.SetActive(showRetry);

            if (cancelButton != null)
                cancelButton.gameObject.SetActive(showCancel);

            UpdateDetailsVisibility();
        }

        void UpdateDetailsVisibility()
        {
            if (detailsButton != null)
            {
                detailsButton.gameObject.SetActive(!string.IsNullOrEmpty(_lastTxHash));
            }

            if (detailsText != null)
            {
                detailsText.gameObject.SetActive(false); // Par défaut caché
            }
        }

        void OnDetailsClicked()
        {
            if (detailsText != null)
            {
                bool isActive = detailsText.gameObject.activeSelf;
                detailsText.gameObject.SetActive(!isActive);

                if (!isActive && !string.IsNullOrEmpty(_lastTxHash))
                {
                    detailsText.text = $"Tx: {_lastTxHash}";
                }
            }

            // Optionnel: ouvrir l'explorateur
            if (!string.IsNullOrEmpty(_lastTxHash))
            {
                string explorerUrl = $"https://monadexplorer.com/tx/{_lastTxHash}";
                Debug.Log($"[VRF UI] Explorer URL: {explorerUrl}");
                // Application.OpenURL(explorerUrl); // Décommenter si tu veux ouvrir automatiquement
            }
        }

        string GetDefaultTitle(VRFContext context)
        {
            return context switch
            {
                VRFContext.RunSeed => "Génération du Seed",
                VRFContext.BossLoot => "Loot VRF",
                VRFContext.RerollSeed => "Nouveau Seed",
                _ => "VRF en cours..."
            };
        }

        string GetDefaultStatusMessage(VRFState state)
        {
            return state switch
            {
                VRFState.WaitingWallet => "En attente du wallet...",
                VRFState.SigningTransaction => "Signez la transaction...",
                VRFState.TransactionPending => "Transaction en cours...",
                VRFState.WaitingSettlement => "Attente VRF settlement...",
                VRFState.ResolvingRandomness => "Résolution randomness...",
                VRFState.Settling => "Finalisation...",
                VRFState.Complete => "Terminé !",
                VRFState.Error => "Erreur",
                _ => ""
            };
        }

        string GetCompletionMessage(VRFContext context)
        {
            return context switch
            {
                VRFContext.RunSeed => "Seed généré !",
                VRFContext.BossLoot => "Loot obtenu !",
                VRFContext.RerollSeed => "Nouveau seed !",
                _ => "Terminé !"
            };
        }

        string TruncateMessage(string msg, int maxLength)
        {
            if (string.IsNullOrEmpty(msg))
                return "";
            return msg.Length <= maxLength ? msg : msg.Substring(0, maxLength) + "...";
        }

        System.Collections.IEnumerator FadeIn()
        {
            if (overlayRoot != null)
                overlayRoot.SetActive(true);

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

            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }

        System.Collections.IEnumerator AutoHideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(autoHideDelay);
            Hide();
        }
    }
}
