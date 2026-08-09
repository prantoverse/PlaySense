using System.Collections.Generic;
using UnityEngine;

using PlaySense.Perception;

namespace PlaySense.Behaviour
{
    public class PlaySenseFindingAnalyzer : MonoBehaviour
    {
        // =====================================================
        // SETTINGS
        // =====================================================

        [Header("Finding Rules")]

        [SerializeField]
        private float shortAttentionThreshold = 1.0f;

        [SerializeField]
        private float sustainedAttentionThreshold = 3.0f;

        [SerializeField]
        private int minimumObjectsForEnvironmentalInspection = 3;

        // =====================================================
        // DIAGNOSTICS
        // =====================================================

        [Header("Diagnostics")]

        [SerializeField]
        private bool debugFindings = true;

        // =====================================================
        // REFERENCES
        // =====================================================

        private PlaySensePerception _perception;

        // =====================================================
        // CURSOR
        // =====================================================

        private int _attentionCursor;

        // =====================================================
        // FINDINGS
        // =====================================================

        private readonly List<PlaySenseFinding>
            _findings =
                new List<PlaySenseFinding>();

        public IReadOnlyList<PlaySenseFinding>
            Findings =>
            _findings;

        // =====================================================
        // TRACKING
        // =====================================================

        private readonly HashSet<string>
            _inspectedObjects =
                new HashSet<string>();

        private bool _environmentInspectionDetected;

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
                    "[PlaySense] Finding Analyzer requires " +
                    "PlaySensePerception on the same GameObject.");

                enabled = false;
                return;
            }

            Debug.Log(
                "[PlaySense] Finding Analyzer initialized.");
        }

        // =====================================================
        // UPDATE
        // =====================================================

        private void Update()
        {
            if (_perception == null)
                return;

            ProcessAttentionSequence();
        }

        // =====================================================
        // PROCESS ATTENTION
        // =====================================================

        private void ProcessAttentionSequence()
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
                PlaySensePerception.AttentionSequenceEntry
                    entry =
                    sequence[_attentionCursor];

                if (entry != null &&
                    entry.EndTime >= 0f)
                {
                    AnalyzeAttention(entry);
                }

                _attentionCursor++;
            }
        }

        // =====================================================
        // ANALYZE ATTENTION
        // =====================================================

        private void AnalyzeAttention(
            PlaySensePerception.AttentionSequenceEntry entry)
        {
            if (entry == null)
                return;

            // -------------------------------------------------
            // TRACK UNIQUE OBJECT
            // -------------------------------------------------

            _inspectedObjects.Add(
                entry.ObjectName);

            // -------------------------------------------------
            // ATTENTION TYPE
            // -------------------------------------------------

            if (entry.Duration <
                shortAttentionThreshold)
            {
                AddFinding(
                    "BriefInspection",
                    $"The player briefly inspected " +
                    $"{entry.ObjectName}.",
                    entry.StartTime);
            }
            else if (entry.Duration >=
                     sustainedAttentionThreshold)
            {
                AddFinding(
                    "SustainedInspection",
                    $"The player maintained attention on " +
                    $"{entry.ObjectName} for " +
                    $"{entry.Duration:F2} seconds.",
                    entry.StartTime);
            }
            else
            {
                AddFinding(
                    "FocusedInspection",
                    $"The player focused attention on " +
                    $"{entry.ObjectName}.",
                    entry.StartTime);
            }

            // -------------------------------------------------
            // REINSPECTION
            // -------------------------------------------------

            if (entry.IsReinspection)
            {
                AddFinding(
                    "Reinspection",
                    $"The player returned attention to " +
                    $"{entry.ObjectName}.",
                    entry.StartTime);
            }

            // -------------------------------------------------
            // ENVIRONMENTAL INSPECTION
            // -------------------------------------------------

            if (!_environmentInspectionDetected &&
                _inspectedObjects.Count >=
                minimumObjectsForEnvironmentalInspection)
            {
                _environmentInspectionDetected =
                    true;

                AddFinding(
                    "EnvironmentalInspection",
                    "The player visually inspected " +
                    "multiple environmental objects.",
                    entry.StartTime);
            }
        }

        // =====================================================
        // ADD FINDING
        // =====================================================

        private void AddFinding(
            string type,
            string text,
            float timestamp)
        {
            PlaySenseFinding finding =
                new PlaySenseFinding(
                    type,
                    text,
                    timestamp);

            _findings.Add(
                finding);

            if (debugFindings)
            {
                Debug.Log(
                    $"[PlaySense] FINDING: " +
                    $"{type} → {text}");
            }
        }

        // =====================================================
        // PUBLIC QUERIES
        // =====================================================

        public int GetFindingCount()
        {
            return _findings.Count;
        }

        public PlaySenseFinding GetFinding(
            int index)
        {
            if (index < 0 ||
                index >= _findings.Count)
            {
                return null;
            }

            return _findings[index];
        }
    }
}