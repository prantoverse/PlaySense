using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using PlaySense.Data.Models;

namespace PlaySense.Export
{
    public static class PlaySensePDFExporter
    {
        private const float PageWidth = 595f;
        private const float PageHeight = 842f;

        private const float MarginLeft = 48f;
        private const float MarginRight = 48f;
        private const float MarginTop = 55f;
        private const float MarginBottom = 55f;

        private const float FontSize = 10f;
        private const float LineHeight = 15f;

        public static string Export(SessionData session, string outputPath)
        {
            if (session == null)
            {
                Debug.LogError(
                    "PlaySense PDF Export failed: SessionData is null.");

                return null;
            }

            List<string> pages =
                BuildPages(session);

            string pdf =
                BuildPdf(pages);

            File.WriteAllBytes(
                outputPath,
                Encoding.ASCII.GetBytes(pdf));

            Debug.Log(
                $"PlaySense PDF Report Generated:\n{outputPath}");

            return outputPath;
        }

        private static List<string> BuildPages(
            SessionData session)
        {
            List<string> pages =
                new List<string>();

            StringBuilder currentPage =
                CreatePage();

            float y =
                PageHeight - MarginTop;

            AddText(
                currentPage,
                ref y,
                "PLAYSENSE PLAYTEST REPORT",
                20f,
                true);

            y -= 8f;

            AddText(
                currentPage,
                ref y,
                "Gameplay session analysis",
                11f,
                false);

            y -= 18f;

            AddSectionTitle(
                currentPage,
                ref y,
                "SESSION SUMMARY");

            AddText(
                currentPage,
                ref y,
                $"Scene: {Safe(session.SceneName, "Unknown")}",
                FontSize,
                false);

            AddText(
                currentPage,
                ref y,
                $"Session ID: {Safe(session.SessionId, "Unknown")}",
                FontSize,
                false);

            AddText(
                currentPage,
                ref y,
                $"Duration: {session.Metrics.Duration:F2} seconds",
                FontSize,
                false);

            AddText(
                currentPage,
                ref y,
                $"Distance travelled: {session.Metrics.TotalDistance:F2} m",
                FontSize,
                false);

            AddText(
                currentPage,
                ref y,
                $"Average speed: {session.Metrics.AverageSpeed:F2} m/s",
                FontSize,
                false);

            AddText(
                currentPage,
                ref y,
                $"Frames recorded: {session.Metrics.FrameCount}",
                FontSize,
                false);

            int eventCount =
                session.Events == null
                    ? 0
                    : session.Events.Count;

            AddText(
                currentPage,
                ref y,
                $"Gameplay events: {eventCount}",
                FontSize,
                false);

            y -= 14f;

            AddSectionTitle(
                currentPage,
                ref y,
                "PLAYER ACTIVITY");

            AddText(
                currentPage,
                ref y,
                "The following timeline contains recorded gameplay events.",
                FontSize,
                false);

            y -= 6f;

            if (eventCount == 0)
            {
                AddText(
                    currentPage,
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
                    var gameEvent =
                        session.Events[i];

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
                        $"{category} / {action}";

                    if (!string.IsNullOrEmpty(target))
                    {
                        line += $" — {target}";
                    }

                    List<string> wrapped =
                        WrapText(
                            line,
                            92);

                    foreach (string wrappedLine in wrapped)
                    {
                        if (y < MarginBottom + 25f)
                        {
                            FinishPage(
                                currentPage);

                            pages.Add(
                                currentPage.ToString());

                            currentPage =
                                CreatePage();

                            y =
                                PageHeight - MarginTop;

                            AddText(
                                currentPage,
                                ref y,
                                "PLAYSENSE PLAYTEST REPORT",
                                14f,
                                true);

                            y -= 10f;

                            AddText(
                                currentPage,
                                ref y,
                                "PLAYER ACTIVITY — CONTINUED",
                                11f,
                                true);

                            y -= 10f;
                        }

                        AddText(
                            currentPage,
                            ref y,
                            wrappedLine,
                            FontSize,
                            false);
                    }
                }
            }

            y -= 18f;

            if (y < MarginBottom + 100f)
            {
                FinishPage(
                    currentPage);

                pages.Add(
                    currentPage.ToString());

                currentPage =
                    CreatePage();

                y =
                    PageHeight - MarginTop;
            }

            AddSectionTitle(
                currentPage,
                ref y,
                "REPORT NOTES");

            AddText(
                currentPage,
                ref y,
                "This report was generated automatically by PlaySense.",
                FontSize,
                false);

            AddText(
                currentPage,
                ref y,
                "Recorded gameplay data is presented for playtest and design analysis.",
                FontSize,
                false);

            FinishPage(
                currentPage);

            pages.Add(
                currentPage.ToString());

            return pages;
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

        private static void AddText(
            StringBuilder page,
            ref float y,
            string text,
            float fontSize,
            bool bold)
        {
            string safeText =
                EscapePdfText(text);

            page.AppendLine(
                $"BT /F1 {fontSize:F1} Tf " +
                $"{MarginLeft:F1} {y:F1} Td " +
                $"({safeText}) Tj ET");

            y -= LineHeight;
        }

        private static string EscapePdfText(
            string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("\\", "\\\\")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("\r", "")
                .Replace("\n", " ");
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

                if (current.Length + 1 + word.Length
                    > maxCharacters)
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

        // -----------------------------------------------------
        // Minimal PDF writer
        // -----------------------------------------------------

        private static string BuildPdf(
            List<string> pages)
        {
            List<string> objects =
                new List<string>();

            objects.Add(
                "<< /Type /Catalog /Pages 2 0 R >>");

            List<int> pageObjectIds =
                new List<int>();

            int nextObjectId = 3;

            int fontObjectId =
                nextObjectId++;

            objects.Add(
                "<< /Type /Font " +
                "/Subtype /Type1 " +
                "/BaseFont /Helvetica >>");

            foreach (string pageContent in pages)
            {
                int pageId =
                    nextObjectId++;

                int contentId =
                    nextObjectId++;

                pageObjectIds.Add(
                    pageId);

                string stream =
                    pageContent;

                string contentObject =
                    "<< /Length " +
                    Encoding.ASCII
                        .GetByteCount(stream) +
                    " >>\n" +
                    "stream\n" +
                    stream +
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

            string kids =
                string.Join(
                    " ",
                    pageObjectIds.ConvertAll(
                        id => $"{id} 0 R"));

            objects[1] =
                "<< /Type /Pages " +
                $"/Kids [{kids}] " +
                $"/Count {pageObjectIds.Count} >>";

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
                    Encoding.ASCII
                        .GetByteCount(
                            pdf.ToString()));

                pdf.AppendLine(
                    $"{i + 1} 0 obj");

                pdf.AppendLine(
                    objects[i]);

                pdf.AppendLine(
                    "endobj");
            }

            long xrefPosition =
                Encoding.ASCII
                    .GetByteCount(
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