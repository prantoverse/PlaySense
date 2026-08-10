using System;
using System.Collections.Generic;
using UnityEngine;
using PlaySense.Data.Models;

namespace PlaySense.Reporting
{
    [Serializable]
    public class AttentionObjectAnalysis
    {
        public string ObjectName;

        public int EpisodeCount;
        public float TotalAttentionTime;
        public float AverageAttentionDuration;
        public float LongestAttentionDuration;

        public int ReinspectionCount;
        public float AttentionPercentage;

        public AttentionObjectAnalysis()
        {
            ObjectName = string.Empty;
            EpisodeCount = 0;
            TotalAttentionTime = 0f;
            AverageAttentionDuration = 0f;
            LongestAttentionDuration = 0f;
            ReinspectionCount = 0;
            AttentionPercentage = 0f;
        }
    }

    [Serializable]
    public class AttentionAnalysis
    {
        // =====================================================
        // OVERVIEW
        // =====================================================

        public int TotalEpisodes;
        public float TotalAttentionTime;
        public float AverageDuration;
        public float LongestDuration;

        public int UniqueObjects;
        public int ReinspectionCount;

        // =====================================================
        // ATTENTION QUALITY
        // =====================================================

        public int BriefEpisodes;
        public int FocusedEpisodes;
        public int SustainedEpisodes;

        public float BriefAttentionTime;
        public float FocusedAttentionTime;
        public float SustainedAttentionTime;

        // =====================================================
        // ATTENTION SWITCHING
        // =====================================================

        public int AttentionSwitchCount;
        public int RapidSwitchCount;
        public float LongestAttentionGap;

        // =====================================================
        // MOVEMENT CONTEXT
        // =====================================================

        public float StationaryAttentionTime;
        public float MovingAttentionTime;

        public int StationaryAttentionEpisodes;
        public int MovingAttentionEpisodes;

        // =====================================================
        // OBJECT FOCUS
        // =====================================================

        public string MostAttendedObject;
        public float MostAttendedObjectTime;
        public float MostAttendedObjectPercentage;

        public string LongestAttendedObject;
        public float LongestAttendedObjectDuration;

        public List<AttentionObjectAnalysis> Objects =
            new List<AttentionObjectAnalysis>();

        // =====================================================
        // FINDINGS
        // =====================================================

        public List<string> Findings =
            new List<string>();
    }

    public class SessionAttentionAnalyzer
    {
        // =====================================================
        // THRESHOLDS
        // =====================================================

        private const float BriefAttentionThreshold = 1.0f;
        private const float SustainedAttentionThreshold = 3.0f;

        private const float RapidSwitchThreshold = 1.5f;
        private const float StationarySpeedThreshold = 0.08f;

        // =====================================================
        // PUBLIC ANALYSIS
        // =====================================================

