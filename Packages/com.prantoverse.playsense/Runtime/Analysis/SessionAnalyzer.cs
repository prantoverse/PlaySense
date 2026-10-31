using System.Collections.Generic;
using UnityEngine;
using PlaySense.Data.Models;
using PlaySense.Settings;

namespace PlaySense.Analysis
{
    /// <summary>Computes the headline SessionMetrics for a session.</summary>
    public class SessionAnalyzer
    {
        public SessionMetrics Analyze(SessionData session)
        {
            SessionMetrics metrics = new SessionMetrics();
            if (session == null)
                return metrics;

            session.EnsureValid();

            metrics.FrameCount = session.Frames.Count;
            metrics.Duration = session.Duration;
            metrics.EventCount = session.Events.Count;
            metrics.AttentionCount = session.BehaviourObservations.Count;

            List<string> agents = SessionQueries.GetAgentIds(session);
            metrics.AgentCount = agents.Count;

            if (agents.Count == 0)
                return metrics;

            List<FrameData> frames = SessionQueries.GetFrames(session, agents[0]);
            float maxSpeed = PlaySenseSettings.Active.maxPlausibleSpeed;
            float distance = 0f;

            for (int i = 1; i < frames.Count; i++)
            {
                float step = Vector3.Distance(frames[i - 1].Position, frames[i].Position);
                float dt = frames[i].Timestamp - frames[i - 1].Timestamp;

                if (dt > 0f && step / dt <= maxSpeed)
                    distance += step;
            }

            float recordedTime = frames.Count > 1
                ? frames[frames.Count - 1].Timestamp - frames[0].Timestamp
                : 0f;

            metrics.TotalDistance = distance;
            metrics.AverageSpeed = recordedTime > 0f ? distance / recordedTime : 0f;
            return metrics;
        }
    }

    /// <summary>Helpers for reading session data, shared by the editor tools and reports.</summary>
    public static class SessionQueries
    {
        /// <summary>Agent ids in the order they first appear. The first is the primary agent.</summary>
        public static List<string> GetAgentIds(SessionData session)
        {
            List<string> ids = new List<string>();
            if (session == null || session.Frames == null)
                return ids;

            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < session.Frames.Count; i++)
            {
                FrameData frame = session.Frames[i];
                string id = frame != null && frame.ObjectName != null ? frame.ObjectName : string.Empty;

                if (frame != null && seen.Add(id))
                    ids.Add(id);
            }

            return ids;
        }

        /// <summary>Frames of one agent, in time order.</summary>
        public static List<FrameData> GetFrames(SessionData session, string agentId)
        {
            List<FrameData> frames = new List<FrameData>();
            if (session == null || session.Frames == null)
                return frames;

            for (int i = 0; i < session.Frames.Count; i++)
            {
                FrameData frame = session.Frames[i];
                if (frame != null && (frame.ObjectName ?? string.Empty) == (agentId ?? string.Empty))
                    frames.Add(frame);
            }

            return frames;
        }

        /// <summary>Frames of the primary (first recorded) agent.</summary>
        public static List<FrameData> GetPrimaryFrames(SessionData session)
        {
            List<string> agents = GetAgentIds(session);
            return agents.Count > 0 ? GetFrames(session, agents[0]) : new List<FrameData>();
        }

        /// <summary>
        /// Interpolated pose of an agent at a session time. Frames must be one
        /// agent's frames in time order. Returns false when there are none.
        /// </summary>
        public static bool SampleAt(List<FrameData> frames, float time, out Vector3 position, out Quaternion rotation, out Vector3 viewDirection)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            viewDirection = Vector3.forward;

            if (frames == null || frames.Count == 0)
                return false;

            if (time <= frames[0].Timestamp || frames.Count == 1)
            {
                Assign(frames[0], out position, out rotation, out viewDirection);
                return true;
            }

            FrameData last = frames[frames.Count - 1];
            if (time >= last.Timestamp)
            {
                Assign(last, out position, out rotation, out viewDirection);
                return true;
            }

            // Binary search for the first frame after time.
            int low = 0;
            int high = frames.Count - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (frames[mid].Timestamp <= time)
                    low = mid + 1;
                else
                    high = mid;
            }

            FrameData a = frames[low - 1];
            FrameData b = frames[low];
            float span = b.Timestamp - a.Timestamp;
            float t = span > 0f ? (time - a.Timestamp) / span : 0f;

            position = Vector3.Lerp(a.Position, b.Position, t);
            rotation = Quaternion.Slerp(Normalize(a.Rotation), Normalize(b.Rotation), t);

            Vector3 viewA = a.ViewDirection.sqrMagnitude > 0.0001f ? a.ViewDirection : a.Rotation * Vector3.forward;
            Vector3 viewB = b.ViewDirection.sqrMagnitude > 0.0001f ? b.ViewDirection : b.Rotation * Vector3.forward;
            viewDirection = Vector3.Slerp(viewA, viewB, t);
            return true;
        }

        private static void Assign(FrameData frame, out Vector3 position, out Quaternion rotation, out Vector3 viewDirection)
        {
            position = frame.Position;
            rotation = Normalize(frame.Rotation);
            viewDirection = frame.ViewDirection.sqrMagnitude > 0.0001f
                ? frame.ViewDirection
                : rotation * Vector3.forward;
        }

        private static Quaternion Normalize(Quaternion q)
        {
            float magnitude = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            if (magnitude < 0.0001f)
                return Quaternion.identity;

            return new Quaternion(q.x / magnitude, q.y / magnitude, q.z / magnitude, q.w / magnitude);
        }
    }
}
