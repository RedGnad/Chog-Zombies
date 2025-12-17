using UnityEngine;
using ChogZombies.CameraSystem;

namespace ChogZombies.LevelGen
{
    public class LevelRuntimeVisualizer : MonoBehaviour
    {
        [Header("Level Parameters")]
        [SerializeField] int levelIndex = 1;
        [SerializeField] int seed = 12345;

        [Header("Layout Parameters")] 
        [SerializeField] float segmentSpacing = 10f;
        [SerializeField] float gateOffsetX = 3f;
        [SerializeField] float gateHeight = 1.5f;
        [SerializeField] float enemyOffsetZ = 3f;
        [SerializeField] float enemyHeight = 1f;
        [SerializeField] float enemyLaneOffsetX = 2.0f;

        [Header("Visual Settings")] 
        [SerializeField] Vector3 gateSize = new Vector3(1f, 2f, 0.5f);
        [SerializeField] Vector3 enemySizeBase = new Vector3(1f, 1f, 1f);
        [SerializeField] Vector3 bossSize = new Vector3(3f, 3f, 3f);

        [Header("Default Materials (no prefab)")]
        [SerializeField] Material gateBaseMaterial;
        [SerializeField] Material enemyBaseMaterial;
        [SerializeField] Material bossBaseMaterial;

        [Header("Prefabs")]
        [SerializeField] GameObject gatePrefab;
        [SerializeField] GameObject enemyGroupPrefab;
        [SerializeField] GameObject bossPrefab;
        [SerializeField] GameObject segmentEnvironmentPrefab;

        [Header("Visual Prefabs (Child)")]
        [SerializeField] GameObject gateVisualPrefab;
        [SerializeField] GameObject enemyVisualPrefab;
        [SerializeField] GameObject bossVisualPrefab;

        [Header("Prefab Tinting")]
        [SerializeField] bool tintGatePrefab = true;
        [SerializeField] bool tintEnemyPrefab = false;
        [SerializeField] bool tintBossPrefab = true;

        Transform _gateLabelsRoot;

        static Shader GetRuntimeColorShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
                return shader;

            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader != null)
                return shader;

