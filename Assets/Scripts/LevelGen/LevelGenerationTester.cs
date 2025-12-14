using UnityEngine;

namespace ChogZombies.LevelGen
{
    public class LevelGenerationTester : MonoBehaviour
    {
        [SerializeField] int levelIndex = 1;
        [SerializeField] int seed = 12345;

        void Start()
        {
            var data = LevelGenerator.Generate(levelIndex, seed);
            LogLevel(data);
        }

        void LogLevel(LevelData data)
        {
            Debug.Log($"Generated level {data.LevelIndex} with {data.Segments.Count} segments");

            for (int i = 0; i < data.Segments.Count; i++)
            {
                var seg = data.Segments[i];
                Debug.Log($"Segment {i}: Left {seg.LeftGate.Type} {seg.LeftGate.Value}, Right {seg.RightGate.Type} {seg.RightGate.Value}, Enemies {seg.EnemyCount}");
            }

            if (data.Boss != null)
            {
                Debug.Log($"Boss: Pattern {data.Boss.Pattern}, HP {data.Boss.Hp}, Damage {data.Boss.Damage}");
            }
        }
    }
}
