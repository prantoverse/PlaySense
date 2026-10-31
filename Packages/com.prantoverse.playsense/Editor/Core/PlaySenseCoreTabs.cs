using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PlaySense.Analysis;
using PlaySense.Data.Models;

namespace PlaySense.EditorTools
{
    /// <summary>Headline numbers, movement and findings.</summary>
    public class PlaySenseSummaryTab : PlaySenseDashboardTab
    {
        public override string Title
        {
            get { return "Summary"; }
        }

        public override int Order
        {
            get { return 0; }
        }

        public override bool SupportsOverview
        {
            get { return true; }
        }

        public override void DrawSession(SessionData session, string path)
        {
            BehaviourReport report = new SessionBehaviourAnalyzer().Analyze(session);

            EditorGUILayout.BeginHorizontal();
            PlaySenseGUI.Metric("Duration", PlaySenseGUI.FormatTime(session.Duration));
            PlaySenseGUI.Metric("Distance", session.Metrics.TotalDistance.ToString("F1") + " m");
            PlaySenseGUI.Metric("Avg speed", session.Metrics.AverageSpeed.ToString("F2") + " m/s");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            PlaySenseGUI.Metric("Active", PlaySenseGUI.Percent(report.ActivePercentage));
            PlaySenseGUI.Metric("Events", report.EventCount.ToString());
            PlaySenseGUI.Metric("Attention", report.AttentionCount + " on " + report.UniqueAttendedObjects + " objects");
            EditorGUILayout.EndHorizontal();

            if (session.Scenes.Count > 1)
            {
                PlaySenseGUI.Section("Scenes");
                for (int i = 0; i < session.Scenes.Count; i++)
                {
                    float end = i + 1 < session.Scenes.Count ? session.Scenes[i + 1].StartTime : session.Duration;
                    GUILayout.Label(
                        PlaySenseGUI.FormatTime(session.Scenes[i].StartTime) + "   " + session.Scenes[i].Name +
                        "   (" + PlaySenseGUI.FormatTime(end - session.Scenes[i].StartTime) + ")",
                        EditorStyles.miniLabel);
                }
            }

            GUILayout.Space(10f);
            PlaySenseMovementChart.Draw(session);

            PlaySenseGUI.Section("Findings");
            foreach (string finding in report.Findings)
                GUILayout.Label("•  " + finding, PlaySenseGUI.WrapStyle);
        }

        public override void DrawOverview(IList<SessionData> sessions, string sceneFilter)
        {
            CrossSessionReport report = new CrossSessionAnalyzer().Analyze(sessions, sceneFilter);

            EditorGUILayout.BeginHorizontal();
            PlaySenseGUI.Metric("Sessions", report.SessionCount.ToString());
            PlaySenseGUI.Metric("Avg length", PlaySenseGUI.FormatTime(report.AverageDuration));
            PlaySenseGUI.Metric("Total played", PlaySenseGUI.FormatTime(report.TotalDuration));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            PlaySenseGUI.Metric("Avg distance", report.AverageDistance.ToString("F0") + " m");
            PlaySenseGUI.Metric("Events", report.TotalEvents.ToString());
            PlaySenseGUI.Metric("Scenes", report.Scenes.Count.ToString());
            EditorGUILayout.EndHorizontal();

            PlaySenseGUI.Section("How far players got");
            GUILayout.Label("Share of sessions that reached each scene.", EditorStyles.miniLabel);
            foreach (CountStat scene in report.Scenes)
            {
                PlaySenseGUI.BarRow(scene.Name, scene.PercentOfSessions / 100f,
                    scene.SessionsWith + " / " + report.SessionCount + "  (" + PlaySenseGUI.Percent(scene.PercentOfSessions) + ")",
                    PlaySenseGUI.Accent);
            }

            PlaySenseGUI.Section("Session length");
            List<float> durations = new List<float>();
            foreach (SessionData session in sessions)
                durations.Add(session.Duration);
            durations.Sort();
            if (durations.Count > 0)
            {
                GUILayout.Label(
                    "Shortest " + PlaySenseGUI.FormatTime(durations[0]) +
                    "   Median " + PlaySenseGUI.FormatTime(durations[durations.Count / 2]) +
                    "   Longest " + PlaySenseGUI.FormatTime(durations[durations.Count - 1]),
                    EditorStyles.miniLabel);
            }
        }
    }

    /// <summary>Gameplay events logged with PlaySenseEvents.Log.</summary>
    public class PlaySenseEventsTab : PlaySenseDashboardTab
    {
        private readonly HashSet<string> _expanded = new HashSet<string>();
        private bool _hideSceneEvents = true;

        public override string Title
        {
            get { return "Events"; }
        }

        public override int Order
        {
            get { return 10; }
        }

        public override bool SupportsOverview
        {
            get { return true; }
        }

        public override void DrawSession(SessionData session, string path)
        {
            if (session.Events.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No events in this session. Log gameplay moments from your code:\n" +
                    "PlaySenseEvents.Log(\"Combat\", \"Kill\", enemy, \"method\", \"gun\");",
                    MessageType.Info);
                return;
            }

            // Counts per category/action for this session.
            CrossSessionReport report = new CrossSessionAnalyzer().Analyze(new List<SessionData> { session });

