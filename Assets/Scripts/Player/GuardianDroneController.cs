using System.Collections.Generic;
using UnityEngine;
using ChogZombies.Loot;
using ChogZombies.Combat;

namespace ChogZombies.Player
{
    /// <summary>
    /// Contrôle des drones gardiens basés sur RunMetaEffects.GuardianDronePower.
    /// Ils orbitent autour du joueur et tirent périodiquement.
    /// </summary>
    public class GuardianDroneController : MonoBehaviour
    {
        [SerializeField] PlayerCombatController player;
        [SerializeField] GameObject droneVisualPrefab;
        [SerializeField] Vector3 droneScale = Vector3.one;
        [SerializeField, Min(1)] int maxDrones = 3;
        [SerializeField, Min(0.1f)] float orbitRadius = 1.8f;
        [SerializeField, Min(0f)] float orbitHeight = 1.4f;
        [SerializeField, Min(0f)] float orbitSpeed = 60f;
        [SerializeField] GameObject projectilePrefab;
        [SerializeField, Min(0.1f)] float projectileSpeed = 18f;
        [SerializeField, Min(0.1f)] float projectileDamage = 6f;
        [SerializeField, Min(0.1f)] float baseFireInterval = 1.5f;
        [SerializeField] bool debugGuardianLogs = false;

        readonly List<Transform> _droneInstances = new List<Transform>();
        float _orbitAngle;
        float _fireTimer;

        void Awake()
        {
            if (player == null)
                player = GetComponentInParent<PlayerCombatController>();

            // Si aucun prefab de projectile n'est assigné pour les drones, on réutilise
            // automatiquement celui du joueur pour garantir un visuel cohérent.
            if (projectilePrefab == null && player != null && player.ProjectilePrefab != null)
            {
                projectilePrefab = player.ProjectilePrefab;
            }

            // Si aucun tuning spécifique n'est défini, on hérite des valeurs du joueur
            // pour que les balles de drone soient identiques visuellement.
            if (player != null)
            {
                if (Mathf.Abs(projectileSpeed - 18f) < 0.01f)
                    projectileSpeed = player.ProjectileSpeed;
            }
        }

        void OnDisable()
        {
            SetDronesActive(false);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _droneInstances.Count; i++)
            {
                if (_droneInstances[i] != null)
                    Destroy(_droneInstances[i].gameObject);
            }
            _droneInstances.Clear();
        }

        void Update()
        {
            if (player == null)
                return;

            float power = Mathf.Max(0f, RunMetaEffects.GuardianDronePower);
            UpdateDrones(power);
            HandleShooting(power);
        }

        void UpdateDrones(float power)
        {
            int desired = Mathf.Clamp(Mathf.FloorToInt(power), 0, maxDrones);
            AdjustDroneCount(desired);

            if (desired <= 0)
            {
                SetDronesActive(false);
                return;
            }

            SetDronesActive(true);
            _orbitAngle += orbitSpeed * Time.deltaTime;
            float segment = 360f / _droneInstances.Count;

            for (int i = 0; i < _droneInstances.Count; i++)
            {
                var t = _droneInstances[i];
                if (t == null)
                    continue;

                float angle = _orbitAngle + segment * i;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius;
                Vector3 pos = player.transform.position + offset + Vector3.up * orbitHeight;
                t.position = pos;
                t.LookAt(player.transform.position + Vector3.up * orbitHeight);
            }
        }

        void HandleShooting(float power)
        {
            if (power <= 0f)
            {
                _fireTimer = 0f;
                return;
            }

            float interval = Mathf.Max(0.2f, baseFireInterval / Mathf.Max(1f, power));
            _fireTimer += Time.deltaTime;
            if (_fireTimer < interval)
                return;

            _fireTimer -= interval;
            for (int i = 0; i < _droneInstances.Count; i++)
            {
                var t = _droneInstances[i];
                if (t == null)
                    continue;

                // Direction principale vers l'avant du joueur pour coller au gameplay runner.
                Vector3 dir = player != null ? player.transform.forward : t.forward;

                // Réutiliser autant que possible les paramètres du joueur.
                Vector3 scale = player != null ? player.ProjectileScale : Vector3.one * 0.3f;
                float life = player != null ? player.ProjectileLifetime : 1f;

                Projectile.SpawnProjectile(t.position, dir.normalized, projectileSpeed, projectileDamage, scale, life, projectilePrefab);
            }

            if (debugGuardianLogs)
            {
                Debug.Log($"[GuardianDrone] Fired volley. power={power:F2} drones={_droneInstances.Count} speed={projectileSpeed:F1} dmg={projectileDamage:F1} prefab={(projectilePrefab != null ? projectilePrefab.name : "<null>")}");
            }
        }

        void AdjustDroneCount(int desired)
        {
            while (_droneInstances.Count < desired)
            {
                Transform t = CreateDroneInstance();
                _droneInstances.Add(t);
            }

            while (_droneInstances.Count > desired)
            {
                int last = _droneInstances.Count - 1;
                if (_droneInstances[last] != null)
                    Destroy(_droneInstances[last].gameObject);
                _droneInstances.RemoveAt(last);
            }
        }

        Transform CreateDroneInstance()
        {
            GameObject go;
            if (droneVisualPrefab != null)
            {
                go = Instantiate(droneVisualPrefab, transform);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(transform, false);
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);
                var fallbackScale = droneScale == Vector3.zero ? Vector3.one * 0.25f : droneScale;
                go.transform.localScale = fallbackScale;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = null;
            }

            go.name = "GuardianDrone";

            // Appliquer la scale configurée au prefab visuel si elle est non nulle.
            if (droneVisualPrefab != null)
            {
                if (droneScale != Vector3.zero)
                    go.transform.localScale = droneScale;
            }
            return go.transform;
        }

        void SetDronesActive(bool active)
        {
            for (int i = 0; i < _droneInstances.Count; i++)
            {
                var t = _droneInstances[i];
                if (t != null && t.gameObject.activeSelf != active)
                    t.gameObject.SetActive(active);
            }
        }
    }
}
