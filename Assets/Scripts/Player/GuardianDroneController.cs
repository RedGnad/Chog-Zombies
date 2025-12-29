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
            if (power <= 0f || projectilePrefab == null)
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

                Vector3 dir = player != null ? player.transform.forward : t.forward;
                Projectile.SpawnProjectile(t.position, dir.normalized, projectileSpeed, projectileDamage, Vector3.one * 0.3f, 1f, projectilePrefab);
            }

            if (debugGuardianLogs)
            {
                Debug.Log($"[GuardianDrone] Fired volley. power={power:F2} drones={_droneInstances.Count}");
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
                go.transform.localScale = Vector3.one * 0.25f;
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = null;
            }

            go.name = "GuardianDrone";
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
