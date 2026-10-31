using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

using PlaySense.Data.Models;
using PlaySense.Analysis;

namespace PlaySense.EditorTools
{
    public static class PlaySensePDFExporter
    {
        private const float PageWidth = 595f;
        private const float PageHeight = 842f;

        private const float MarginLeft = 48f;
        private const float MarginTop = 55f;
        private const float MarginBottom = 55f;

        private const float FontSize = 10f;
        private const float LineHeight = 15f;

        // =====================================================
        // PUBLIC EXPORT
        // =====================================================

        public static string Export(
            SessionData session,
            string outputPath)
        {
            if (session == null)
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: SessionData is null.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: output path is empty.");
                return null;
            }

            try
            {
                outputPath = Path.GetFullPath(outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[PlaySense] PDF Export failed: invalid output path.\n{exception}");
                return null;
            }

            string directory = Path.GetDirectoryName(outputPath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: could not determine output directory.");
                return null;
            }

            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[PlaySense] PDF Export failed: could not create output directory.\n{exception}");
                return null;
            }

            SessionBehaviourAnalyzer analyzer =
                new SessionBehaviourAnalyzer();

            BehaviourReport behaviour =
                analyzer.Analyze(session);

            if (behaviour == null)
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: behavioural analysis returned null.");
                return null;
            }

            List<string> pages =
                BuildPages(session, behaviour);

