using System;
using System.Collections.Generic;

namespace PlaySense.Data.Models
{
    [Serializable]
    public class SessionData
    {
        // =====================================================
        // SESSION
        // =====================================================

        public string SessionId;

        public string SceneName;

        public float Duration;

        // =====================================================
        // MOVEMENT DATA
        // =====================================================

        public List<FrameData> Frames =
            new List<FrameData>();

        // =====================================================
        // GAME EVENTS
        // =====================================================

        public List<GameEventData> Events =
            new List<GameEventData>();

        // =====================================================
        // BEHAVIOURAL OBSERVATIONS
        // =====================================================

        public List<BehaviourObservation>
            BehaviourObservations =
            new List<BehaviourObservation>();

        // =====================================================
        // ANALYTICS
        // =====================================================

        public SessionMetrics Metrics;
    }
}