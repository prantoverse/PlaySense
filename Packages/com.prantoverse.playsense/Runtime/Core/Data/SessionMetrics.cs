using System;

namespace PlaySense.Data.Models
{
    /// <summary>Headline numbers for a session, computed when recording stops.</summary>
    [Serializable]
    public class SessionMetrics
    {
        public int FrameCount;

        public float Duration;

        /// <summary>Distance travelled by the primary (first recorded) agent, teleports excluded.</summary>
        public float TotalDistance;

        /// <summary>Primary agent's distance divided by the time it was recorded.</summary>
        public float AverageSpeed;

        public int AgentCount;

        public int EventCount;

        public int AttentionCount;
    }
}