        public AttentionAnalysis Analyze(SessionData session)
        {
            AttentionAnalysis analysis =
                new AttentionAnalysis();

            if (session == null ||
                session.BehaviourObservations == null ||
                session.BehaviourObservations.Count == 0)
            {
                BuildFindings(analysis);
                return analysis;
            }

            Dictionary<string, AttentionObjectAnalysis> objectLookup =
                new Dictionary<string, AttentionObjectAnalysis>(
                    StringComparer.OrdinalIgnoreCase);

            float previousAttentionEnd = -1f;
            string previousObject = string.Empty;

            for (int i = 0;
                 i < session.BehaviourObservations.Count;
                 i++)
            {
                BehaviourObservation observation =
                    session.BehaviourObservations[i];

                if (observation == null ||
                    !observation.Attended)
                {
                    continue;
                }

                float duration =
                    Mathf.Max(0f, observation.Duration);

                string objectName =
                    string.IsNullOrWhiteSpace(observation.Subject)
                        ? "Unknown object"
                        : observation.Subject.Trim();

                analysis.TotalEpisodes++;
                analysis.TotalAttentionTime += duration;

                analysis.LongestDuration =
                    Mathf.Max(
                        analysis.LongestDuration,
                        duration);

                // -------------------------------------------------
                // ATTENTION QUALITY
                // -------------------------------------------------

                if (duration < BriefAttentionThreshold)
                {
                    analysis.BriefEpisodes++;
                    analysis.BriefAttentionTime += duration;
                }
                else if (duration >= SustainedAttentionThreshold)
                {
                    analysis.SustainedEpisodes++;
                    analysis.SustainedAttentionTime += duration;
                }
                else
                {
                    analysis.FocusedEpisodes++;
                    analysis.FocusedAttentionTime += duration;
                }

                // -------------------------------------------------
                // OBJECT ANALYSIS
                // -------------------------------------------------

                AttentionObjectAnalysis objectAnalysis;

                if (!objectLookup.TryGetValue(
                    objectName,
                    out objectAnalysis))
                {
                    objectAnalysis =
                        new AttentionObjectAnalysis
                        {
                            ObjectName = objectName
                        };

                    objectLookup.Add(
                        objectName,
                        objectAnalysis);

                    analysis.UniqueObjects++;
                }

                objectAnalysis.EpisodeCount++;
                objectAnalysis.TotalAttentionTime += duration;

                objectAnalysis.LongestAttentionDuration =
                    Mathf.Max(
                        objectAnalysis.LongestAttentionDuration,
                        duration);

                if (string.Equals(
                    observation.Type,
                    "Reinspection",
                    StringComparison.OrdinalIgnoreCase))
                {
                    objectAnalysis.ReinspectionCount++;
                    analysis.ReinspectionCount++;
                }

                // -------------------------------------------------
                // ATTENTION SWITCHING
                // -------------------------------------------------

                if (previousAttentionEnd >= 0f)
                {
                    float gap =
                        observation.Timestamp -
                        previousAttentionEnd;

                    if (gap >= 0f)
                    {
                        analysis.LongestAttentionGap =
                            Mathf.Max(
                                analysis.LongestAttentionGap,
                                gap);

                        if (!string.Equals(
                            previousObject,
                            objectName,
                            StringComparison.OrdinalIgnoreCase))
                        {
                            analysis.AttentionSwitchCount++;

                            if (gap <= RapidSwitchThreshold)
                            {
                                analysis.RapidSwitchCount++;
                            }
                        }
                    }
                }

                previousAttentionEnd =
                    observation.Timestamp + duration;

                previousObject =
                    objectName;

                // -------------------------------------------------
                // MOVEMENT CONTEXT
                //
                // BehaviourObservation does not currently contain
                // a recorded speed value, so movement state is
                // inferred from nearby FrameData samples.
                // -------------------------------------------------

                bool moving =
                    WasMovingDuringObservation(
                        session,
                        observation);

                if (moving)
                {
                    analysis.MovingAttentionTime += duration;
                    analysis.MovingAttentionEpisodes++;
                }
                else
                {
                    analysis.StationaryAttentionTime += duration;
                    analysis.StationaryAttentionEpisodes++;
                }
            }

            // =====================================================
            // FINALIZE OBJECT ANALYSIS
            // =====================================================

            foreach (KeyValuePair<string, AttentionObjectAnalysis> pair
                     in objectLookup)
            {
                AttentionObjectAnalysis objectAnalysis =
                    pair.Value;

                if (objectAnalysis.EpisodeCount > 0)
                {
                    objectAnalysis.AverageAttentionDuration =
                        objectAnalysis.TotalAttentionTime /
                        objectAnalysis.EpisodeCount;
                }

                if (analysis.TotalAttentionTime > 0f)
                {
                    objectAnalysis.AttentionPercentage =
                        objectAnalysis.TotalAttentionTime /
                        analysis.TotalAttentionTime *
                        100f;
                }

                analysis.Objects.Add(
                    objectAnalysis);
            }

            analysis.Objects.Sort(
                CompareObjectAttention);

            if (analysis.TotalEpisodes > 0)
            {
                analysis.AverageDuration =
                    analysis.TotalAttentionTime /
                    analysis.TotalEpisodes;
            }

            // -----------------------------------------------------
            // MOST ATTENDED OBJECT
            // -----------------------------------------------------

            if (analysis.Objects.Count > 0)
            {
                AttentionObjectAnalysis mostAttended =
                    analysis.Objects[0];

                analysis.MostAttendedObject =
                    mostAttended.ObjectName;

                analysis.MostAttendedObjectTime =
                    mostAttended.TotalAttentionTime;

                analysis.MostAttendedObjectPercentage =
                    mostAttended.AttentionPercentage;

                AttentionObjectAnalysis longestObject =
                    analysis.Objects[0];

                for (int i = 1;
                     i < analysis.Objects.Count;
                     i++)
                {
                    if (analysis.Objects[i].LongestAttentionDuration >
                        longestObject.LongestAttentionDuration)
                    {
                        longestObject =
                            analysis.Objects[i];
                    }
                }

                analysis.LongestAttendedObject =
                    longestObject.ObjectName;

                analysis.LongestAttendedObjectDuration =
                    longestObject.LongestAttentionDuration;
            }

            BuildFindings(analysis);

            return analysis;
        }

        // =====================================================
        // MOVEMENT CONTEXT
        // =====================================================

