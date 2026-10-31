using System;
using UnityEngine;

namespace PlaySense.Data.Models
{
    /// <summary>
    /// A completed period of visual attention on one target.
    /// </summary>
    [Serializable]
    public class BehaviourObservation
    {
        public const string AttentionType = "ObjectAttended";

        /// <summary>When attention started (seconds, PlaySense session clock).</summary>
        public float Timestamp;

        /// <summary>How long attention lasted (seconds).</summary>
        public float Duration;

        public string Type = AttentionType;

        /// <summary>Display name of the target.</summary>
        public string Subject = string.Empty;

        /// <summary>Stable id of the target (its hierarchy path). Unique even when names repeat.</summary>
        public string TargetId = string.Empty;

        /// <summary>Optional designer category from PlaySenseTarget (e.g. "Clue", "Door").</summary>
        public string Category = string.Empty;

        /// <summary>Agent that did the looking.</summary>
        public string AgentId = string.Empty;

        /// <summary>Name of the PlaySenseView that saw the target best.</summary>
        public string ViewName = string.Empty;

        /// <summary>Distance from the view when attention started (metres).</summary>
        public float Distance;

        /// <summary>Angle from the view centre when attention started (degrees).</summary>
        public float Angle;

        /// <summary>Attention score (0-1) when attention started.</summary>
        public float AttentionScore;

        /// <summary>Target position.</summary>
        public Vector3 Position;

        /// <summary>Index into SessionData.Scenes of the scene this happened in.</summary>
        public int Scene;

        /// <summary>True when the agent had already attended to this target earlier in the session.</summary>
        public bool IsReinspection;

        // Kept for compatibility with sessions recorded before v2.
        public bool Visible = true;
        public bool Unobstructed = true;
        public bool Attended = true;

        /// <summary>Id to group by: TargetId when present, else Subject (older sessions).</summary>
        public string Key
        {
            get { return string.IsNullOrEmpty(TargetId) ? Subject : TargetId; }
        }

        public float EndTime
        {
            get { return Timestamp + Duration; }
        }
    }
}
