using System;
using UnityEngine;

namespace PlaySense.Data.Models
{
    [Serializable]
    public class BehaviourObservation
    {
        // =====================================================
        // TIME
        // =====================================================

        public float Timestamp;

        public float Duration;

        // =====================================================
        // OBSERVATION
        // =====================================================

        public string Type;

        public string Subject;

        // =====================================================
        // CONTEXT
        // =====================================================

        public float Distance;

        public float Angle;

        public Vector3 Position;

        // =====================================================
        // STATE
        // =====================================================

        public bool Visible;

        public bool Unobstructed;

        public bool Attended;

        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public BehaviourObservation()
        {
            Timestamp = 0f;
            Duration = 0f;

            Type = string.Empty;
            Subject = string.Empty;

            Distance = 0f;
            Angle = 0f;

            Position = Vector3.zero;

            Visible = false;
            Unobstructed = false;
            Attended = false;
        }
    }
}