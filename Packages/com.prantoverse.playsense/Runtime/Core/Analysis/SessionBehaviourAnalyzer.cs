using System;
using System.Collections.Generic;
using UnityEngine;
using PlaySense.Data.Models;
using PlaySense.Settings;

namespace PlaySense.Analysis
{
    /// <summary>Designer-facing behaviour summary of one session.</summary>
    [Serializable]
    public class BehaviourReport
    {
        // Movement (primary agent)
        public float TotalDistance;
        public float AverageSpeed;
        public float PeakSpeed;
        public float ActiveTime;
        public float IdleTime;
        public float ActivePercentage;
        public float IdlePercentage;
        public int MovementSegments;

        // Events
        public int EventCount;
        public string FirstEvent = string.Empty;
        public string LastEvent = string.Empty;
        public string MostFrequentCategory = "None";
        public float LongestEventGap;

        // Attention
        public int AttentionCount;
        public float TotalAttentionTime;
        public float AverageAttentionDuration;
        public float LongestAttentionDuration;
        public int UniqueAttendedObjects;
        public int ReinspectionCount;
        public int BriefAttentionCount;
        public int SustainedAttentionCount;

        /// <summary>Attention totals per target, most attended first.</summary>
        public List<TargetAttention> Targets = new List<TargetAttention>();

        /// <summary>Plain-language findings for the report.</summary>
        public List<string> Findings = new List<string>();
    }

    [Serializable]
    public class TargetAttention
    {
        public string TargetId = string.Empty;
        public string Name = string.Empty;
        public string Category = string.Empty;
        public int Count;
        public float TotalDuration;
        public float LongestDuration;
        public float FirstSeen;
        public Vector3 Position;
    }

    /// <summary>Turns a recorded session into a BehaviourReport. Thresholds come from PlaySenseSettings.</summary>
    public class SessionBehaviourAnalyzer
    {
        private readonly PlaySenseSettings _settings;

        public SessionBehaviourAnalyzer()
            : this(null)
        {
        }

        public SessionBehaviourAnalyzer(PlaySenseSettings settings)
        {
            _settings = settings != null ? settings : PlaySenseSettings.Active;
        }

        public BehaviourReport Analyze(SessionData session)
        {
            BehaviourReport report = new BehaviourReport();
            if (session == null)
                return report;

            session.EnsureValid();

            report.TotalDistance = session.Metrics.TotalDistance;
            report.AverageSpeed = session.Metrics.AverageSpeed;

            AnalyzeMovement(session, report);
            AnalyzeEvents(session, report);
            AnalyzeAttention(session, report);
            BuildFindings(session, report);
            return report;
        }

        // =====================================================
        // MOVEMENT
        // =====================================================

        private void AnalyzeMovement(SessionData session, BehaviourReport report)
        {
            List<FrameData> frames = SessionQueries.GetPrimaryFrames(session);

            if (frames.Count < 2)
            {
                report.IdleTime = Mathf.Max(0f, session.Duration);
                report.IdlePercentage = session.Duration > 0f ? 100f : 0f;
                return;
            }

            bool wasMoving = false;

            for (int i = 1; i < frames.Count; i++)
            {
                float dt = frames[i].Timestamp - frames[i - 1].Timestamp;
                if (dt <= 0f)
                    continue;

                float speed = Vector3.Distance(frames[i - 1].Position, frames[i].Position) / dt;

                // Teleports, respawns and scene changes are not movement.
                if (speed > _settings.maxPlausibleSpeed || frames[i].Scene != frames[i - 1].Scene)
                    continue;

                report.PeakSpeed = Mathf.Max(report.PeakSpeed, speed);

                bool moving = speed > _settings.idleSpeedThreshold;
                if (moving)
                {
                    report.ActiveTime += dt;
                    if (!wasMoving)
                        report.MovementSegments++;
                }
                else
                {
                    report.IdleTime += dt;
                }

                wasMoving = moving;
            }

            float analysed = report.ActiveTime + report.IdleTime;
            if (analysed > 0f)
            {
                report.ActivePercentage = report.ActiveTime / analysed * 100f;
                report.IdlePercentage = report.IdleTime / analysed * 100f;
            }
        }

        // =====================================================
        // EVENTS
        // =====================================================

        private static void AnalyzeEvents(SessionData session, BehaviourReport report)
        {
            List<GameEventData> events = session.Events;
            report.EventCount = events.Count;

            if (events.Count == 0)
                return;

            report.FirstEvent = FormatEvent(events[0]);
            report.LastEvent = FormatEvent(events[events.Count - 1]);

            Dictionary<string, int> categories = new Dictionary<string, int>();
            int highest = 0;
            float previous = -1f;

            for (int i = 0; i < events.Count; i++)
            {
                GameEventData gameEvent = events[i];
                if (gameEvent == null)
                    continue;

                string category = string.IsNullOrEmpty(gameEvent.Category) ? "Unknown" : gameEvent.Category;
                int count;
                categories.TryGetValue(category, out count);
                categories[category] = ++count;

                if (count > highest)
                {
                    highest = count;
                    report.MostFrequentCategory = category;
                }

                if (previous >= 0f)
                    report.LongestEventGap = Mathf.Max(report.LongestEventGap, gameEvent.Timestamp - previous);

                previous = gameEvent.Timestamp;
            }
        }

        private static string FormatEvent(GameEventData gameEvent)
        {
            return gameEvent == null ? string.Empty : gameEvent.Timestamp.ToString("F2") + "s " + gameEvent;
        }

