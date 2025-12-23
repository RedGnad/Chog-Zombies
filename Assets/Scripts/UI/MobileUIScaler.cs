using UnityEngine;
using UnityEngine.UI;

namespace ChogZombies.UI
{
    /// <summary>
    /// Utilitaire pour adapter l'UI aux écrans mobiles.
    /// Configure le CanvasScaler pour une mise à l'échelle responsive.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class MobileUIScaler : MonoBehaviour
    {
        [Header("Reference Resolution")]
        [SerializeField] Vector2 referenceResolution = new Vector2(1080, 1920);

        [Header("Scaling")]
        [SerializeField] float matchWidthOrHeight = 0.5f;
        [SerializeField] bool autoDetectOrientation = true;

        [Header("Mobile Adjustments")]
        [SerializeField] float mobileScaleMultiplier = 1f;
        [SerializeField] float minButtonSize = 48f; // dp recommandé par Google

        CanvasScaler _scaler;
        ScreenOrientation _lastOrientation;

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            ApplySettings();
        }

        void Start()
        {
            ApplySettings();
        }

        void Update()
        {
            if (autoDetectOrientation && Screen.orientation != _lastOrientation)
            {
                _lastOrientation = Screen.orientation;
                ApplySettings();
            }
        }

        void ApplySettings()
        {
            if (_scaler == null)
                return;

            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = referenceResolution;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Ajuster le match selon l'orientation
            if (autoDetectOrientation)
            {
                bool isLandscape = Screen.width > Screen.height;
                _scaler.matchWidthOrHeight = isLandscape ? 1f : matchWidthOrHeight;
            }
            else
            {
                _scaler.matchWidthOrHeight = matchWidthOrHeight;
            }

            _lastOrientation = Screen.orientation;
        }

        /// <summary>
        /// Retourne la taille minimum recommandée pour un bouton tactile en pixels.
        /// </summary>
        public float GetMinTouchTargetSize()
        {
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            return minButtonSize * (dpi / 160f);
        }

        /// <summary>
        /// Vérifie si un RectTransform a une taille suffisante pour le tactile.
        /// </summary>
        public bool IsTouchTargetSufficient(RectTransform rect)
        {
            if (rect == null)
                return false;

            float minSize = GetMinTouchTargetSize();
            return rect.rect.width >= minSize && rect.rect.height >= minSize;
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Settings")]
        void EditorApplySettings()
        {
            _scaler = GetComponent<CanvasScaler>();
            ApplySettings();
        }

        [ContextMenu("Log Screen Info")]
        void EditorLogScreenInfo()
        {
            Debug.Log($"Screen: {Screen.width}x{Screen.height} DPI={Screen.dpi} Orientation={Screen.orientation}");
            Debug.Log($"SafeArea: {Screen.safeArea}");
            Debug.Log($"Min touch target: {GetMinTouchTargetSize()}px");
        }
#endif
    }
}
