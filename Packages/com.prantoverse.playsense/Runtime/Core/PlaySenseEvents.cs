using System.Collections.Generic;
using UnityEngine;
using PlaySense.Core;
using PlaySense.Data.Models;

namespace PlaySense
{
    /// <summary>
    /// Log gameplay events from anywhere in your game:
    /// <code>
    /// PlaySenseEvents.Log("Puzzle", "Solved", "Lever Door");
    /// PlaySenseEvents.Log("Combat", "Died", gameObject, "cause", "fall", "checkpoint", "3");
    /// PlaySenseEvents.Log("Combat", "Kill", enemy, "method", "ability", "ability", "Shockwave");
    /// </code>
    /// Calls are ignored (at no cost beyond the call) when nothing is recording,
    /// so they can stay in shipping code.
    /// </summary>
    public static class PlaySenseEvents
    {
        /// <summary>True while a PlaySense session is recording.</summary>
        public static bool IsRecording
        {
            get
            {
                PlaySenseManager manager = PlaySenseManager.Instance;
                return manager != null && manager.IsRecording;
            }
        }

        /// <summary>Seconds since the session started (0 when not recording).</summary>
        public static float SessionTime
        {
            get
            {
                PlaySenseManager manager = PlaySenseManager.Instance;
                return manager != null ? manager.SessionTime : 0f;
            }
        }

        /// <summary>Logs an event with no position.</summary>
        public static void Log(string category, string action, string target = null)
        {
            Record(category, action, target, null, null);
        }

        /// <summary>Logs an event at a world position.</summary>
        public static void Log(string category, string action, string target, Vector3 position)
        {
            Record(category, action, target, position, null);
        }

        /// <summary>
        /// Logs an event about a GameObject (its name and position are used).
        /// Optional metadata is given as alternating key, value strings.
        /// </summary>
        public static void Log(string category, string action, GameObject target, params string[] keyValuePairs)
        {
            if (!IsRecording)
                return;

            string targetName = target != null ? target.name : null;
            Vector3? position = target != null ? target.transform.position : (Vector3?)null;

            Record(category, action, targetName, position, ToParameters(keyValuePairs));
        }

        /// <summary>Logs an event with metadata given as alternating key, value strings.</summary>
        public static void LogWithData(string category, string action, string target, params string[] keyValuePairs)
        {
            if (!IsRecording)
                return;

            Record(category, action, target, null, ToParameters(keyValuePairs));
        }

        private static void Record(
            string category,
            string action,
            string target,
            Vector3? position,
            IList<EventParameter> metadata)
        {
            PlaySenseManager manager = PlaySenseManager.Instance;
            PlaySenseSession session = manager != null ? manager.Session : null;
            if (session == null)
                return;

            session.RecordEvent(category, action, target, position, metadata);
        }

        private static List<EventParameter> ToParameters(string[] keyValuePairs)
        {
            if (keyValuePairs == null || keyValuePairs.Length == 0)
                return null;

            if (keyValuePairs.Length % 2 != 0)
                Debug.LogWarning("[PlaySense] Event metadata needs key, value pairs. The last key has no value.");

            List<EventParameter> parameters = new List<EventParameter>(keyValuePairs.Length / 2);
            for (int i = 0; i + 1 < keyValuePairs.Length; i += 2)
                parameters.Add(new EventParameter(keyValuePairs[i], keyValuePairs[i + 1]));

            return parameters;
        }
    }
}