        // =====================================================
        // ATTENTION
        // =====================================================

        private void AnalyzeAttention(SessionData session, BehaviourReport report)
        {
            Dictionary<string, TargetAttention> targets = new Dictionary<string, TargetAttention>();

            for (int i = 0; i < session.BehaviourObservations.Count; i++)
            {
                BehaviourObservation observation = session.BehaviourObservations[i];
                if (observation == null || !observation.Attended)
                    continue;

                float duration = Mathf.Max(0f, observation.Duration);

                report.AttentionCount++;
                report.TotalAttentionTime += duration;
                report.LongestAttentionDuration = Mathf.Max(report.LongestAttentionDuration, duration);

                if (duration < _settings.briefAttentionThreshold)
                    report.BriefAttentionCount++;
                else if (duration >= _settings.sustainedAttentionThreshold)
                    report.SustainedAttentionCount++;

                string key = observation.Key ?? string.Empty;
                TargetAttention target;

                if (!targets.TryGetValue(key, out target))
                {
                    target = new TargetAttention
                    {
                        TargetId = key,
                        Name = string.IsNullOrEmpty(observation.Subject) ? "Unknown" : observation.Subject,
                        Category = observation.Category ?? string.Empty,
                        FirstSeen = observation.Timestamp,
                        Position = observation.Position
                    };
                    targets.Add(key, target);
                }
                else
                {
                    // Older sessions have no IsReinspection flag; a repeat key is a reinspection.
                    if (session.Version < 2)
                        report.ReinspectionCount++;
                }

                if (session.Version >= 2 && observation.IsReinspection)
                    report.ReinspectionCount++;

                target.Count++;
                target.TotalDuration += duration;
                target.LongestDuration = Mathf.Max(target.LongestDuration, duration);
            }

            report.UniqueAttendedObjects = targets.Count;
            report.AverageAttentionDuration = report.AttentionCount > 0
                ? report.TotalAttentionTime / report.AttentionCount
                : 0f;

            report.Targets = new List<TargetAttention>(targets.Values);
            report.Targets.Sort((a, b) => b.TotalDuration.CompareTo(a.TotalDuration));
        }

        // =====================================================
        // FINDINGS
        // =====================================================

        private void BuildFindings(SessionData session, BehaviourReport report)
        {
            List<string> findings = report.Findings;
            findings.Clear();

            if (session.Duration <= 0f)
                return;

            // Movement
            if (report.ActivePercentage >= 80f)
                findings.Add("The player stayed highly active, moving during " + report.ActivePercentage.ToString("F0") + "% of the session.");
            else if (report.ActivePercentage <= 35f)
                findings.Add("The player moved very little: only " + report.ActivePercentage.ToString("F0") + "% of the session was active movement.");
            else
                findings.Add("The player mixed moving and stopping, with " + report.ActivePercentage.ToString("F0") + "% of the session spent moving.");

            if (report.IdleTime >= 5f)
                findings.Add("The player spent about " + report.IdleTime.ToString("F1") + " seconds standing still.");

            if (report.PeakSpeed > 0f)
                findings.Add("Peak speed was " + report.PeakSpeed.ToString("F2") + " m/s against an average of " + report.AverageSpeed.ToString("F2") + " m/s.");

            if (report.MovementSegments >= 5)
                findings.Add("Movement broke into " + report.MovementSegments + " separate segments, suggesting repeated stops while exploring.");
            else if (report.MovementSegments <= 1)
                findings.Add("Movement was mostly continuous, with few starts and stops.");

            // Events
            if (report.EventCount == 0)
            {
                findings.Add("No gameplay events were recorded. Log key moments with PlaySenseEvents.Log to get interaction analysis.");
            }
            else
            {
                findings.Add(report.EventCount + " gameplay event(s) were recorded; the most common category was \"" + report.MostFrequentCategory + "\".");

                if (report.LongestEventGap >= 10f)
                    findings.Add("The longest gap between events was " + report.LongestEventGap.ToString("F1") + " seconds.");
            }

            // Attention
            if (report.AttentionCount == 0)
            {
                findings.Add("No attention was recorded. Add PlaySenseTarget to the objects you care about and a PlaySenseView to the camera.");
                return;
            }

            findings.Add(report.AttentionCount + " attention period(s) across " + report.UniqueAttendedObjects +
                         " object(s), " + report.TotalAttentionTime.ToString("F1") + " seconds in total (average " +
                         report.AverageAttentionDuration.ToString("F1") + " s).");

            if (report.Targets.Count > 0)
            {
                TargetAttention top = report.Targets[0];
                findings.Add("\"" + top.Name + "\" held attention longest: " + top.TotalDuration.ToString("F1") + " s over " + top.Count + " look(s).");
            }

            if (report.SustainedAttentionCount > 0)
                findings.Add(report.SustainedAttentionCount + " attention period(s) lasted " + _settings.sustainedAttentionThreshold.ToString("F0") + " s or more, indicating sustained interest.");

            if (report.BriefAttentionCount > 0 && report.BriefAttentionCount >= report.AttentionCount / 2f)
                findings.Add("Most attention was brief (under " + _settings.briefAttentionThreshold.ToString("F0") + " s), which reads as scanning rather than studying.");

            if (report.ReinspectionCount > 0)
                findings.Add("The player returned to objects they had already looked at " + report.ReinspectionCount + " time(s).");
        }
    }
}
