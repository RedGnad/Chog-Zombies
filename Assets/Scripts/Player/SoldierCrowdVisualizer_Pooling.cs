using UnityEngine;
using ChogZombies.Player;
using System.Collections.Generic;

namespace ChogZombies.Player
{
    public class SoldierCrowdVisualizer_Pooling : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerCombatController player;
        [SerializeField] GameObject soldierPrefab;
        
        [Header("Pooling Settings")]
        [SerializeField] int poolSize = 10; // Pool fixe, pas d'instanciation runtime
        [SerializeField] float radius = 1.5f;
        [SerializeField] float heightOffset = 0.5f;
        
        [Header("Visual Settings")]
        [SerializeField] float soldierScaleMultiplier = 1.5f;

        [Header("Visual Progression")]
        [SerializeField] int extraPowerCostStart = 2;
        [SerializeField] int extraPowerCostIncrease = 2;

        [Header("Animation")]
        [SerializeField] string movementBoolParameter = "IsMoving";
        [SerializeField] float movementMinSpeed = 0.05f;
        
        Queue<GameObject> _pool = new Queue<GameObject>();
        List<GameObject> _activeSoldiers = new List<GameObject>();
        int _lastSoldierCount = -1;
        Vector3 _lastRootPosition;
        bool _hasLastRootPosition;

        void Start()
        {
            if (player == null)
            {
                player = FindObjectOfType<PlayerCombatController>();
            }

            if (soldierPrefab == null)
            {
                Debug.LogError("SoldierCrowdVisualizer: SoldierPrefab non assigné !");
                enabled = false;
                return;
            }

            InitializePool();
        }

        void InitializePool()
        {
            // Clamp de sécurité pour éviter des valeurs extrêmes par erreur dans l'inspecteur
            poolSize = Mathf.Clamp(poolSize, 0, 100);

            // Créer le pool au démarrage uniquement
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(soldierPrefab, transform);
                MakeInstanceVisualOnly(go);
                // Ajuster la taille des soldats supplémentaires pour éviter qu'ils soient trop petits
                if (soldierScaleMultiplier > 0f)
                {
                    go.transform.localScale *= soldierScaleMultiplier;
                }
                go.SetActive(false);
                _pool.Enqueue(go);
            }
        }

        void MakeInstanceVisualOnly(GameObject go)
        {
            if (go == null)
                return;

            // Empêcher les interactions physiques / triggers
            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }

            var rigidbodies = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                rigidbodies[i].useGravity = false;
                rigidbodies[i].isKinematic = true;
            }

            // IMPORTANT: les clones sont purement visuels.
            // On désactive donc tous les scripts (même ceux provenant d'assets),
            // pour éviter tirs multiples, logique, triggers, etc.
            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null)
                    continue;

                if (b == this)
                    continue;

                b.enabled = false;
            }
        }

        void Update()
        {
            if (player == null)
                return;

            int currentCount = player.SoldierCount;
            if (currentCount != _lastSoldierCount)
            {
                UpdateVisualSoldiers(currentCount);
                _lastSoldierCount = currentCount;
            }

            if (transform.hasChanged)
            {
                UpdateSoldierPositions();
                transform.hasChanged = false;
            }

            UpdateMovementAnimations();
        }

        void UpdateVisualSoldiers(int count)
        {
            // On représente uniquement les soldats supplémentaires autour du soldat principal,
            // mais avec un coût croissant en puissance pour éviter d'afficher 10 soldats trop vite.
            int targetCount = Mathf.Min(ComputeVisibleExtraSoldiers(count), poolSize);

            // Retourner les soldats en trop au pool
            while (_activeSoldiers.Count > targetCount)
            {
                var soldier = _activeSoldiers[_activeSoldiers.Count - 1];
                soldier.SetActive(false);
                _pool.Enqueue(soldier);
                _activeSoldiers.RemoveAt(_activeSoldiers.Count - 1);
            }

            // Prendre des soldats du pool si nécessaire
            while (_activeSoldiers.Count < targetCount && _pool.Count > 0)
            {
                var soldier = _pool.Dequeue();
                soldier.SetActive(true);
                _activeSoldiers.Add(soldier);
            }

            UpdateSoldierPositions();
        }

        int ComputeVisibleExtraSoldiers(int power)
        {
            int p = Mathf.Max(power, 1);
            int remaining = Mathf.Max(p - 1, 0);

            int cost = Mathf.Max(1, extraPowerCostStart);
            int inc = Mathf.Max(0, extraPowerCostIncrease);

            int extra = 0;
            while (extra < poolSize && remaining >= cost)
            {
                remaining -= cost;
                extra++;
                cost += inc;
            }

            return extra;
        }

        void UpdateSoldierPositions()
        {
            int activeCount = _activeSoldiers.Count;
            if (activeCount == 0)
                return;

            float angleStep = 360f / activeCount;
            for (int i = 0; i < activeCount; i++)
            {
                if (_activeSoldiers[i] != null)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 localPos = new Vector3(
                        Mathf.Cos(angle) * radius,
                        heightOffset,
                        Mathf.Sin(angle) * radius
                    );
                    _activeSoldiers[i].transform.localPosition = localPos;
                    _activeSoldiers[i].transform.rotation = Quaternion.identity;
                }
            }
        }

        void UpdateMovementAnimations()
        {
            float speed = 0f;
            float dt = Time.deltaTime;
            if (_hasLastRootPosition && dt > 0f)
            {
                speed = (transform.position - _lastRootPosition).magnitude / dt;
            }

            _lastRootPosition = transform.position;
            _hasLastRootPosition = true;

            bool isMoving = speed > Mathf.Max(0f, movementMinSpeed);

            if (string.IsNullOrEmpty(movementBoolParameter))
                return;

            int count = _activeSoldiers.Count;
            for (int i = 0; i < count; i++)
            {
                var soldier = _activeSoldiers[i];
                if (soldier == null)
                    continue;

                var animator = soldier.GetComponentInChildren<Animator>();
                if (animator == null)
                    continue;

                animator.SetBool(movementBoolParameter, isMoving);
            }
        }

        // Pas de nettoyage manuel nécessaire : les soldats sont enfants de ce GameObject
        // et seront détruits automatiquement avec la scène / le parent.
    }
}
