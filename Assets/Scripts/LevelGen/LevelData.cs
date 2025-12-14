using System;
using System.Collections.Generic;

namespace ChogZombies.LevelGen
{
    [Serializable]
    public enum GateType
    {
        Add,
        Subtract,
        Multiply2,
        Multiply3
    }

    [Serializable]
    public class GateData
    {
        public GateType Type;
        public int Value;
    }

    [Serializable]
    public class SegmentData
    {
        public GateData LeftGate;
        public GateData RightGate;
        public int EnemyCount;
    }

    public enum BossPatternType
    {
        A,
        B,
        C,
        D
    }

    [Serializable]
    public class BossData
    {
        public BossPatternType Pattern;
        public int Hp;
        public int Damage;
    }

    [Serializable]
    public class LevelData
    {
        public int LevelIndex;
        public List<SegmentData> Segments = new List<SegmentData>();
        public BossData Boss;
    }
}
