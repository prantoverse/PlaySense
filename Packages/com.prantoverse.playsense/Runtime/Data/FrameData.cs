using System;
using UnityEngine;

namespace PlaySense.Data.Models
{
    /// <summary>One position/rotation sample of one agent.</summary>
    [Serializable]
    public class FrameData
    {
        /// <summary>Agent id (see PlaySenseAgent.Id). Frames from several agents are interleaved.</summary>
        public string ObjectName = string.Empty;

        /// <summary>Seconds since the session started, on the PlaySense session clock.</summary>
        public float Timestamp;

        public Vector3 Position;

        public Quaternion Rotation;

        /// <summary>Direction the agent was looking (its view camera, else its forward).</summary>
        public Vector3 ViewDirection;
    }
}
