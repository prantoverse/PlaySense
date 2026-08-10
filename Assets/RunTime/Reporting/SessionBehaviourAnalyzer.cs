using System;
using System.Collections.Generic;
using UnityEngine;
using PlaySense.Data.Models;

namespace PlaySense.Reporting
{
    [Serializable]
    public class BehaviourReport
    {
        // =====================================================
        // MOVEMENT
        // =====================================================

        public float TotalDistance;
        public float AverageSpeed;
        public float PeakSpeed;

        public float ActiveTime;
        public float IdleTime;

        public float ActivePercentage;
        public float IdlePercentage;

        public int MovementSegments;

        // =====================================================
        // EVENTS
        // =====================================================

        public int EventCount;

        public string FirstEvent;
        public string LastEvent;
        public string MostFrequentCategory;

        public float LongestEventGap;

        // =====================================================
        // ATTENTION
        // =====================================================

        public int AttentionCount;

        public float TotalAttentionTime;

        public float AverageAttentionDuration;

        public float LongestAttentionDuration;

        public int UniqueAttendedObjects;

        public int ReinspectionCount;

        // =====================================================
        // FINDINGS
        // =====================================================

        public List<string> Findings =
            new List<string>();
    }

    public class SessionBehaviourAnalyzer
    {
        private const float IdleSpeedThreshold = 0.08f;

        // =====================================================
        // PUBLIC ANALYSIS
        // =====================================================

        public BehaviourReport Analyze(
            SessionData session)
        {
            BehaviourReport report =
                new BehaviourReport();

            if (session == null)
                return report;

            report.TotalDistance =
                session.Metrics.TotalDistance;

            report.AverageSpeed =
                session.Metrics.AverageSpeed;

            report.EventCount =
                session.Events == null
                    ? 0
                    : session.Events.Count;

            AnalyzeMovement(
                session,
                report);

            AnalyzeEvents(
                session,
                report);

            AnalyzeAttention(
                session,
                report);

            BuildFindings(
                session,
                report);

            return report;
        }

        // =====================================================
        // MOVEMENT
        // =====================================================

        private void AnalyzeMovement(
            SessionData session,
            BehaviourReport report)
        {
            if (session.Frames == null ||
                session.Frames.Count < 2)
            {
                report.ActiveTime = 0f;
                report.IdleTime =
                    Mathf.Max(
                        0f,
                        session.Duration);

                report.ActivePercentage = 0f;
                report.IdlePercentage =
                    session.Duration > 0f
                        ? 100f
                        : 0f;

                report.PeakSpeed = 0f;
                report.MovementSegments = 0;

                return;
            }

            float activeTime = 0f;
            float idleTime = 0f;
            float peakSpeed = 0f;

            int movementSegments = 0;

            bool wasMoving = false;

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
                    distance / deltaTime;

                peakSpeed =
                    Mathf.Max(
                        peakSpeed,
                        speed);

                bool moving =
                    speed >
                    IdleSpeedThreshold;

                if (moving)
                {
                    activeTime += deltaTime;

                    if (!wasMoving)
                        movementSegments++;
                }
                else
                {
                    idleTime += deltaTime;
                }

                wasMoving = moving;
            }

            float analysedTime =
                activeTime + idleTime;

            if (analysedTime <= 0f)
            {
                analysedTime =
                    Mathf.Max(
                        0f,
                        session.Duration);
            }

            report.ActiveTime =
                activeTime;

            report.IdleTime =
                idleTime;

            report.PeakSpeed =
                peakSpeed;

            report.MovementSegments =
                movementSegments;

            if (analysedTime > 0f)
            {
                report.ActivePercentage =
                    activeTime /
                    analysedTime *
                    100f;

                report.IdlePercentage =
                    idleTime /
                    analysedTime *
                    100f;
            }
            else
            {
                report.ActivePercentage = 0f;
                report.IdlePercentage = 0f;
            }
        }

        // =====================================================
        // EVENTS
        // =====================================================

