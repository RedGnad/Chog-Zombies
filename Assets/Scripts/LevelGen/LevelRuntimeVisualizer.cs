using UnityEngine;

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

        void Start()
        {
            BuildWithParams(levelIndex, seed);
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
            // Nettoyer les anciens enfants si on relance en Play dans l'éditeur
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }

            for (int i = 0; i < level.Segments.Count; i++)
            {
                float z = i * segmentSpacing;
                var segment = level.Segments[i];

                // Portes
                CreateGate(segment.LeftGate, new Vector3(-gateOffsetX, gateSize.y * 0.5f, z));
                CreateGate(segment.RightGate, new Vector3(gateOffsetX, gateSize.y * 0.5f, z));

                // Représentation simple des ennemis: un cube dont la taille X reflète enemyCount
                int groupsInRow = GetEnemyGroupsInRow(level.LevelIndex, i, level.Segments.Count);
                SpawnEnemyRow(groupsInRow, segment.EnemyCount, z + enemyOffsetZ);
            }

            // Boss à la fin du couloir
            float bossZ = level.Segments.Count * segmentSpacing + enemyOffsetZ;
            CreateBoss(level.Boss, new Vector3(0f, bossSize.y * 0.5f, bossZ));
        }

        void CreateGate(GateData gate, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Gate_{gate.Type}_{gate.Value}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = gateSize;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.sharedMaterial);
                renderer.material.color = GetGateColor(gate.Type);
            }

            // Label textuel pour expliquer l'effet de la porte
            var labelGo = new GameObject("GateLabel");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, gateSize.z * 0.5f + 0.01f);
            labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // vers la caméra (qui regarde +Z)

            var text = labelGo.AddComponent<TextMesh>();
            text.text = GetGateLabel(gate);
            text.fontSize = 64;
            text.characterSize = 0.1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            var gateBehaviour = go.AddComponent<GateBehaviour>();
            gateBehaviour.Initialize(gate);
        }

        void CreateEnemyGroup(int enemyCount, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Enemies_{enemyCount}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;

            // On encode grossièrement le nombre d'ennemis dans la taille en X
            // Largeur plus importante pour rendre les groupes moins esquivables
            float scaleX = Mathf.Clamp(1.2f + enemyCount * 0.22f, 1.3f, 4.2f);
            go.transform.localScale = new Vector3(scaleX, enemySizeBase.y, enemySizeBase.z);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.sharedMaterial);
                renderer.material.color = new Color(0.2f, 0.6f, 1f); // bleu
            }

            var enemy = go.AddComponent<Enemies.EnemyGroupBehaviour>();
            enemy.Initialize(enemyCount, levelIndex);

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        void CreateBoss(BossData boss, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Boss_{boss.Pattern}_HP{boss.Hp}_Dmg{boss.Damage}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            var size = bossSize;
            size.x = Mathf.Max(size.x, 10f);
            size.y = Mathf.Max(size.y, 3f);
            size.z = Mathf.Max(size.z, 3f);
            go.transform.localScale = size;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.sharedMaterial);
                renderer.material.color = GetBossColor(boss.Pattern);
            }

            var bossBehaviour = go.AddComponent<Enemies.BossBehaviour>();
            bossBehaviour.Initialize(boss);

            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }

            var rb = go.AddComponent<Rigidbody>();
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
