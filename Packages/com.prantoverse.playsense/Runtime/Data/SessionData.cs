using System;
using System.Collections.Generic;

namespace PlaySense.Data.Models
{
    /// <summary>Everything recorded in one play session. Saved as JSON in a .pss file.</summary>
    [Serializable]
    public class SessionData
    {
        /// <summary>File format version. 1 = original format, 2 = this format.</summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;

        public string SessionId = string.Empty;

        public string SceneName = string.Empty;

        /// <summary>Local wall-clock start time, ISO 8601.</summary>
        public string StartedAt = string.Empty;

        public string UnityVersion = string.Empty;

        public string Platform = string.Empty;

        /// <summary>Length of the session on the PlaySense session clock (seconds).</summary>
        public float Duration;

        public List<FrameData> Frames = new List<FrameData>();

        public List<GameEventData> Events = new List<GameEventData>();

        public List<BehaviourObservation> BehaviourObservations = new List<BehaviourObservation>();

        public SessionMetrics Metrics = new SessionMetrics();

        /// <summary>Makes a loaded session safe to read (no null lists or metrics).</summary>
        public void EnsureValid()
        {
            if (Frames == null) Frames = new List<FrameData>();
            if (Events == null) Events = new List<GameEventData>();
            if (BehaviourObservations == null) BehaviourObservations = new List<BehaviourObservation>();
            if (Metrics == null) Metrics = new SessionMetrics();
            if (SessionId == null) SessionId = string.Empty;
            if (SceneName == null) SceneName = string.Empty;
        }
    }
}
