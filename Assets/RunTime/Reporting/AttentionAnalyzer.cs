using System.Collections.Generic;
using PlaySense.Data.Models;

namespace PlaySense.Reporting
{
    public class AttentionAnalyzer
    {
        // =====================================================
        // PUBLIC API
        // =====================================================

        public List<AttentionData> Analyze(
            SessionData session)
        {
            List<AttentionData> results =
                new List<AttentionData>();

            if (session == null)
                return results;

            if (session.BehaviourObservations == null ||
                session.BehaviourObservations.Count == 0)
            {
                return results;
            }

            Dictionary<string, AttentionData> lookup =
                new Dictionary<string, AttentionData>();

            foreach (
                BehaviourObservation observation
                in session.BehaviourObservations)
            {
                if (observation == null)
                    continue;

                string target =
                    string.IsNullOrEmpty(
                        observation.Subject)
                        ? "Unknown"
                        : observation.Subject;

                if (!lookup.TryGetValue(
                    target,
                    out AttentionData attention))
                {
                    attention =
                        CreateAttentionData(
                            target,
                            observation);

                    lookup.Add(
                        target,
                        attention);

                    results.Add(
                        attention);
                }

                ProcessObservation(
                    attention,
                    observation);
            }

            FinalizeResults(
                results);

            return results;
        }

        // =====================================================
        // CREATE
        // =====================================================

        private AttentionData CreateAttentionData(
            string target,
            BehaviourObservation observation)
        {
            AttentionData data =
                new AttentionData();

            data.Target =
                target;

            data.AttentionCount = 0;

            data.TotalDuration = 0f;

            data.LongestDuration = 0f;

            data.FirstSeenTime =
                observation.Timestamp;

            data.LastSeenTime =
                observation.Timestamp;

            data.DistanceAccumulator = 0f;

            data.AngleAccumulator = 0f;

            data.SampleCount = 0;

            return data;
        }

        // =====================================================
        // PROCESS
        // =====================================================

        private void ProcessObservation(
            AttentionData attention,
            BehaviourObservation observation)
        {
            // Every BehaviourObservation represents
            // one distinct attention episode.
            attention.AttentionCount++;

            attention.TotalDuration +=
                observation.Duration;

            if (observation.Duration >
                attention.LongestDuration)
            {
                attention.LongestDuration =
                    observation.Duration;
            }

            if (observation.Timestamp <
                attention.FirstSeenTime)
            {
                attention.FirstSeenTime =
                    observation.Timestamp;
            }

            float observationEnd =
                observation.Timestamp +
                observation.Duration;

            if (observationEnd >
                attention.LastSeenTime)
            {
                attention.LastSeenTime =
                    observationEnd;
            }

            attention.DistanceAccumulator +=
                observation.Distance;

            attention.AngleAccumulator +=
                observation.Angle;

            attention.SampleCount++;
        }

        // =====================================================
        // FINALIZE
        // =====================================================

        private void FinalizeResults(
            List<AttentionData> results)
        {
            foreach (
                AttentionData attention
                in results)
            {
                if (attention == null)
                    continue;

                if (attention.SampleCount > 0)
                {
                    attention.AverageDistance =
                        attention.DistanceAccumulator /
                        attention.SampleCount;

                    attention.AverageAngle =
                        attention.AngleAccumulator /
                        attention.SampleCount;
                }
                else
                {
                    attention.AverageDistance = 0f;
                    attention.AverageAngle = 0f;
                }
            }
        }
    }
}