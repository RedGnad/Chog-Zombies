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

        [Header("Visual Settings")] 
        [SerializeField] Vector3 gateSize = new Vector3(1f, 2f, 0.5f);
        [SerializeField] Vector3 enemySizeBase = new Vector3(1f, 1f, 1f);
        [SerializeField] Vector3 bossSize = new Vector3(3f, 3f, 3f);

        void Start()
        {
            BuildWithParams(levelIndex, seed);
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
                CreateEnemyGroup(segment.EnemyCount, new Vector3(0f, enemySizeBase.y * 0.5f, z + enemyOffsetZ));
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
            // On centre le texte au milieu de la porte, légèrement décollé de la face avant
            labelGo.transform.localPosition = new Vector3(0f, gateSize.y * 0.5f, gateSize.z * 0.5f + 0.01f);
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
            float scaleX = Mathf.Clamp(enemyCount / 3f, 1f, 8f);
            go.transform.localScale = new Vector3(scaleX, enemySizeBase.y, enemySizeBase.z);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.sharedMaterial);
                renderer.material.color = new Color(0.2f, 0.6f, 1f); // bleu
            }

            var enemy = go.AddComponent<Enemies.EnemyGroupBehaviour>();
            enemy.Initialize(enemyCount);

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
