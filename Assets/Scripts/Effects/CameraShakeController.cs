using System.Collections;
using UnityEngine;

namespace ChogZombies.Effects
{
    /// <summary>
    /// Contrôleur de camera shake. Singleton accessible globalement.
    /// Attacher sur la caméra principale ou un parent de celle-ci.
    /// </summary>
    public class CameraShakeController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] float defaultIntensity = 0.15f;
        [SerializeField] float defaultDuration = 0.2f;
        [SerializeField] AnimationCurve shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("Limits")]
        [SerializeField] float maxIntensity = 0.5f;
        [SerializeField] float cooldownBetweenShakes = 0.05f;

        Vector3 _originalPosition;
        Coroutine _shakeCoroutine;
        float _lastShakeTime;

        public static CameraShakeController Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _originalPosition = transform.localPosition;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Déclenche un shake avec les paramètres par défaut.
        /// </summary>
        public void Shake()
        {
            Shake(defaultIntensity, defaultDuration);
        }

        /// <summary>
        /// Déclenche un shake avec intensité et durée personnalisées.
        /// </summary>
        public void Shake(float intensity, float duration)
        {
            if (Time.time - _lastShakeTime < cooldownBetweenShakes)
                return;

            _lastShakeTime = Time.time;
            intensity = Mathf.Min(intensity, maxIntensity);
            _originalPosition = transform.localPosition;

            if (_shakeCoroutine != null)
                StopCoroutine(_shakeCoroutine);

            _shakeCoroutine = StartCoroutine(ShakeRoutine(intensity, duration));
        }

        /// <summary>
        /// Shake léger pour les hits normaux.
        /// </summary>
        public void ShakeLight()
        {
            Shake(defaultIntensity * 0.5f, defaultDuration * 0.5f);
        }

        /// <summary>
        /// Shake moyen pour les hits importants.
        /// </summary>
        public void ShakeMedium()
        {
            Shake(defaultIntensity, defaultDuration);
        }

        /// <summary>
        /// Shake fort pour les boss ou événements majeurs.
        /// </summary>
        public void ShakeHeavy()
        {
            Shake(defaultIntensity * 2f, defaultDuration * 1.5f);
        }

        IEnumerator ShakeRoutine(float intensity, float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float currentIntensity = intensity * shakeCurve.Evaluate(t);

                float x = Random.Range(-currentIntensity, currentIntensity);
                float y = Random.Range(-currentIntensity, currentIntensity);

                transform.localPosition = _originalPosition + new Vector3(x, y, 0);
                yield return null;
            }

            transform.localPosition = _originalPosition;
            _shakeCoroutine = null;
        }

        /// <summary>
        /// Méthode statique pour déclencher un shake facilement.
        /// </summary>
        public static void TriggerShake(float intensity = -1f, float duration = -1f)
        {
            if (Instance == null)
                return;

            if (intensity < 0 && duration < 0)
                Instance.Shake();
            else if (intensity < 0)
                Instance.Shake(Instance.defaultIntensity, duration);
            else if (duration < 0)
                Instance.Shake(intensity, Instance.defaultDuration);
            else
                Instance.Shake(intensity, duration);
        }
    }
}
