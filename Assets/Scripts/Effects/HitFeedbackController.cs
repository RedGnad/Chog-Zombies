using System.Collections;
using UnityEngine;

namespace ChogZombies.Effects
{
    /// <summary>
    /// Contrôleur pour les effets de feedback de hit (flash, scale pop, etc.)
    /// Attacher sur les ennemis ou le joueur.
    /// </summary>
    public class HitFeedbackController : MonoBehaviour
    {
        [Header("Flash")]
        [SerializeField] bool enableFlash = true;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float flashDuration = 0.1f;

        [Header("Scale Pop")]
        [SerializeField] bool enableScalePop = true;
        [SerializeField] float scalePop = 1.15f;
        [SerializeField] float scalePopDuration = 0.1f;

        [Header("Shake")]
        [SerializeField] bool enableShake = false;
        [SerializeField] float shakeIntensity = 0.1f;
        [SerializeField] float shakeDuration = 0.1f;

        [Header("References")]
        [SerializeField] Renderer[] targetRenderers;
        [SerializeField] Transform scaleTarget;

        MaterialPropertyBlock _propBlock;
        Vector3 _originalScale;
        Vector3 _originalPosition;
        Coroutine _flashCoroutine;
        Coroutine _scaleCoroutine;
        Coroutine _shakeCoroutine;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (scaleTarget == null)
                scaleTarget = transform;

            _originalScale = scaleTarget.localScale;
            _originalPosition = transform.localPosition;

            if (targetRenderers == null || targetRenderers.Length == 0)
                targetRenderers = GetComponentsInChildren<Renderer>();
        }

        public void TriggerHitFeedback()
        {
            if (enableFlash)
                TriggerFlash();

            if (enableScalePop)
                TriggerScalePop();

            if (enableShake)
                TriggerShake();
        }

        public void TriggerFlash()
        {
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine());
        }

        public void TriggerScalePop()
        {
            if (_scaleCoroutine != null)
                StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScalePopRoutine());
        }

        public void TriggerShake()
        {
            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeRoutine());
        }

        IEnumerator FlashRoutine()
        {
            SetRenderersColor(flashColor);
            yield return new WaitForSeconds(flashDuration);
            ClearRenderersColor();
            _flashCoroutine = null;
        }

        IEnumerator ScalePopRoutine()
        {
            float elapsed = 0f;
            float halfDuration = scalePopDuration * 0.5f;
            Vector3 popScale = _originalScale * scalePop;

            // Scale up
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                scaleTarget.localScale = Vector3.Lerp(_originalScale, popScale, t);
                yield return null;
            }

            // Scale down
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                scaleTarget.localScale = Vector3.Lerp(popScale, _originalScale, t);
                yield return null;
            }

            scaleTarget.localScale = _originalScale;
            _scaleCoroutine = null;
        }

        IEnumerator ShakeRoutine()
        {
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float x = Random.Range(-shakeIntensity, shakeIntensity);
                float y = Random.Range(-shakeIntensity, shakeIntensity);
                transform.localPosition = _originalPosition + new Vector3(x, y, 0);
                yield return null;
            }

            transform.localPosition = _originalPosition;
            _shakeCoroutine = null;
        }

        void SetRenderersColor(Color color)
        {
            if (targetRenderers == null)
                return;

            foreach (var rend in targetRenderers)
            {
                if (rend == null)
                    continue;

                rend.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorId, color);
                _propBlock.SetColor(EmissionColorId, color * 2f);
                rend.SetPropertyBlock(_propBlock);
            }
        }

        void ClearRenderersColor()
        {
            if (targetRenderers == null)
                return;

            foreach (var rend in targetRenderers)
            {
                if (rend == null)
                    continue;

                rend.SetPropertyBlock(null);
            }
        }

        void OnDisable()
        {
            // Reset state
            if (scaleTarget != null)
                scaleTarget.localScale = _originalScale;
            transform.localPosition = _originalPosition;
            ClearRenderersColor();
        }
    }
}
