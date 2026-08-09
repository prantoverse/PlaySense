using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

using PlaySense.Data.Models;
using PlaySense.Reporting;

namespace PlaySense.Export
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
                outputPath =
                    Path.GetFullPath(outputPath);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[PlaySense] PDF Export failed: invalid output path.\n{exception}");

                return null;
            }

            string directory =
                Path.GetDirectoryName(outputPath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                Debug.LogError(
                    $"[PlaySense] PDF Export failed: could not determine output directory.\n{outputPath}");

                return null;
            }

            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[PlaySense] PDF Export failed: could not create output directory.\n{exception}");

                return null;
            }

            // -------------------------------------------------
            // Analyse session
            // -------------------------------------------------

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

            // -------------------------------------------------
            // Build pages
            // -------------------------------------------------

            List<string> pages =
                BuildPages(
                    session,
                    behaviour);

            if (pages == null ||
                pages.Count == 0)
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: no pages were generated.");

                return null;
            }

            string pdf =
                BuildPdf(pages);

            if (string.IsNullOrWhiteSpace(pdf))
            {
                Debug.LogError(
                    "[PlaySense] PDF Export failed: generated PDF was empty.");

                return null;
            }

            // -------------------------------------------------
            // Write file
            // -------------------------------------------------

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
                $"Duration: {session.Duration:F2} seconds",
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

            AddText(
                page,
                ref y,
                $"Gameplay events: {eventCount}",
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
                        WrapText(
                            finding,
                            84);

                    bool firstLine = true;

                    foreach (string line in wrapped)
                    {
                        EnsureSpace(
                            ref page,
                            ref y,
                            pages,
                            30f);

                        string outputLine =
                            firstLine
                                ? "- " + line
                                : "  " + line;

                        AddText(
                            page,
                            ref y,
                            outputLine,
                            FontSize,
                            false);

                        firstLine = false;
                    }

                    y -= 3f;
                }
            }

            y -= 14f;

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
                        Safe(
                            gameEvent.Category,
                            "Event");

                    string action =
                        Safe(
                            gameEvent.Action,
                            "Unknown action");

                    string target =
                        Safe(
                            gameEvent.Target,
                            "");

                    string line =
                        $"{gameEvent.Timestamp:F2}s   " +
                        $"{category}: {action}";

                    if (!string.IsNullOrEmpty(target))
                    {
                        line +=
                            $" - {target}";
                    }

                    List<string> wrapped =
                        WrapText(
                            line,
                            88);

                    foreach (string wrappedLine in wrapped)
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

            int observationCount =
                session.BehaviourObservations == null
                    ? 0
                    : session.BehaviourObservations.Count;

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

                    string description =
                        BuildObservationDescription(
                            observation);

                    foreach (string line in
                             WrapText(
                                 description,
                                 88))
                    {
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
                120f);

            AddSectionTitle(
                page,
                ref y,
                "BEHAVIOURAL CONTEXT");

            string behaviourContext =
                BuildBehaviourContext(
                    behaviour);

            foreach (string line in
                     WrapText(
                         behaviourContext,
                         88))
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

            y -= 15f;

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
        // BEHAVIOURAL CONTEXT
        // =====================================================

        private static string BuildBehaviourContext(
            BehaviourReport behaviour)
        {
            if (behaviour == null)
            {
                return
                    "Behavioural analysis was unavailable.";
            }

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
            {
                return
                    "Behavioural observation was unavailable.";
            }

            string subject =
                Safe(
                    observation.Subject,
                    "the object");

            if (observation.Type ==
                "ObjectAttended")
            {
                return
                    $"The player visually attended to {subject} " +
                    $"for {observation.Duration:F2} seconds. " +
                    $"The object was approximately {observation.Distance:F2} metres away " +
                    $"at an average view angle of {observation.Angle:F1} degrees.";
            }

            return
                $"PlaySense recorded {Safe(observation.Type, "a behavioural observation")} " +
                $"involving {subject} for {observation.Duration:F2} seconds.";
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

            // Current PDF implementation uses Helvetica.
            // Bold flag is retained for future font support.
            string fontName = "/F1";

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
            if (y >=
                MarginBottom +
                requiredSpace)
            {
                return;
            }

            FinishPage(page);

            pages.Add(
                page.ToString());

            page =
                CreatePage();

            y =
                PageHeight -
                MarginTop;

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
                    // -----------------------------------------
                    // PDF special characters
                    // -----------------------------------------

                    case '\\':
                        result.Append("\\\\");
                        break;

                    case '(':
                        result.Append("\\(");
                        break;

                    case ')':
                        result.Append("\\)");
                        break;

                    // -----------------------------------------
                    // Whitespace
                    // -----------------------------------------

                    case '\r':
                    case '\n':
                    case '\t':
                        result.Append(" ");
                        break;

                    // -----------------------------------------
                    // Unicode punctuation
                    // Convert to ASCII equivalents because
                    // the PDF is currently ASCII/Helvetica.
                    // -----------------------------------------

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

                    // -----------------------------------------
                    // Degree symbol
                    // -----------------------------------------

                    case '°':
                        result.Append(" deg");
                        break;

                    // -----------------------------------------
                    // Normal ASCII characters
                    // -----------------------------------------

                    default:

                        if (c >= 32 &&
                            c <= 126)
                        {
                            result.Append(c);
                        }
                        else
                        {
                            // Ignore unsupported Unicode
                            // characters instead of corrupting
                            // the PDF stream.
                            result.Append("?");
                        }

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
            // Create page objects
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
                {
                    objects.Add("");
                }

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
                {
                    objects.Add("");
                }

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
                        id =>
                            $"{id} 0 R"));

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

            // -------------------------------------------------
            // Cross-reference table
            // -------------------------------------------------

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

            // -------------------------------------------------
            // Trailer
            // -------------------------------------------------

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