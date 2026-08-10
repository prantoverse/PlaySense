using System;
using UnityEngine;

namespace PlaySense.Data.Models
{
    [Serializable]
    public class FrameData
    {
        public string ObjectName = string.Empty;

        public float Timestamp;

        public Vector3 Position;

        public Quaternion Rotation;

    }
}