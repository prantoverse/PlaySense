using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaySense.Data.Models
{
    /// <summary>A gameplay event logged with PlaySenseEvents.Log.</summary>
    [Serializable]
    public class GameEventData
    {
        public string Category = string.Empty;

        public string Action = string.Empty;

        public string Target = string.Empty;

        /// <summary>Seconds since the session started, on the PlaySense session clock.</summary>
        public float Timestamp;

        public Vector3 Position;

        /// <summary>False when the event was logged without a position.</summary>
        public bool HasPosition;

        public List<EventParameter> Metadata = new List<EventParameter>();

        public string GetMetadata(string key, string fallback = "")
        {
            if (Metadata == null)
                return fallback;

            for (int i = 0; i < Metadata.Count; i++)
            {
                if (Metadata[i] != null && Metadata[i].Key == key)
                    return Metadata[i].Value;
            }

            return fallback;
        }

        public override string ToString()
        {
            string category = string.IsNullOrEmpty(Category) ? "Event" : Category;
            string action = string.IsNullOrEmpty(Action) ? "Unknown action" : Action;
            string target = string.IsNullOrEmpty(Target) ? string.Empty : " - " + Target;
            return category + ": " + action + target;
        }
    }
}