        private void AnalyzeEvents(
            SessionData session,
            BehaviourReport report)
        {
            if (session.Events == null ||
                session.Events.Count == 0)
            {
                report.FirstEvent = string.Empty;
                report.LastEvent = string.Empty;
                report.MostFrequentCategory = "None";
                report.LongestEventGap = 0f;

                return;
            }

            GameEventData first =
                session.Events[0];

            GameEventData last =
                session.Events[
                    session.Events.Count - 1];

            report.FirstEvent =
                first == null
                    ? string.Empty
                    : FormatEvent(first);

            report.LastEvent =
                last == null
                    ? string.Empty
                    : FormatEvent(last);

            Dictionary<string, int> categories =
                new Dictionary<string, int>();

            float longestGap = 0f;

            float previousTimestamp = -1f;

            for (int i = 0;
                 i < session.Events.Count;
                 i++)
            {
                GameEventData gameEvent =
                    session.Events[i];

                if (gameEvent == null)
                    continue;

                string category =
                    string.IsNullOrEmpty(
                        gameEvent.Category)
                        ? "Unknown"
                        : gameEvent.Category;

                if (!categories.ContainsKey(category))
                    categories[category] = 0;

                categories[category]++;

                if (previousTimestamp >= 0f)
                {
                    float gap =
                        gameEvent.Timestamp -
                        previousTimestamp;

                    if (gap >= 0f)
                    {
                        longestGap =
                            Mathf.Max(
                                longestGap,
                                gap);
                    }
                }

                previousTimestamp =
                    gameEvent.Timestamp;
            }

            report.LongestEventGap =
                longestGap;

            string mostFrequent =
                "None";

            int highestCount = 0;

            foreach (KeyValuePair<string, int> pair
                     in categories)
            {
                if (pair.Value > highestCount)
                {
                    highestCount =
                        pair.Value;

                    mostFrequent =
                        pair.Key;
                }
            }

            report.MostFrequentCategory =
                mostFrequent;
        }

        // =====================================================
        // ATTENTION
        // =====================================================

        private void AnalyzeAttention(
            SessionData session,
            BehaviourReport report)
        {
            report.AttentionCount = 0;
            report.TotalAttentionTime = 0f;
            report.AverageAttentionDuration = 0f;
            report.LongestAttentionDuration = 0f;
            report.UniqueAttendedObjects = 0;
            report.ReinspectionCount = 0;

            if (session == null ||
                session.BehaviourObservations == null ||
                session.BehaviourObservations.Count == 0)
            {
                return;
            }

            HashSet<string> attendedObjects =
                new HashSet<string>();

            float totalDuration = 0f;

            for (int i = 0;
                 i < session.BehaviourObservations.Count;
                 i++)
            {
                BehaviourObservation observation =
                    session.BehaviourObservations[i];

                if (observation == null)
                    continue;

                // -------------------------------------------------
                // ATTENTION FILTER
                // -------------------------------------------------

                if (!observation.Attended)
                    continue;

                // -------------------------------------------------
                // COUNT
                // -------------------------------------------------

                report.AttentionCount++;

                // -------------------------------------------------
                // DURATION
                // -------------------------------------------------

                float duration =
                    Mathf.Max(
                        0f,
                        observation.Duration);

                totalDuration +=
                    duration;

                report.TotalAttentionTime +=
                    duration;

                report.LongestAttentionDuration =
                    Mathf.Max(
                        report.LongestAttentionDuration,
                        duration);

                // -------------------------------------------------
                // UNIQUE OBJECT
                // -------------------------------------------------

                if (!string.IsNullOrWhiteSpace(
                    observation.Subject))
                {
                    attendedObjects.Add(
                        observation.Subject);
                }

                // -------------------------------------------------
                // REINSPECTION
                // -------------------------------------------------

                if (string.Equals(
                    observation.Type,
                    "Reinspection",
                    StringComparison.OrdinalIgnoreCase))
                {
                    report.ReinspectionCount++;
                }
            }

            report.UniqueAttendedObjects =
                attendedObjects.Count;

            if (report.AttentionCount > 0)
            {
                report.AverageAttentionDuration =
                    totalDuration /
                    report.AttentionCount;
            }
        }

        // =====================================================
        // FINDINGS
        // =====================================================