            PlaySenseGUI.Section("Counts");
            int max = 1;
            foreach (EventStat stat in report.Events)
                max = Mathf.Max(max, stat.TotalCount);

            foreach (EventStat stat in report.Events)
                PlaySenseGUI.BarRow(stat.Label, stat.TotalCount / (float)max, stat.TotalCount + "x", PlaySenseGUI.Accent, 240f);

            PlaySenseGUI.Section("Timeline");
            int shown = Mathf.Min(session.Events.Count, 300);
            for (int i = 0; i < shown; i++)
            {
                GameEventData gameEvent = session.Events[i];
                if (gameEvent == null)
                    continue;

                string meta = string.Empty;
                if (gameEvent.Metadata != null && gameEvent.Metadata.Count > 0)
                {
                    List<string> parts = new List<string>();
                    foreach (EventParameter parameter in gameEvent.Metadata)
                        parts.Add(parameter.Key + "=" + parameter.Value);
                    meta = "   {" + string.Join(", ", parts.ToArray()) + "}";
                }

                GUILayout.Label(
                    gameEvent.Timestamp.ToString("F1").PadLeft(7) + "s   " + gameEvent + meta +
                    (session.Scenes.Count > 1 ? "   [" + session.GetSceneName(gameEvent.Scene) + "]" : string.Empty),
                    EditorStyles.miniLabel);
            }

            if (session.Events.Count > shown)
                GUILayout.Label("… " + (session.Events.Count - shown) + " more", EditorStyles.miniLabel);
        }

        public override void DrawOverview(IList<SessionData> sessions, string sceneFilter)
        {
            CrossSessionReport report = new CrossSessionAnalyzer().Analyze(sessions, sceneFilter);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(report.TotalEvents + " events across " + report.SessionCount + " session(s)", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            _hideSceneEvents = GUILayout.Toggle(_hideSceneEvents, "Hide scene changes", EditorStyles.miniButton);
            if (GUILayout.Button("Export CSV", EditorStyles.miniButton))
                ExportCsv(report);
            EditorGUILayout.EndHorizontal();

            if (report.Events.Count == 0)
            {
                EditorGUILayout.HelpBox("No events in these sessions. Use PlaySenseEvents.Log in your game code.", MessageType.Info);
                return;
            }

            PlaySenseGUI.Section("What players did");
            GUILayout.Label("Bar = share of sessions with at least one. Expand a row for its metadata breakdown.", EditorStyles.miniLabel);
            GUILayout.Space(4f);

            foreach (EventStat stat in report.Events)
            {
                if (_hideSceneEvents && stat.Category == "Scene")
                    continue;

                EditorGUILayout.BeginHorizontal();
                bool canExpand = stat.Metadata.Count > 0;
                bool expanded = _expanded.Contains(stat.Label);

                if (canExpand)
                {
                    bool now = EditorGUILayout.Foldout(expanded, GUIContent.none, true);
                    if (now != expanded)
                    {
                        if (now) _expanded.Add(stat.Label);
                        else _expanded.Remove(stat.Label);
                    }
                }
                else
                {
                    GUILayout.Space(14f);
                }

                EditorGUILayout.BeginVertical();
                PlaySenseGUI.BarRow(
                    stat.Label,
                    stat.PercentOfSessions / 100f,
                    stat.SessionsWith + "/" + report.SessionCount + " (" + PlaySenseGUI.Percent(stat.PercentOfSessions) + ")  " +
                    stat.TotalCount + "x, " + stat.AveragePerSession.ToString("0.#") + "/session",
                    PlaySenseGUI.Accent, 220f);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                if (!canExpand || !_expanded.Contains(stat.Label))
                    continue;

                EditorGUI.indentLevel++;
                foreach (MetadataStat meta in stat.Metadata)
                {
                    GUILayout.Label("      " + meta.Key, EditorStyles.miniBoldLabel);
                    foreach (ValueStat value in meta.Values)
                    {
                        PlaySenseGUI.BarRow(
                            "        " + value.Value,
                            value.PercentOfEvents / 100f,
                            PlaySenseGUI.Percent(value.PercentOfEvents) + " of events, " +
                            value.SessionsWith + " session(s)",
                            PlaySenseGUI.Good, 220f);
                    }
                }
                EditorGUI.indentLevel--;
                GUILayout.Space(4f);
            }

            PlaySenseGUI.Section("Categories");
            foreach (CountStat category in report.Categories)
            {
                if (_hideSceneEvents && category.Name == "Scene")
                    continue;

                PlaySenseGUI.BarRow(category.Name, category.PercentOfSessions / 100f,
                    category.SessionsWith + "/" + report.SessionCount + " (" + PlaySenseGUI.Percent(category.PercentOfSessions) + "), " + category.TotalCount + "x",
                    PlaySenseGUI.Accent);
            }
        }

        private static void ExportCsv(CrossSessionReport report)
        {
            string path = EditorUtility.SaveFilePanel("Export PlaySense events", string.Empty, "PlaySense_Events.csv", "csv");
            if (string.IsNullOrEmpty(path))
                return;

            File.WriteAllText(path, report.ToCsv());
            EditorUtility.RevealInFinder(path);
        }
    }
}
