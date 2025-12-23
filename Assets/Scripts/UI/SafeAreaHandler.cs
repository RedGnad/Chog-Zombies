using UnityEngine;

namespace ChogZombies.UI
{
    /// <summary>
    /// Ajuste un RectTransform pour respecter la Safe Area de l'écran (encoches iPhone, etc.)
    /// Attacher sur le panel racine de l'UI qui doit respecter la safe area.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] bool applyOnStart = true;
        [SerializeField] bool updateEveryFrame = false;
        [SerializeField] bool applyTop = true;
        [SerializeField] bool applyBottom = true;
        [SerializeField] bool applyLeft = true;
        [SerializeField] bool applyRight = true;

        RectTransform _rectTransform;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;
        ScreenOrientation _lastOrientation;

        void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        void Start()
        {
            if (applyOnStart)
                ApplySafeArea();
        }

        void Update()
        {
            if (updateEveryFrame)
            {
                if (HasSafeAreaChanged())
                    ApplySafeArea();
            }
        }

        bool HasSafeAreaChanged()
        {
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            var orientation = Screen.orientation;

            if (safeArea != _lastSafeArea || screenSize != _lastScreenSize || orientation != _lastOrientation)
            {
                _lastSafeArea = safeArea;
                _lastScreenSize = screenSize;
                _lastOrientation = orientation;
                return true;
            }
            return false;
        }

        public void ApplySafeArea()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            var safeArea = Screen.safeArea;
            var screenWidth = Screen.width;
            var screenHeight = Screen.height;

            if (screenWidth <= 0 || screenHeight <= 0)
                return;

            // Convertir safe area en anchors (0-1)
            float left = applyLeft ? safeArea.xMin / screenWidth : 0f;
            float right = applyRight ? safeArea.xMax / screenWidth : 1f;
            float bottom = applyBottom ? safeArea.yMin / screenHeight : 0f;
            float top = applyTop ? safeArea.yMax / screenHeight : 1f;

            _rectTransform.anchorMin = new Vector2(left, bottom);
            _rectTransform.anchorMax = new Vector2(right, top);

            // Reset offsets
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(screenWidth, screenHeight);
            _lastOrientation = Screen.orientation;

            Debug.Log($"[SafeArea] Applied: left={left:F3} right={right:F3} bottom={bottom:F3} top={top:F3}");
        }

        /// <summary>
        /// Force le recalcul de la safe area.
        /// </summary>
        public void Refresh()
        {
            ApplySafeArea();
        }

#if UNITY_EDITOR
        [ContextMenu("Apply Safe Area (Editor)")]
        void EditorApplySafeArea()
        {
            ApplySafeArea();
        }

        [ContextMenu("Reset to Full Screen")]
        void EditorResetFullScreen()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            _rectTransform.anchorMin = Vector2.zero;
            _rectTransform.anchorMax = Vector2.one;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
#endif
    }
}