            shader = Shader.Find("Standard");
            return shader;
        }

        static void ApplyRuntimeColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            var shader = GetRuntimeColorShader();
            if (shader == null)
                return;

            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            renderer.material = mat;
        }

        static void ApplyTint(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        static void ApplyTintToHierarchy(GameObject go, Color color)
        {
            if (go == null)
                return;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                ApplyTint(renderers[i], color);
            }
        }

        static void StripSceneControlComponents(GameObject go)
        {
            if (go == null)
                return;

            var follows = go.GetComponentsInChildren<CameraFollow>(true);
            for (int i = 0; i < follows.Length; i++)
                Destroy(follows[i]);

            var cameras = go.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
                Destroy(cameras[i]);

            var listeners = go.GetComponentsInChildren<AudioListener>(true);
            for (int i = 0; i < listeners.Length; i++)
                Destroy(listeners[i]);

            var lights = go.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
                Destroy(lights[i]);
        }

        static void DisablePhysicsComponents(GameObject go)
        {
            if (go == null)
                return;

            var colliders = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;

            var rbs = go.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
                Destroy(rbs[i]);
        }

        static Bounds ComputeHierarchyBoundsInLocalSpace(Transform root, Renderer[] renderers)
        {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool hasAny = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null)
                    continue;

                var wb = r.bounds;
                var min = wb.min;
                var max = wb.max;

                Vector3[] corners =
                {
                    new Vector3(min.x, min.y, min.z),
                    new Vector3(min.x, min.y, max.z),
                    new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, max.y, max.z),
                    new Vector3(max.x, min.y, min.z),
                    new Vector3(max.x, min.y, max.z),
                    new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, max.y, max.z),
                };

                for (int c = 0; c < corners.Length; c++)
                {
                    var p = root.InverseTransformPoint(corners[c]);
                    if (!hasAny)
                    {
                        b = new Bounds(p, Vector3.zero);
                        hasAny = true;
                    }
                    else
                    {
                        b.Encapsulate(p);
                    }
                }
            }

            return b;
        }

        static void FitVisualToUnitBox(Transform visual)
        {
            if (visual == null)
                return;

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return;

            Physics.SyncTransforms();
            var localBounds = ComputeHierarchyBoundsInLocalSpace(visual, renderers);
            var size = localBounds.size;

            float sx = size.x > 0.0001f ? 1f / size.x : 1f;
            float sy = size.y > 0.0001f ? 1f / size.y : 1f;
            float sz = size.z > 0.0001f ? 1f / size.z : 1f;
            float s = Mathf.Min(sx, sy, sz);

            visual.localScale = Vector3.one * s;
            Physics.SyncTransforms();

            localBounds = ComputeHierarchyBoundsInLocalSpace(visual, renderers);
            visual.localPosition = -localBounds.center;
        }

        GameObject AttachVisualChild(GameObject root, GameObject visualPrefab)
        {
            if (root == null || visualPrefab == null)
                return null;

            var v = Instantiate(visualPrefab, root.transform, false);
            v.name = "Visual";

            StripSceneControlComponents(v);
            DisablePhysicsComponents(v);

            v.transform.localPosition = Vector3.zero;
            v.transform.localRotation = Quaternion.identity;
            v.transform.localScale = Vector3.one;

            FitVisualToUnitBox(v.transform);
            return v;
        }

        Transform GetGateLabelsRoot()
        {
            if (_gateLabelsRoot != null)
                return _gateLabelsRoot;

            var existing = GameObject.Find("GateLabelsRoot");
            if (existing != null)
            {
                _gateLabelsRoot = existing.transform;
                return _gateLabelsRoot;
            }

            var go = new GameObject("GateLabelsRoot");
            _gateLabelsRoot = go.transform;
            return _gateLabelsRoot;
        }

        void ClearGateLabels()
        {
            var root = GetGateLabelsRoot();
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (child == null)
                    continue;

                if (child.name.StartsWith("GateLabel_"))
                    Destroy(child.gameObject);
            }
        }

        void Start()
        {
            var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
            if (run != null)
                return;

            BuildWithParams(levelIndex, seed);
        }

        void OnValidate()
        {
            gateHeight = Mathf.Max(0.01f, gateHeight);
            enemyHeight = Mathf.Max(0.01f, enemyHeight);

            gateSize.y = gateHeight;
            enemySizeBase.y = enemyHeight;

            if (Application.isPlaying && isActiveAndEnabled)
            {
                var run = FindObjectOfType<ChogZombies.Game.RunGameController>();
                if (run == null)
                    BuildWithParams(levelIndex, seed);
            }
        }

        int GetEnemyGroupsInRow(int levelIndex, int segmentIndex, int segmentCount)
        {
            int l = Mathf.Max(1, levelIndex);
            if (segmentCount <= 1)
                return 1;

            float progress = segmentIndex / (float)(segmentCount - 1);

            // Plus on est loin dans le niveau et plus le niveau est élevé, plus on densifie.
            int groups = 1;
            if (l >= 7 && progress > 0.45f)
                groups = 2;
            if (l >= 12 && progress > 0.7f)
                groups = 3;

            return Mathf.Clamp(groups, 1, 3);
        }

        void SpawnEnemyRow(int groupsInRow, int enemyCount, float z)
        {
            groupsInRow = Mathf.Clamp(groupsInRow, 1, 3);

            if (groupsInRow == 1)
            {
                CreateEnemyGroup(enemyCount, new Vector3(0f, enemySizeBase.y * 0.5f, z));
                return;
            }

            if (groupsInRow == 2)
            {
                CreateEnemyGroup(enemyCount, new Vector3(-enemyLaneOffsetX, enemySizeBase.y * 0.5f, z));
                CreateEnemyGroup(enemyCount, new Vector3(enemyLaneOffsetX, enemySizeBase.y * 0.5f, z));
                return;
            }

            CreateEnemyGroup(enemyCount, new Vector3(-enemyLaneOffsetX, enemySizeBase.y * 0.5f, z));
            CreateEnemyGroup(enemyCount, new Vector3(0f, enemySizeBase.y * 0.5f, z));
            CreateEnemyGroup(enemyCount, new Vector3(enemyLaneOffsetX, enemySizeBase.y * 0.5f, z));
        }

        public void BuildWithParams(int newLevelIndex, int newSeed)
        {
            levelIndex = newLevelIndex;
            seed = newSeed;

            var levelData = LevelGenerator.Generate(levelIndex, seed);
            BuildLevel(levelData);
        }

        void BuildLevel(LevelData level)
        {
            ClearGateLabels();

            // Nettoyer les anciens enfants si on relance en Play dans l'éditeur
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null)
                    continue;

                // Ne détruire que ce que ce visualizer a généré (évite de casser une Camera si elle est sous ce parent).
                if (child.name.StartsWith("Gate_") ||
                    child.name.StartsWith("Enemies_") ||
                    child.name.StartsWith("Boss_") ||
                    child.name.StartsWith("SegmentEnv_"))
                {
                    Destroy(child.gameObject);
                }
            }

            for (int i = 0; i < level.Segments.Count; i++)
            {
                float z = i * segmentSpacing;
                var segment = level.Segments[i];

                if (segmentEnvironmentPrefab != null)
                {
                    var env = Instantiate(segmentEnvironmentPrefab, transform);
                    env.name = $"SegmentEnv_{i}";
                    env.transform.localPosition = new Vector3(0f, 0f, z);

                    StripSceneControlComponents(env);
                }

                // Portes
                CreateGate(segment.LeftGate, new Vector3(-gateOffsetX, gateSize.y * 0.5f, z));
                CreateGate(segment.RightGate, new Vector3(gateOffsetX, gateSize.y * 0.5f, z));

                // Représentation simple des ennemis: un cube dont la taille X reflète enemyCount
                int groupsInRow = GetEnemyGroupsInRow(level.LevelIndex, i, level.Segments.Count);
                SpawnEnemyRow(groupsInRow, segment.EnemyCount, z + enemyOffsetZ);

                // À partir du niveau 11, on ajoute en plus un groupe "chaser" qui suit le joueur sur Z,
                // mais uniquement sur certains segments pour éviter d'en avoir trop.
                if (level.LevelIndex >= 11 && (i % 3) == 1)
                {
                    int chaserCount = Mathf.Max(1, segment.EnemyCount / 2);
                    float chaserZ = z + enemyOffsetZ * 1.5f;
                    CreateChaserEnemyGroup(chaserCount, new Vector3(0f, enemySizeBase.y * 0.5f, chaserZ));
                }
            }

            // Boss à la fin du couloir
            float bossZ = level.Segments.Count * segmentSpacing + enemyOffsetZ;
            CreateBoss(level.Boss, new Vector3(0f, bossSize.y * 0.5f, bossZ));
        }

        void CreateGate(GateData gate, Vector3 position)
        {
            GameObject go;
            if (gateVisualPrefab != null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }
            else if (gatePrefab != null)
            {
                go = Instantiate(gatePrefab, transform, false);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }

            StripSceneControlComponents(go);

            go.name = $"Gate_{gate.Type}_{gate.Value}";
            go.transform.localPosition = position;

            // Toujours utiliser gateSize pour la taille de la porte, même avec un prefab,
            // afin de conserver la même échelle globale.
            go.transform.localScale = Vector3.one;
            go.transform.localScale = gateSize;

            var renderer = go.GetComponent<Renderer>();
            var gateColor = GetGateColor(gate.Type);
            if (gateVisualPrefab != null)
            {
                if (renderer != null)
                    renderer.enabled = false;

                var visualGo = AttachVisualChild(go, gateVisualPrefab);
                if (visualGo != null && tintGatePrefab)
                    ApplyTintToHierarchy(visualGo, gateColor);
            }
            else if (renderer != null && gatePrefab == null)
            {
                if (gateBaseMaterial != null)
                    renderer.sharedMaterial = gateBaseMaterial;
                else
                    ApplyRuntimeColor(renderer, gateColor); // Fallback si aucun material assigné

                ApplyTint(renderer, gateColor);
            }
            else if (tintGatePrefab)
            {
                ApplyTintToHierarchy(go, gateColor);
            }

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;

            // Label textuel pour expliquer l'effet de la porte.
            // IMPORTANT: le label ne doit pas être enfant de la porte, sinon il hérite de la scale (x100 => texte illisible).
            var labelGo = new GameObject($"GateLabel_{gate.Type}_{gate.Value}");
            labelGo.transform.SetParent(GetGateLabelsRoot(), true);

            // Positionner le label sur la face avant de la porte (côté caméra) et à mi-hauteur.
            // bounds est en world-space, donc indépendant de la localScale.
            Physics.SyncTransforms();
            var b = col.bounds;
            labelGo.transform.position = new Vector3(b.center.x, b.center.y, b.min.z - 0.01f);
            labelGo.transform.rotation = Quaternion.identity; // caméra regarde +Z

            // Root non-scalé => le label ne grossit pas quand la porte est x100.
            labelGo.transform.localScale = Vector3.one;

            var text = labelGo.AddComponent<TextMesh>();
            text.text = GetGateLabel(gate);
            text.fontSize = 64;
            text.characterSize = 0.07f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;

            var gateBehaviour = go.GetComponent<GateBehaviour>();
            if (gateBehaviour == null)
            {
                gateBehaviour = go.AddComponent<GateBehaviour>();
            }
            gateBehaviour.Initialize(gate);
        }

        void CreateEnemyGroup(int enemyCount, Vector3 position)
        {
            GameObject go;
            if (enemyVisualPrefab != null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }
            else if (enemyGroupPrefab != null)
            {
                go = Instantiate(enemyGroupPrefab, transform, false);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }

            StripSceneControlComponents(go);
            go.name = $"Enemies_{enemyCount}";
            go.transform.localPosition = position;

            // Largeur dépendante du nombre d'ennemis, comme avant, même avec prefab.
            go.transform.localScale = Vector3.one;
            float scaleX = Mathf.Clamp(1.2f + enemyCount * 0.22f, 1.3f, 4.2f);
            go.transform.localScale = new Vector3(scaleX, enemySizeBase.y, enemySizeBase.z);

            Color enemyColor = new Color(0.2f, 0.6f, 1f);
            if (enemyVisualPrefab != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;

                var visualGo = AttachVisualChild(go, enemyVisualPrefab);
                if (visualGo != null && tintEnemyPrefab)
                    ApplyTintToHierarchy(visualGo, enemyColor);
            }
            else if (enemyGroupPrefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (enemyBaseMaterial != null)
                        renderer.sharedMaterial = enemyBaseMaterial;
                    else
                        ApplyRuntimeColor(renderer, enemyColor);

                    ApplyTint(renderer, enemyColor);
                }
            }
            else if (tintEnemyPrefab)
            {
                ApplyTintToHierarchy(go, enemyColor);
            }

            var enemy = go.GetComponent<Enemies.EnemyGroupBehaviour>();
            if (enemy == null)
            {
                enemy = go.AddComponent<Enemies.EnemyGroupBehaviour>();
            }
            enemy.Initialize(enemyCount, levelIndex);

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
            }
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        void CreateChaserEnemyGroup(int enemyCount, Vector3 position)
        {
            GameObject go;
            if (enemyVisualPrefab != null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }
            else if (enemyGroupPrefab != null)
            {
                go = Instantiate(enemyGroupPrefab, transform, false);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }

            StripSceneControlComponents(go);
            go.name = $"EnemiesChaser_{enemyCount}";
            go.transform.localPosition = position;

            // Même logique de largeur que les groupes normaux.
            go.transform.localScale = Vector3.one;
            float scaleX = Mathf.Clamp(1.2f + enemyCount * 0.22f, 1.3f, 4.2f);
            go.transform.localScale = new Vector3(scaleX, enemySizeBase.y, enemySizeBase.z);

            Color chaserColor = new Color(1.0f, 0.55f, 0.2f);
            if (enemyVisualPrefab != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;

                var visualGo = AttachVisualChild(go, enemyVisualPrefab);
                if (visualGo != null && tintEnemyPrefab)
                    ApplyTintToHierarchy(visualGo, chaserColor);
            }
            else if (enemyGroupPrefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (enemyBaseMaterial != null)
                        renderer.sharedMaterial = enemyBaseMaterial;
                    else
                        ApplyRuntimeColor(renderer, chaserColor);

                    ApplyTint(renderer, chaserColor);
                }
            }
            else if (tintEnemyPrefab)
            {
                ApplyTintToHierarchy(go, chaserColor);
            }

            var chaser = go.GetComponent<Enemies.EnemyChaserGroupBehaviour>();
            if (chaser == null)
            {
                chaser = go.AddComponent<Enemies.EnemyChaserGroupBehaviour>();
            }
            chaser.Initialize(enemyCount, levelIndex);

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
            }
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        void CreateBoss(BossData boss, Vector3 position)
        {
            GameObject go;
            if (bossVisualPrefab != null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }
            else if (bossPrefab != null)
            {
                go = Instantiate(bossPrefab, transform, false);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
            }

            StripSceneControlComponents(go);
            go.name = $"Boss_{boss.Pattern}_HP{boss.Hp}_Dmg{boss.Damage}";
            go.transform.localPosition = position;

            // Boss: conserver la même taille logique même avec un prefab.
            go.transform.localScale = Vector3.one;
            var size = bossSize;
            size.x = Mathf.Max(size.x, 10f);
            size.y = Mathf.Max(size.y, 3f);
            size.z = Mathf.Max(size.z, 3f);
            go.transform.localScale = size;

            var bossColor = GetBossColor(boss.Pattern);
            if (bossVisualPrefab != null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.enabled = false;

                var visualGo = AttachVisualChild(go, bossVisualPrefab);
                if (visualGo != null && tintBossPrefab)
                    ApplyTintToHierarchy(visualGo, bossColor);
            }
            else if (bossPrefab == null)
            {
                var renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (bossBaseMaterial != null)
                        renderer.sharedMaterial = bossBaseMaterial;
                    else
                        ApplyRuntimeColor(renderer, bossColor);

                    ApplyTint(renderer, bossColor);
                }
            }
            else if (tintBossPrefab)
            {
                ApplyTintToHierarchy(go, bossColor);
            }

            var bossBehaviour = go.GetComponent<Enemies.BossBehaviour>();
            if (bossBehaviour == null)
            {
                bossBehaviour = go.AddComponent<Enemies.BossBehaviour>();
            }
            bossBehaviour.Initialize(boss);

            var col = go.GetComponent<Collider>();
            if (col == null)
            {
                col = go.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = go.AddComponent<Rigidbody>();
            }
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        Color GetGateColor(GateType type)
        {
            switch (type)
            {
                case GateType.Add:
                    return Color.green;
                case GateType.Subtract:
                    return Color.red;
                case GateType.Multiply2:
                    return Color.blue;
                case GateType.Multiply3:
                    return new Color(0.8f, 0f, 0.8f); // magenta
                case GateType.Multiply:
                    return new Color(0.2f, 0.7f, 1f);
                default:
                    return Color.white;
            }
        }

        string GetGateLabel(GateData gate)
        {
            switch (gate.Type)
            {
                case GateType.Add:
                    return $"+{gate.Value}";
                case GateType.Subtract:
                    return $"-{gate.Value}";
                case GateType.Multiply2:
                case GateType.Multiply3:
                    return $"x{gate.Value}";
                case GateType.Multiply:
                    float mul = gate.Value / 100f;
                    return $"x{mul:0.##}";
                default:
                    return string.Empty;
            }
        }

        Color GetBossColor(BossPatternType pattern)
        {
            switch (pattern)
            {
                case BossPatternType.A:
                    return new Color(0.6f, 1f, 0.6f);
                case BossPatternType.B:
                    return new Color(1f, 1f, 0.6f);
                case BossPatternType.C:
                    return new Color(1f, 0.6f, 0.4f);
                case BossPatternType.D:
                    return new Color(1f, 0.2f, 0.2f);
                default:
                    return Color.white;
            }
        }
    }
}