        private bool WasMovingDuringObservation(
            SessionData session,
            BehaviourObservation observation)
        {
            if (session == null ||
                session.Frames == null ||
                session.Frames.Count < 2)
            {
                return false;
            }

            float startTime =
                observation.Timestamp;

            float endTime =
                observation.Timestamp +
                Mathf.Max(
                    0f,
                    observation.Duration);

            float movementTime = 0f;

            for (int i = 1;
                 i < session.Frames.Count;
                 i++)
            {
                FrameData previous =
                    session.Frames[i - 1];

                FrameData current =
                    session.Frames[i];

                if (previous == null ||
                    current == null)
                {
                    continue;
                }

                if (current.Timestamp < startTime)
                    continue;

                if (previous.Timestamp > endTime)
                    break;

                float deltaTime =
                    current.Timestamp -
                    previous.Timestamp;

                if (deltaTime <= 0f)
                    continue;

                float distance =
                    Vector3.Distance(
                        previous.Position,
                        current.Position);

                float speed =
                    distance /
                    deltaTime;

                float segmentStart =
                    Mathf.Max(
                        previous.Timestamp,
                        startTime);

                float segmentEnd =
                    Mathf.Min(
                        current.Timestamp,
                        endTime);

                float overlap =
                    segmentEnd -
                    segmentStart;

                if (overlap <= 0f)
                    continue;

                if (speed >
                    StationarySpeedThreshold)
                {
                    movementTime += overlap;
                }
            }

            return movementTime > 0f;
        }

        // =====================================================
        // OBJECT SORTING
        // =====================================================

        private int CompareObjectAttention(
            AttentionObjectAnalysis a,
            AttentionObjectAnalysis b)
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return 1;

            if (b == null)
                return -1;

            return b.TotalAttentionTime.CompareTo(
                a.TotalAttentionTime);
        }

        // =====================================================
        // FINDINGS
        // =====================================================

        private void BuildFindings(
            AttentionAnalysis analysis)
        {
            analysis.Findings.Clear();

            if (analysis == null ||
                analysis.TotalEpisodes == 0)
            {
                if (analysis != null)
                {
                    analysis.Findings.Add(
                        "No attended objects were recorded, so visual-attention analysis is limited.");
                }

                return;
            }

            // -------------------------------------------------
            // OVERVIEW
            // -------------------------------------------------

            analysis.Findings.Add(
                $"{analysis.TotalEpisodes} attention episode(s) were recorded across {analysis.UniqueObjects} unique object(s), covering {analysis.TotalAttentionTime:F1} seconds of visual attention.");

            // -------------------------------------------------
            // ATTENTION DEPTH
            // -------------------------------------------------

            if (analysis.SustainedEpisodes > 0)
            {
                analysis.Findings.Add(
                    $"{analysis.SustainedEpisodes} attention episode(s) reached at least {SustainedAttentionThreshold:F0} seconds, indicating sustained visual attention.");
            }
            else
            {
                analysis.Findings.Add(
                    "No attention episode reached the sustained-attention threshold.");
            }

            if (analysis.BriefEpisodes > 0)
            {
                analysis.Findings.Add(
                    $"{analysis.BriefEpisodes} attention episode(s) were brief, lasting less than {BriefAttentionThreshold:F0} second.");
            }

            // -------------------------------------------------
            // FOCUS CONCENTRATION
            // -------------------------------------------------

            if (!string.IsNullOrEmpty(
                analysis.MostAttendedObject))
            {
                analysis.Findings.Add(
                    $"The most attended object was \"{analysis.MostAttendedObject}\", receiving {analysis.MostAttendedObjectTime:F1} seconds of attention ({analysis.MostAttendedObjectPercentage:F0}% of recorded attention).");
            }

            // -------------------------------------------------
            // REINSPECTION
            // -------------------------------------------------

            if (analysis.ReinspectionCount > 0)
            {
                analysis.Findings.Add(
                    $"The player returned to previously inspected content {analysis.ReinspectionCount} time(s), indicating repeated visual inspection.");
            }
            else
            {
                analysis.Findings.Add(
                    "No explicit reinspection episodes were recorded.");
            }

            // -------------------------------------------------
            // SWITCHING
            // -------------------------------------------------

            if (analysis.AttentionSwitchCount > 0)
            {
                analysis.Findings.Add(
                    $"Attention shifted between different objects {analysis.AttentionSwitchCount} time(s).");

                if (analysis.RapidSwitchCount > 0)
                {
                    analysis.Findings.Add(
                        $"{analysis.RapidSwitchCount} of those attention switches occurred within {RapidSwitchThreshold:F1} seconds, suggesting rapid visual switching.");
                }
            }

            // -------------------------------------------------
            // MOVEMENT CONTEXT
            // -------------------------------------------------

            if (analysis.TotalAttentionTime > 0f)
            {
                float stationaryPercentage =
                    analysis.StationaryAttentionTime /
                    analysis.TotalAttentionTime *
                    100f;

                float movingPercentage =
                    analysis.MovingAttentionTime /
                    analysis.TotalAttentionTime *
                    100f;

                analysis.Findings.Add(
                    $"Attention occurred approximately {stationaryPercentage:F0}% while stationary and {movingPercentage:F0}% while moving.");
            }
        }
    }
}