            if (pages == null || pages.Count == 0)
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: no pages were generated.");
                return null;
            }

            string pdf = BuildPdf(pages);

            if (string.IsNullOrWhiteSpace(pdf))
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: generated PDF was empty.");
                return null;
            }

            try
            {
                File.WriteAllBytes(
                    outputPath,
                    Encoding.ASCII.GetBytes(pdf));
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[PlaySense] PDF Export failed while writing file:\n{exception}");
                return null;
            }

            Debug.Log(
                $"[PlaySense] PDF Report Generated:\n{outputPath}");

            return outputPath;
        }

        // =====================================================
        // PAGE BUILDING
        // =====================================================

        private static List<string> BuildPages(
            SessionData session,
            BehaviourReport behaviour)
        {
            List<string> pages =
                new List<string>();

            StringBuilder page =
                CreatePage();

            float y =
                PageHeight - MarginTop;

            // =================================================
            // HEADER
            // =================================================

            AddText(
                page,
                ref y,
                "PLAYSENSE PLAYTEST REPORT",
                20f,
                true);

            y -= 4f;

            AddText(
                page,
                ref y,
                "Gameplay analytics and behavioural analysis",
                11f,
                false);

            y -= 18f;

            // =================================================
            // SESSION SUMMARY
            // =================================================

            AddSectionTitle(
                page,
                ref y,
                "SESSION SUMMARY");

            AddText(
                page,
                ref y,
                $"Scene: {Safe(session.SceneName, "Unknown")}",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Session ID: {Safe(session.SessionId, "Unknown")}",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Duration: {GetSessionDuration(session):F2} seconds",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Distance travelled: {session.Metrics.TotalDistance:F2} m",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Average speed: {session.Metrics.AverageSpeed:F2} m/s",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Frames recorded: {session.Metrics.FrameCount}",
                FontSize,
                false);

            int eventCount =
                session.Events == null
                    ? 0
                    : session.Events.Count;

            int observationCount =
                session.BehaviourObservations == null
                    ? 0
                    : session.BehaviourObservations.Count;

            AddText(
                page,
                ref y,
                $"Gameplay events: {eventCount}",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Behaviour observations: {observationCount}",
                FontSize,
                false);

            y -= 14f;

            // =================================================
            // BEHAVIOURAL ANALYSIS
            // =================================================

            AddSectionTitle(
                page,
                ref y,
                "BEHAVIOURAL ANALYSIS");

            AddText(
                page,
                ref y,
                $"Active time: {behaviour.ActiveTime:F2} seconds",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Idle time: {behaviour.IdleTime:F2} seconds",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Active percentage: {behaviour.ActivePercentage:F1}%",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Idle percentage: {behaviour.IdlePercentage:F1}%",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Peak speed: {behaviour.PeakSpeed:F2} m/s",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Movement segments: {behaviour.MovementSegments}",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Most frequent event category: {Safe(behaviour.MostFrequentCategory, "None")}",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Longest event gap: {behaviour.LongestEventGap:F2} seconds",
                FontSize,
                false);

            y -= 10f;

            // =================================================
            // ATTENTION ANALYSIS
            // =================================================

            EnsureSpace(
                ref page,
                ref y,
                pages,
                125f);

            AddSectionTitle(
                page,
                ref y,
                "ATTENTION ANALYSIS");

            AddText(
                page,
                ref y,
                $"Attention count: {behaviour.AttentionCount}",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Total attention time: {behaviour.TotalAttentionTime:F2} seconds",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Average attention duration: {behaviour.AverageAttentionDuration:F2} seconds",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Longest attention duration: {behaviour.LongestAttentionDuration:F2} seconds",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Unique attended objects: {behaviour.UniqueAttendedObjects}",
                FontSize,
                false);

            AddText(
                page,
                ref y,
                $"Reinspection count: {behaviour.ReinspectionCount}",
                FontSize,
                false);

            y -= 12f;

            // =================================================
            // MOVEMENT CHART
            // =================================================

            EnsureSpace(
                ref page,
                ref y,
                pages,
                175f);

            AddSectionTitle(
                page,
                ref y,
                "MOVEMENT OVER TIME");

            DrawMovementChart(
                page,
                ref y,
                session);

            y -= 14f;

            // =================================================
            // ATTENTION CHART
            // =================================================

            EnsureSpace(
                ref page,
                ref y,
                pages,
                175f);

            AddSectionTitle(
                page,
                ref y,
                "ATTENTION OVER TIME");

            DrawAttentionChart(
                page,
                ref y,
                session);

            y -= 14f;

            // =================================================
            // KEY FINDINGS
            // =================================================

            AddSectionTitle(
                page,
                ref y,
                "KEY FINDINGS");

            if (behaviour.Findings == null ||
                behaviour.Findings.Count == 0)
            {
                AddText(
                    page,
                    ref y,
                    "No behavioural findings were generated.",
                    FontSize,
                    false);
            }
            else
            {
                foreach (string finding in behaviour.Findings)
                {
                    if (string.IsNullOrWhiteSpace(finding))
                        continue;

                    List<string> wrapped =
                        WrapText(finding, 84);

                    bool firstLine = true;

                    foreach (string line in wrapped)
                    {
                        EnsureSpace(
                            ref page,
                            ref y,
                            pages,
                            30f);

                        AddText(
                            page,
                            ref y,
                            firstLine ? "- " + line : "  " + line,
                            FontSize,
                            false);

                        firstLine = false;
                    }

                    y -= 3f;
                }
            }

            // =================================================
            // ATTENTION FINDINGS
            // =================================================

            BuildAttentionFindings(
                page,
                ref y,
                pages,
                behaviour);

            y -= 10f;

            // =================================================
            // GAMEPLAY TIMELINE
            // =================================================

            AddSectionTitle(
                page,
                ref y,
                "GAMEPLAY TIMELINE");

            if (eventCount == 0)
            {
                AddText(
                    page,
                    ref y,
                    "No gameplay events were recorded.",
                    FontSize,
                    false);
            }
            else
            {
                for (int i = 0;
                     i < session.Events.Count;
                     i++)
                {
                    GameEventData gameEvent =
                        session.Events[i];

                    if (gameEvent == null)
                        continue;

                    string category =
                        Safe(gameEvent.Category, "Event");

                    string action =
                        Safe(gameEvent.Action, "Unknown action");

                    string target =
                        Safe(gameEvent.Target, "");

                    string line =
                        $"{gameEvent.Timestamp:F2}s   " +
                        $"{category}: {action}";

                    if (!string.IsNullOrEmpty(target))
                        line += $" - {target}";

                    foreach (string wrappedLine in
                             WrapText(line, 88))
                    {
                        EnsureSpace(
                            ref page,
                            ref y,
                            pages,
                            30f);

                        AddText(
                            page,
                            ref y,
                            wrappedLine,
                            FontSize,
                            false);
                    }
                }
            }

            y -= 18f;

            // =================================================
            // MOST ATTENDED OBJECTS
            // =================================================

            if (behaviour.Targets != null && behaviour.Targets.Count > 0)
            {
                EnsureSpace(
                    ref page,
                    ref y,
                    pages,
                    120f);

                AddSectionTitle(
                    page,
                    ref y,
                    "MOST ATTENDED OBJECTS");

                int shown = Mathf.Min(behaviour.Targets.Count, 10);

                for (int i = 0; i < shown; i++)
                {
                    TargetAttention target = behaviour.Targets[i];

                    EnsureSpace(
                        ref page,
                        ref y,
                        pages,
                        30f);

                    AddText(
                        page,
                        ref y,
                        $"{i + 1}. {Safe(target.Name, "Unknown")}" +
                        (string.IsNullOrEmpty(target.Category) ? "" : $" ({target.Category})") +
                        $"   {target.TotalDuration:F1}s total, {target.Count} look(s), " +
                        $"longest {target.LongestDuration:F1}s, first at {target.FirstSeen:F1}s",
                        FontSize,
                        false);
                }

                y -= 18f;
            }

            // =================================================
            // BEHAVIOURAL TIMELINE
            // =================================================

            EnsureSpace(
                ref page,
                ref y,
                pages,
                150f);

            AddSectionTitle(
                page,
                ref y,
                "BEHAVIOURAL TIMELINE");

            if (observationCount == 0)
            {
                AddText(
                    page,
                    ref y,
                    "No behavioural observations were recorded.",
                    FontSize,
                    false);
            }
            else
            {
                for (int i = 0;
                     i < session.BehaviourObservations.Count;
                     i++)
                {
                    BehaviourObservation observation =
                        session.BehaviourObservations[i];

                    if (observation == null)
                        continue;

                    EnsureSpace(
                        ref page,
                        ref y,
                        pages,
                        95f);

                    AddText(
                        page,
                        ref y,
                        $"{observation.Timestamp:F2}s   {Safe(observation.Type, "Behaviour")}",
                        FontSize,
                        true);

                    AddText(
                        page,
                        ref y,
                        $"Target: {Safe(observation.Subject, "Unknown object")}",
                        FontSize,
                        false);

                    AddText(
                        page,
                        ref y,
                        $"Duration: {observation.Duration:F2}s   " +
                        $"Distance: {observation.Distance:F2}m   " +
                        $"View angle: {observation.Angle:F1} deg",
                        FontSize,
                        false);

                    AddText(
                        page,
                        ref y,
                        $"Category: {Safe(observation.Category, "-")}   " +
                        $"View: {Safe(observation.ViewName, "-")}   " +
                        $"Reinspection: {(observation.IsReinspection ? "Yes" : "No")}",
                        FontSize,
                        false);

                    string description =
                        BuildObservationDescription(
                            observation);

                    foreach (string line in
                             WrapText(description, 88))
                    {
                        EnsureSpace(
                            ref page,
                            ref y,
                            pages,
                            30f);

                        AddText(
                            page,
                            ref y,
                            line,
                            FontSize,
                            false);
                    }

                    y -= 6f;
                }
            }

            // =================================================
            // BEHAVIOURAL CONTEXT
            // =================================================

            EnsureSpace(
                ref page,
                ref y,
                pages,
                140f);

            AddSectionTitle(
                page,
                ref y,
                "BEHAVIOURAL CONTEXT");

            string behaviourContext =
                BuildBehaviourContext(
                    behaviour);

            foreach (string line in
                     WrapText(behaviourContext, 88))
            {
                EnsureSpace(
                    ref page,
                    ref y,
                    pages,
                    30f);

                AddText(
                    page,
                    ref y,
                    line,
                    FontSize,
                    false);
            }

            // =================================================
            // FOOTER
            // =================================================

            EnsureSpace(
                ref page,
                ref y,
                pages,
                45f);

            AddText(
                page,
                ref y,
                "Generated automatically by PlaySense.",
                8f,
                false);

            AddText(
                page,
                ref y,
                "This report is based on recorded gameplay telemetry.",
                8f,
                false);

            FinishPage(page);

            pages.Add(
                page.ToString());

            return pages;
        }

        // =====================================================
        // MOVEMENT CHART
        // =====================================================

        private static void DrawMovementChart(
            StringBuilder page,
            ref float y,
            SessionData session)
        {
            List<FrameData> frames =
                SessionQueries.GetPrimaryFrames(session);

            if (frames.Count < 2)
            {
                AddText(
                    page,
                    ref y,
                    "Not enough movement data was recorded to generate a chart.",
                    FontSize,
                    false);
                return;
            }

            float chartX =
                MarginLeft + 35f;

            float chartY =
                y - 110f;

            float chartWidth =
                PageWidth -
                MarginLeft -
                80f;

            float chartHeight = 105f;

            List<float> times =
                new List<float>();

            List<float> speeds =
                new List<float>();

            for (int i = 1;
                 i < frames.Count;
                 i++)
            {
                FrameData previous =
                    frames[i - 1];

                FrameData current =
                    frames[i];

                if (previous == null ||
                    current == null)
                    continue;

                float deltaTime =
                    current.Timestamp -
                    previous.Timestamp;

                if (deltaTime <= 0f)
                    continue;

                Vector2 previousXZ =
                    new Vector2(
                        previous.Position.x,
                        previous.Position.z);

                Vector2 currentXZ =
                    new Vector2(
                        current.Position.x,
                        current.Position.z);

                float distance =
                    Vector2.Distance(
                        previousXZ,
                        currentXZ);

                float speed =
                    distance / deltaTime;

                if (float.IsNaN(speed) ||
                    float.IsInfinity(speed))
                    continue;

                times.Add(current.Timestamp);
                speeds.Add(speed);
            }

            if (speeds.Count < 2)
            {
                AddText(
                    page,
                    ref y,
                    "Not enough valid movement samples were recorded to generate a chart.",
                    FontSize,
                    false);
                return;
            }

            float minTime =
                times[0];

            float maxTime =
                times[times.Count - 1];

            float maxSpeed = 0f;

            for (int i = 0;
                 i < speeds.Count;
                 i++)
            {
                maxSpeed =
                    Mathf.Max(
                        maxSpeed,
                        speeds[i]);
            }

            if (maxSpeed <= 0f)
                maxSpeed = 1f;

            maxSpeed *= 1.10f;

            float timeRange =
                maxTime - minTime;

            if (timeRange <= 0f)
                timeRange = 1f;

            DrawChartBackground(
                page,
                chartX,
                chartY,
                chartWidth,
                chartHeight);

            DrawChartGrid(
                page,
                chartX,
                chartY,
                chartWidth,
                chartHeight);

            DrawChartAxes(
                page,
                chartX,
                chartY,
                chartWidth,
                chartHeight);

            bool firstPoint = true;

            for (int i = 0;
                 i < speeds.Count;
                 i++)
            {
                float normalizedX =
                    (times[i] - minTime) /
                    timeRange;

                float normalizedY =
                    speeds[i] /
                    maxSpeed;

                float pointX =
                    chartX +
                    normalizedX *
                    chartWidth;

                float pointY =
                    chartY +
                    normalizedY *
                    chartHeight;

                if (firstPoint)
                {
                    page.AppendLine(
                        $"{pointX:F1} {pointY:F1} m");

                    firstPoint = false;
                }
                else
                {
                    page.AppendLine(
                        $"{pointX:F1} {pointY:F1} l");
                }
            }

            page.AppendLine("S");

            AddChartLabel(
                page,
                chartX - 32f,
                chartY + chartHeight - 3f,
                $"{maxSpeed:F1} m/s");

            AddChartLabel(
                page,
                chartX - 10f,
                chartY - 3f,
                "0");

            AddChartLabel(
                page,
                chartX,
                chartY - 16f,
                $"{minTime:F1}s");

            AddChartLabel(
                page,
                chartX + chartWidth - 25f,
                chartY - 16f,
                $"{maxTime:F1}s");

            AddChartLabel(
                page,
                chartX + chartWidth / 2f - 20f,
                chartY - 30f,
                "Time (s)");

            AddChartLabel(
                page,
                chartX - 5f,
                chartY + chartHeight + 8f,
                "Speed");

            y =
                chartY - 38f;
        }

        // =====================================================
        // ATTENTION CHART
        // =====================================================

        private static void DrawAttentionChart(
            StringBuilder page,
            ref float y,
            SessionData session)
        {
            if (session == null ||
                session.BehaviourObservations == null ||
                session.BehaviourObservations.Count == 0)
            {
                AddText(
                    page,
                    ref y,
                    "No behavioural observations were recorded to generate an attention chart.",
                    FontSize,
                    false);
                return;
            }

            List<BehaviourObservation> observations =
                new List<BehaviourObservation>();

            float minTime =
                float.MaxValue;

            float maxTime =
                float.MinValue;

            float maxDuration = 0f;

            for (int i = 0;
                 i < session.BehaviourObservations.Count;
                 i++)
            {
                BehaviourObservation observation =
                    session.BehaviourObservations[i];

                if (observation == null ||
                    !observation.Attended)
                    continue;

                observations.Add(observation);

                minTime =
                    Mathf.Min(
                        minTime,
                        observation.Timestamp);

                maxTime =
                    Mathf.Max(
                        maxTime,
                        observation.Timestamp +
                        Mathf.Max(0f, observation.Duration));

                maxDuration =
                    Mathf.Max(
                        maxDuration,
                        Mathf.Max(0f, observation.Duration));
            }

            if (observations.Count == 0)
            {
                AddText(
                    page,
                    ref y,
                    "No attended observations were recorded to generate an attention chart.",
                    FontSize,
                    false);
                return;
            }

            if (maxTime <= minTime)
                maxTime = minTime + 1f;

            if (maxDuration <= 0f)
                maxDuration = 1f;

            float chartX =
                MarginLeft + 35f;

            float chartY =
                y - 110f;

            float chartWidth =
                PageWidth -
                MarginLeft -
                80f;

            float chartHeight = 105f;

            DrawChartBackground(
                page,
                chartX,
                chartY,
                chartWidth,
                chartHeight);

            DrawChartGrid(
                page,
                chartX,
                chartY,
                chartWidth,
                chartHeight);

            DrawChartAxes(
                page,
                chartX,
                chartY,
                chartWidth,
                chartHeight);

            // -------------------------------------------------
            // Attention duration bars
            // -------------------------------------------------

            for (int i = 0;
                 i < observations.Count;
                 i++)
            {
                BehaviourObservation observation =
                    observations[i];

                float start =
                    observation.Timestamp;

                float duration =
                    Mathf.Max(
                        0f,
                        observation.Duration);

                float end =
                    start + duration;

                float normalizedStart =
                    Mathf.Clamp01(
                        (start - minTime) /
                        (maxTime - minTime));

                float normalizedEnd =
                    Mathf.Clamp01(
                        (end - minTime) /
                        (maxTime - minTime));

                float barX =
                    chartX +
                    normalizedStart *
                    chartWidth;

                float barWidth =
                    Mathf.Max(
                        2f,
                        (normalizedEnd -
                         normalizedStart) *
                        chartWidth);

                float barY =
                    chartY +
                    15f +
                    (i % 5) * 16f;

                page.AppendLine("q");
                page.AppendLine("0.35 g");
                page.AppendLine(
                    $"{barX:F1} {barY:F1} " +
                    $"{Mathf.Min(barWidth, chartWidth):F1} 10 re");
                page.AppendLine("f");
                page.AppendLine("Q");
            }

            AddChartLabel(
                page,
                chartX - 32f,
                chartY + chartHeight - 3f,
                $"{maxDuration:F1}s");

            AddChartLabel(
                page,
                chartX - 10f,
                chartY - 3f,
                "0");

            AddChartLabel(
                page,
                chartX,
                chartY - 16f,
                $"{minTime:F1}s");

            AddChartLabel(
                page,
                chartX + chartWidth - 25f,
                chartY - 16f,
                $"{maxTime:F1}s");

            AddChartLabel(
                page,
                chartX + chartWidth / 2f - 20f,
                chartY - 30f,
                "Time (s)");

            AddChartLabel(
                page,
                chartX - 5f,
                chartY + chartHeight + 8f,
                "Attention");

            y =
                chartY - 38f;
        }

        private static void DrawChartBackground(
            StringBuilder page,
            float chartX,
            float chartY,
            float chartWidth,
            float chartHeight)
        {
            page.AppendLine("q");
            page.AppendLine("0.95 g");

            page.AppendLine(
                $"{chartX:F1} {chartY:F1} " +
                $"{chartWidth:F1} {chartHeight:F1} re");

            page.AppendLine("f");
            page.AppendLine("Q");
        }

        private static void DrawChartGrid(
            StringBuilder page,
            float chartX,
            float chartY,
            float chartWidth,
            float chartHeight)
        {
            for (int i = 0; i <= 4; i++)
            {
                float gridY =
                    chartY +
                    (chartHeight / 4f) * i;

                page.AppendLine(
                    $"{chartX:F1} {gridY:F1} m " +
                    $"{chartX + chartWidth:F1} {gridY:F1} l S");
            }

            for (int i = 0; i <= 4; i++)
            {
                float gridX =
                    chartX +
                    (chartWidth / 4f) * i;

                page.AppendLine(
                    $"{gridX:F1} {chartY:F1} m " +
                    $"{gridX:F1} {chartY + chartHeight:F1} l S");
            }
        }

        private static void DrawChartAxes(
            StringBuilder page,
            float chartX,
            float chartY,
            float chartWidth,
            float chartHeight)
        {
            page.AppendLine(
                $"{chartX:F1} {chartY:F1} m " +
                $"{chartX + chartWidth:F1} {chartY:F1} l S");

            page.AppendLine(
                $"{chartX:F1} {chartY:F1} m " +
                $"{chartX:F1} {chartY + chartHeight:F1} l S");
        }

        private static void AddChartLabel(
            StringBuilder page,
            float x,
            float y,
            string text)
        {
            string safeText =
                EscapePdfText(text);

            page.AppendLine(
                $"BT /F1 7 Tf " +
                $"{x:F1} {y:F1} Td " +
                $"({safeText}) Tj ET");
        }

        // =====================================================
        // ATTENTION FINDINGS
        // =====================================================

        private static void BuildAttentionFindings(
            StringBuilder page,
            ref float y,
            List<string> pages,
            BehaviourReport behaviour)
        {
            if (behaviour == null)
                return;

            EnsureSpace(
                ref page,
                ref y,
                pages,
                100f);

            AddSectionTitle(
                page,
                ref y,
                "ATTENTION FINDINGS");

            if (behaviour.AttentionCount <= 0)
            {
                AddText(
                    page,
                    ref y,
                    "No attended objects were recorded.",
                    FontSize,
                    false);

                return;
            }

            string finding =
                $"The player generated {behaviour.AttentionCount} attention observation(s), " +
                $"covering {behaviour.TotalAttentionTime:F2} seconds of recorded attention.";

            AddWrappedFinding(
                page,
                ref y,
                pages,
                finding);

            if (behaviour.UniqueAttendedObjects > 0)
            {
                AddWrappedFinding(
                    page,
                    ref y,
                    pages,
                    $"Attention was distributed across {behaviour.UniqueAttendedObjects} unique object(s).");
            }

            if (behaviour.ReinspectionCount > 0)
            {
                AddWrappedFinding(
                    page,
                    ref y,
                    pages,
                    $"The player revisited previously attended content {behaviour.ReinspectionCount} time(s), indicating repeated inspection.");
            }

            if (behaviour.LongestAttentionDuration >= 3f)
            {
                AddWrappedFinding(
                    page,
                    ref y,
                    pages,
                    $"The longest recorded attention episode lasted {behaviour.LongestAttentionDuration:F2} seconds, indicating sustained visual attention.");
            }
            else
            {
                AddWrappedFinding(
                    page,
                    ref y,
                    pages,
                    "Attention episodes were generally brief, with no sustained attention episode reaching three seconds.");
            }
        }

        private static void AddWrappedFinding(
            StringBuilder page,
            ref float y,
            List<string> pages,
            string text)
        {
            bool firstLine = true;

            foreach (string line in WrapText(text, 84))
            {
                EnsureSpace(
                    ref page,
                    ref y,
                    pages,
                    30f);

                AddText(
                    page,
                    ref y,
                    firstLine ? "- " + line : "  " + line,
                    FontSize,
                    false);

                firstLine = false;
            }

            y -= 3f;
        }

        // =====================================================
        // BEHAVIOURAL CONTEXT
        // =====================================================

        private static string BuildBehaviourContext(
            BehaviourReport behaviour)
        {
            if (behaviour == null)
                return "Behavioural analysis was unavailable.";

            StringBuilder result =
                new StringBuilder();

            if (behaviour.ActivePercentage >= 80f)
            {
                result.Append(
                    "The player demonstrated a highly active movement pattern during the session. ");
            }
            else if (behaviour.ActivePercentage <= 35f)
            {
                result.Append(
                    "The player demonstrated a relatively low-movement pattern, with substantial periods of minimal movement. ");
            }
            else
            {
                result.Append(
                    "The player demonstrated a mixed movement pattern, alternating between active movement and periods of minimal movement. ");
            }

            if (behaviour.MovementSegments >= 5)
            {
                result.Append(
                    $"Movement was divided into {behaviour.MovementSegments} distinct segments, indicating repeated starts and stops. ");
            }
            else if (behaviour.MovementSegments <= 1)
            {
                result.Append(
                    "Movement was relatively continuous, with few distinct starts and stops. ");
            }

            if (behaviour.AttentionCount > 0)
            {
                result.Append(
                    $"{behaviour.AttentionCount} attention observation(s) were recorded, " +
                    $"covering {behaviour.TotalAttentionTime:F2} seconds");

                if (behaviour.UniqueAttendedObjects > 0)
                {
                    result.Append(
                        $" across {behaviour.UniqueAttendedObjects} unique object(s)");
                }

                result.Append(". ");

                if (behaviour.ReinspectionCount > 0)
                {
                    result.Append(
                        $"The player returned to previously attended content {behaviour.ReinspectionCount} time(s). ");
                }
            }
            else
            {
                result.Append(
                    "No attended objects were recorded, so attention-level behavioural analysis is limited. ");
            }

            if (behaviour.EventCount > 0)
            {
                result.Append(
                    $"{behaviour.EventCount} gameplay event(s) were recorded");

                if (!string.IsNullOrEmpty(
                    behaviour.MostFrequentCategory) &&
                    behaviour.MostFrequentCategory != "None")
                {
                    result.Append(
                        $", with \"{behaviour.MostFrequentCategory}\" being the most frequent category");
                }

                result.Append(".");
            }
            else
            {
                result.Append(
                    "No gameplay events were recorded, so interaction-level behavioural analysis is limited.");
            }

            return result.ToString();
        }

        // =====================================================
        // OBSERVATION DESCRIPTION
        // =====================================================

        private static string BuildObservationDescription(
            BehaviourObservation observation)
        {
            if (observation == null)
                return "Behavioural observation was unavailable.";

            string subject =
                Safe(
                    observation.Subject,
                    "the object");

            if (observation.Attended)
            {
                return
                    $"The player visually attended to {subject} " +
                    $"for {observation.Duration:F2} seconds. " +
                    $"The object was approximately {observation.Distance:F2} metres away " +
                    $"{observation.Angle:F1} degrees from the centre of view when attention began.";
            }

            return
                $"PlaySense recorded {Safe(observation.Type, "a behavioural observation")} " +
                $"involving {subject} for {observation.Duration:F2} seconds.";
        }

        // =====================================================
        // SESSION HELPERS
        // =====================================================

        private static float GetSessionDuration(
            SessionData session)
        {
            if (session == null)
                return 0f;

            if (session.Duration > 0f)
                return session.Duration;

            if (session.Metrics.Duration > 0f)
                return session.Metrics.Duration;

            return 0f;
        }

        // =====================================================
        // SECTION TITLES
        // =====================================================

        private static void AddSectionTitle(
            StringBuilder page,
            ref float y,
            string title)
        {
            y -= 8f;

            AddText(
                page,
                ref y,
                title,
                12f,
                true);

            y -= 5f;
        }

        // =====================================================
        // TEXT
        // =====================================================

        private static void AddText(
            StringBuilder page,
            ref float y,
            string text,
            float fontSize,
            bool bold)
        {
            string safeText =
                EscapePdfText(text);

            string fontName =
                "/F1";

            page.AppendLine(
                $"BT {fontName} {fontSize:F1} Tf " +
                $"{MarginLeft:F1} {y:F1} Td " +
                $"({safeText}) Tj ET");

            y -= LineHeight;
        }

        // =====================================================
        // PAGE MANAGEMENT
        // =====================================================

        private static void EnsureSpace(
            ref StringBuilder page,
            ref float y,
            List<string> pages,
            float requiredSpace)
        {
            if (y >= MarginBottom + requiredSpace)
                return;

            FinishPage(page);

            pages.Add(
                page.ToString());

            page =
                CreatePage();

            y =
                PageHeight - MarginTop;

            AddText(
                page,
                ref y,
                "PLAYSENSE PLAYTEST REPORT",
                14f,
                true);

            y -= 8f;

            AddText(
                page,
                ref y,
                "CONTINUED",
                11f,
                true);

            y -= 10f;
        }

        private static StringBuilder CreatePage()
        {
            return new StringBuilder();
        }

        private static void FinishPage(
            StringBuilder page)
        {
            page.AppendLine(
                "BT /F1 8 Tf 48 28 Td " +
                "(Generated by PlaySense) Tj ET");
        }

        // =====================================================
        // TEXT HELPERS
        // =====================================================

        private static string EscapePdfText(
            string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            StringBuilder result =
                new StringBuilder();

            foreach (char c in text)
            {
                switch (c)
                {
                    case '\\':
                        result.Append("\\\\");
                        break;

                    case '(':
                        result.Append("\\(");
                        break;

                    case ')':
                        result.Append("\\)");
                        break;

                    case '\r':
                    case '\n':
                    case '\t':
                        result.Append(" ");
                        break;

                    case '—':
                    case '–':
                        result.Append("-");
                        break;

                    case '“':
                    case '”':
                        result.Append("\"");
                        break;

                    case '‘':
                    case '’':
                        result.Append("'");
                        break;

                    case '•':
                        result.Append("-");
                        break;

                    case '→':
                    case '←':
                    case '⇒':
                    case '⇐':
                        result.Append(">");
                        break;

                    case '°':
                        result.Append(" deg");
                        break;

                    default:
                        if (c >= 32 && c <= 126)
                            result.Append(c);
                        else
                            result.Append("?");
                        break;
                }
            }

            return result.ToString();
        }

        private static string Safe(
            string value,
            string fallback)
        {
            return string.IsNullOrEmpty(value)
                ? fallback
                : value;
        }

        private static List<string> WrapText(
            string text,
            int maxCharacters)
        {
            List<string> lines =
                new List<string>();

            if (string.IsNullOrEmpty(text))
            {
                lines.Add("");
                return lines;
            }

            string[] words =
                text.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

            StringBuilder current =
                new StringBuilder();

            foreach (string word in words)
            {
                if (current.Length == 0)
                {
                    current.Append(word);
                    continue;
                }

                if (current.Length +
                    1 +
                    word.Length >
                    maxCharacters)
                {
                    lines.Add(
                        current.ToString());

                    current.Clear();

                    current.Append(word);
                }
                else
                {
                    current.Append(" ");
                    current.Append(word);
                }
            }

            if (current.Length > 0)
            {
                lines.Add(
                    current.ToString());
            }

            return lines;
        }

        // =====================================================
        // PDF GENERATION
        // =====================================================

        private static string BuildPdf(
            List<string> pages)
        {
            if (pages == null ||
                pages.Count == 0)
            {
                return null;
            }

            List<string> objects =
                new List<string>();

            // -------------------------------------------------
            // Object 1: Catalog
            // -------------------------------------------------

            objects.Add(
                "<< /Type /Catalog /Pages 2 0 R >>");

            // -------------------------------------------------
            // Object 2: Pages
            // -------------------------------------------------

            objects.Add("");

            int nextObjectId = 3;

            // -------------------------------------------------
            // Object 3: Helvetica font
            // -------------------------------------------------

            int fontObjectId =
                nextObjectId++;

            objects.Add(
                "<< /Type /Font " +
                "/Subtype /Type1 " +
                "/BaseFont /Helvetica >>");

            List<int> pageObjectIds =
                new List<int>();

            // -------------------------------------------------
            // Page objects
            // -------------------------------------------------

            foreach (string pageContent in pages)
            {
                int pageId =
                    nextObjectId++;

                int contentId =
                    nextObjectId++;

                pageObjectIds.Add(
                    pageId);

                string contentObject =
                    "<< /Length " +
                    Encoding.ASCII.GetByteCount(
                        pageContent) +
                    " >>\n" +
                    "stream\n" +
                    pageContent +
                    "\nendstream";

                while (objects.Count < contentId)
                    objects.Add("");

                objects[contentId - 1] =
                    contentObject;

                string pageObject =
                    "<< /Type /Page " +
                    "/Parent 2 0 R " +
                    "/MediaBox [0 0 595 842] " +
                    "/Resources << /Font << " +
                    $"/F1 {fontObjectId} 0 R " +
                    ">> >> " +
                    $"/Contents {contentId} 0 R >>";

                while (objects.Count < pageId)
                    objects.Add("");

                objects[pageId - 1] =
                    pageObject;
            }

            // -------------------------------------------------
            // Page tree
            // -------------------------------------------------

            string kids =
                string.Join(
                    " ",
                    pageObjectIds.ConvertAll(
                        id => $"{id} 0 R"));

            objects[1] =
                "<< /Type /Pages " +
                $"/Kids [{kids}] " +
                $"/Count {pageObjectIds.Count} >>";

            // -------------------------------------------------
            // Build PDF
            // -------------------------------------------------

            StringBuilder pdf =
                new StringBuilder();

            pdf.AppendLine(
                "%PDF-1.4");

            List<long> offsets =
                new List<long>();

            for (int i = 0;
                 i < objects.Count;
                 i++)
            {
                offsets.Add(
                    Encoding.ASCII.GetByteCount(
                        pdf.ToString()));

                pdf.AppendLine(
                    $"{i + 1} 0 obj");

                pdf.AppendLine(
                    objects[i]);

                pdf.AppendLine(
                    "endobj");
            }

            long xrefPosition =
                Encoding.ASCII.GetByteCount(
                    pdf.ToString());

            pdf.AppendLine(
                "xref");

            pdf.AppendLine(
                $"0 {objects.Count + 1}");

            pdf.AppendLine(
                "0000000000 65535 f ");

            foreach (long offset in offsets)
            {
                pdf.AppendLine(
                    $"{offset:D10} 00000 n ");
            }

            pdf.AppendLine(
                "trailer");

            pdf.AppendLine(
                "<< " +
                $"/Size {objects.Count + 1} " +
                "/Root 1 0 R >>");

            pdf.AppendLine(
                "startxref");

            pdf.AppendLine(
                xrefPosition.ToString());

            pdf.AppendLine(
                "%%EOF");

            return pdf.ToString();
        }
    }
}