        private void BuildFindings(
            SessionData session,
            BehaviourReport report)
        {
            report.Findings.Clear();

            if (session == null ||
                session.Duration <= 0f)
            {
                return;
            }

            // =================================================
            // MOVEMENT FINDINGS
            // =================================================

            if (report.ActivePercentage >= 80f)
            {
                report.Findings.Add(
                    $"The player remained highly active, with movement detected during {report.ActivePercentage:F0}% of the analysed session.");
            }
            else if (report.ActivePercentage <= 35f)
            {
                report.Findings.Add(
                    $"The player spent most of the session moving very little, with only {report.ActivePercentage:F0}% classified as active movement.");
            }
            else
            {
                report.Findings.Add(
                    $"The player showed a mixed movement pattern, with {report.ActivePercentage:F0}% of the session classified as active movement.");
            }

            if (report.IdleTime >= 5f)
            {
                report.Findings.Add(
                    $"The player spent approximately {report.IdleTime:F1} seconds with minimal movement.");
            }

            if (report.PeakSpeed > 0f)
            {
                report.Findings.Add(
                    $"Peak recorded movement speed was {report.PeakSpeed:F2} m/s, compared with an average of {report.AverageSpeed:F2} m/s.");
            }

            if (report.MovementSegments >= 5)
            {
                report.Findings.Add(
                    $"The movement pattern contained {report.MovementSegments} distinct movement segments, suggesting repeated starts and stops during exploration.");
            }
            else if (report.MovementSegments <= 1)
            {
                report.Findings.Add(
                    "The player maintained a relatively continuous movement pattern with few starts and stops.");
            }

            // =================================================
            // EVENT FINDINGS
            // =================================================

            if (report.EventCount == 0)
            {
                report.Findings.Add(
                    "No gameplay events were recorded, so interaction-level behavioural analysis is limited.");
            }
            else
            {
                report.Findings.Add(
                    $"{report.EventCount} gameplay event(s) were recorded during the session.");

                if (!string.IsNullOrEmpty(
                    report.MostFrequentCategory) &&
                    report.MostFrequentCategory != "None")
                {
                    report.Findings.Add(
                        $"The most frequently recorded event category was \"{report.MostFrequentCategory}\".");
                }
            }

            if (report.LongestEventGap >= 10f)
            {
                report.Findings.Add(
                    $"There was a {report.LongestEventGap:F1}-second gap between recorded gameplay events.");
            }

            // =================================================
            // ATTENTION FINDINGS
            // =================================================

            if (report.AttentionCount == 0)
            {
                report.Findings.Add(
                    "No attended objects were recorded, so visual-attention analysis is limited.");
            }
            else
            {
                report.Findings.Add(
                    $"{report.AttentionCount} attention sequence(s) were recorded across {report.UniqueAttendedObjects} unique object(s).");

                report.Findings.Add(
                    $"The player spent {report.TotalAttentionTime:F1} seconds in recorded visual attention, with an average attention duration of {report.AverageAttentionDuration:F1} seconds.");

                if (report.LongestAttentionDuration >= 3f)
                {
                    report.Findings.Add(
                        $"The longest recorded attention sequence lasted {report.LongestAttentionDuration:F1} seconds, indicating sustained attention to at least one object.");
                }

                if (report.ReinspectionCount > 0)
                {
                    report.Findings.Add(
                        $"The player returned to previously inspected content {report.ReinspectionCount} time(s), indicating repeated visual inspection.");
                }
            }
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private string FormatEvent(
            GameEventData gameEvent)
        {
            if (gameEvent == null)
                return string.Empty;

            string category =
                string.IsNullOrEmpty(
                    gameEvent.Category)
                    ? "Event"
                    : gameEvent.Category;

            string action =
                string.IsNullOrEmpty(
                    gameEvent.Action)
                    ? "Unknown action"
                    : gameEvent.Action;

            string target =
                string.IsNullOrEmpty(
                    gameEvent.Target)
                    ? ""
                    : $" — {gameEvent.Target}";

            return
                $"{gameEvent.Timestamp:F2}s " +
                $"{category}: " +
                $"{action}" +
                target;
        }
    }
}