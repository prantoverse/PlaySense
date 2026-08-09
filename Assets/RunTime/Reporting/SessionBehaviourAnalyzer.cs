using System;
using System.Collections.Generic;
using UnityEngine;
using PlaySense.Data.Models;

namespace PlaySense.Reporting
{
    public class BehaviourReport
    {
        public float TotalDistance;
        public float AverageSpeed;
        public float PeakSpeed;

        public float ActiveTime;
        public float IdleTime;

        public float ActivePercentage;
        public float IdlePercentage;

        public int MovementSegments;
        public int EventCount;

        public string FirstEvent;
        public string LastEvent;
        public string MostFrequentCategory;

        public float LongestEventGap;

        public List<string> Findings =
            new List<string>();
    }

    public class SessionBehaviourAnalyzer
    {
        private const float IdleSpeedThreshold = 0.08f;

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
                    session.Duration;

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
                analysedTime =
                    session.Duration;

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
                return;
            }

            GameEventData first =
                session.Events[0];

            GameEventData last =
                session.Events[
                    session.Events.Count - 1];

            report.FirstEvent =
                FormatEvent(first);

            report.LastEvent =
                FormatEvent(last);

            Dictionary<string, int> categories =
                new Dictionary<string, int>();

            float longestGap = 0f;

            for (int i = 0;
                 i < session.Events.Count;
                 i++)
            {
                GameEventData gameEvent =
                    session.Events[i];

                string category =
                    string.IsNullOrEmpty(
                        gameEvent.Category)
                        ? "Unknown"
                        : gameEvent.Category;

                if (!categories.ContainsKey(category))
                    categories[category] = 0;

                categories[category]++;

                if (i > 0)
                {
                    float gap =
                        gameEvent.Timestamp -
                        session.Events[i - 1].Timestamp;

                    longestGap =
                        Mathf.Max(
                            longestGap,
                            gap);
                }
            }

            report.LongestEventGap =
                longestGap;

            string mostFrequent =
                "None";

            int highestCount = 0;

            foreach (var pair in categories)
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
        // FINDINGS
        // =====================================================

        private void BuildFindings(
            SessionData session,
            BehaviourReport report)
        {
            report.Findings.Clear();

            if (session.Duration <= 0f)
                return;

            // -------------------------------------------------
            // Activity
            // -------------------------------------------------

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

            // -------------------------------------------------
            // Idle behaviour
            // -------------------------------------------------

            if (report.IdleTime >= 5f)
            {
                report.Findings.Add(
                    $"The player spent approximately {report.IdleTime:F1} seconds with minimal movement.");
            }

            // -------------------------------------------------
            // Speed
            // -------------------------------------------------

            if (report.PeakSpeed > 0f)
            {
                report.Findings.Add(
                    $"Peak recorded movement speed was {report.PeakSpeed:F2} m/s, compared with an average of {report.AverageSpeed:F2} m/s.");
            }

            // -------------------------------------------------
            // Exploration
            // -------------------------------------------------

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

            // -------------------------------------------------
            // Events
            // -------------------------------------------------

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

            // -------------------------------------------------
            // Event gaps
            // -------------------------------------------------

            if (report.LongestEventGap >= 10f)
            {
                report.Findings.Add(
                    $"There was a {report.LongestEventGap:F1}-second gap between recorded gameplay events.");
            }
        }

        // =====================================================
        // HELPERS
        // =====================================================

        private string FormatEvent(
            GameEventData gameEvent)
        {
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