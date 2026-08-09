using System.Collections.Generic;
using UnityEngine;

using PlaySense.Perception;
using PlaySense.Data.Models;

namespace PlaySense.Behaviour
{
    public class PlaySenseBehaviourInterpreter : MonoBehaviour
    {
        // =====================================================
        // SETTINGS
        // =====================================================

        [Header("Interpretation")]

        [SerializeField]
        private float shortAttentionThreshold = 1.0f;

        [SerializeField]
        private float sustainedAttentionThreshold = 3.0f;

        [SerializeField]
        private float reinspectionGapThreshold = 5.0f;

        [SerializeField]
        private float rapidSwitchGapThreshold = 1.5f;

        [Header("Diagnostics")]

        [SerializeField]
        private bool debugInterpretation = true;

        // =====================================================
        // REFERENCES
        // =====================================================

        private PlaySensePerception _perception;

        // =====================================================
        // CURSOR
        // =====================================================

        private int _attentionCursor;

        // =====================================================
        // INTERPRETATIONS
        // =====================================================

        private readonly List<BehaviourInterpretation>
            _interpretations =
                new List<BehaviourInterpretation>();

        public IReadOnlyList<BehaviourInterpretation>
            Interpretations =>
            _interpretations;

        // =====================================================
        // FINDINGS
        // =====================================================

        private readonly List<PlaySenseFinding> _findings = new List<PlaySenseFinding>();

        public IReadOnlyList<PlaySenseFinding> Findings => _findings;

        // =====================================================
        // INITIALIZATION
        // =====================================================

        private void Awake()
        {
            _perception =
                GetComponent<PlaySensePerception>();

            if (_perception == null)
            {
                Debug.LogError(
                    "[PlaySense] Behaviour Interpreter " +
                    "requires PlaySensePerception " +
                    "on the same GameObject.");

                enabled = false;
                return;
            }

            Debug.Log(
                "[PlaySense] Behaviour Interpreter initialized.");
        }

        // =====================================================
        // UPDATE
        // =====================================================

        private void Update()
        {
            if (_perception == null)
                return;

            ProcessNewAttentionSequences();
        }

        // =====================================================
        // PROCESS NEW ATTENTION
        // =====================================================

        private void ProcessNewAttentionSequences()
        {
            IReadOnlyList<
                PlaySensePerception.AttentionSequenceEntry>
                sequence =
                _perception.AttentionSequence;

            if (sequence == null)
                return;

            while (_attentionCursor <
                   sequence.Count)
            {
                PlaySensePerception
                    .AttentionSequenceEntry entry =
                    sequence[_attentionCursor];

                if (entry != null &&
                    entry.EndTime >= 0f)
                {
                    InterpretAttention(entry);
                }

                _attentionCursor++;
            }
        }

        // =====================================================
        // INTERPRET ATTENTION
        // =====================================================

        private void InterpretAttention(
            PlaySensePerception.AttentionSequenceEntry entry)
        {
            if (entry == null)
                return;

            // -------------------------------------------------
            // SHORT INSPECTION
            // -------------------------------------------------

            if (entry.Duration <
                shortAttentionThreshold)
            {
                CreateInterpretation(
                    "BriefInspection",
                    entry,
                    "The player briefly attended to the object.");
            }

            // -------------------------------------------------
            // SUSTAINED INSPECTION
            // -------------------------------------------------

            else if (entry.Duration >=
                     sustainedAttentionThreshold)
            {
                CreateInterpretation(
                    "SustainedInspection",
                    entry,
                    "The player maintained attention on the object.");
            }

            // -------------------------------------------------
            // NORMAL INSPECTION
            // -------------------------------------------------

            else
            {
                CreateInterpretation(
                    "FocusedInspection",
                    entry,
                    "The player maintained focused visual attention.");
            }

            // -------------------------------------------------
            // REINSPECTION
            // -------------------------------------------------

            if (entry.IsReinspection)
            {
                CreateInterpretation(
                    "Reinspection",
                    entry,
                    "The player returned attention to a previously inspected object.");
            }
        }

        // =====================================================
        // CREATE INTERPRETATION
        // =====================================================

        private void CreateInterpretation(
            string type,
            PlaySensePerception.AttentionSequenceEntry entry,
            string description)
        {
            BehaviourInterpretation interpretation =
                new BehaviourInterpretation
                {
                    Timestamp =
                        entry.StartTime,

                    Duration =
                        entry.Duration,

                    Type =
                        type,

                    Subject =
                        entry.ObjectName,

                    Distance =
                        entry.Distance,

                    Angle =
                        entry.Angle,

                    AttentionScore =
                        entry.AttentionScore,

                    Description =
                        description
                };

            _interpretations.Add(
                interpretation);

            if (debugInterpretation)
            {
                Debug.Log(
                    $"[PlaySense] INTERPRETATION: " +
                    $"{type} → " +
                    $"{entry.ObjectName} | " +
                    $"Duration: " +
                    $"{entry.Duration:F2}s");
            }
        }

        // =====================================================
        // PUBLIC QUERY
        // =====================================================

        public int GetInterpretationCount()
        {
            return _interpretations.Count;
        }

        // =====================================================
        // DATA MODEL
        // =====================================================

        [System.Serializable]
        public class BehaviourInterpretation
        {
            public float Timestamp;

            public float Duration;

            public string Type;

            public string Subject;

            public float Distance;

            public float Angle;

            public float AttentionScore;

            public string Description;
        }
    }
}