using System;

namespace PlaySense.Data.Models
{
    [Serializable]
    public class AttentionData
    {
        // =====================================================
        // TARGET
        // =====================================================

        public string Target = string.Empty;

        // =====================================================
        // ATTENTION COUNTS
        // =====================================================

        // Number of separate times the player attended
        // to this target.
        public int AttentionCount;

        // Total accumulated attention time.
        public float TotalDuration;

        // Longest continuous attention period.
        public float LongestDuration;

        // =====================================================
        // TIMING
        // =====================================================

        // First time the player attended to the target.
        public float FirstSeenTime;

        // Most recent time the player attended to the target.
        public float LastSeenTime;

        // =====================================================
        // SPATIAL DATA
        // =====================================================

        // Average distance from player while attending.
        public float AverageDistance;

        // Average viewing angle while attending.
        public float AverageAngle;

        // =====================================================
        // INTERNAL ACCUMULATORS
        // =====================================================

        [NonSerialized]
        public float DistanceAccumulator;

        [NonSerialized]
        public float AngleAccumulator;

        [NonSerialized]
        public int SampleCount;
    }
}