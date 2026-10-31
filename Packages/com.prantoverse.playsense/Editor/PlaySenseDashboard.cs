using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PlaySense.Analysis;
using PlaySense.Core;
using PlaySense.Data.Models;
using PlaySense.Storage;

namespace PlaySense.EditorTools
{
    /// <summary>Lists recorded sessions and shows their movement, events and attention.</summary>
    public class PlaySenseDashboard : EditorWindow
    {
        private class SessionEntry
        {
            public string Path;
            public string FileName;
            public SessionData Session;
            public DateTime Modified;
        }

        private readonly List<SessionEntry> _entries = new List<SessionEntry>();
        private SessionEntry _selected;
        private BehaviourReport _report;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private bool _needsRefresh = true;

        // IMGUI needs the same controls in Layout and Repaint, so changes
        // made by clicks are applied at the start of the next Layout.
        private SessionEntry _pendingSelect;
        private bool _hasPendingSelect;

        private GUIStyle _titleStyle;
        private GUIStyle _metricValueStyle;
        private GUIStyle _wrapStyle;

        [MenuItem("Window/PlaySense/Dashboard", priority = 0)]
        public static void ShowWindow()
        {
            PlaySenseDashboard window = GetWindow<PlaySenseDashboard>();
            window.titleContent = new GUIContent("PlaySense");
            window.minSize = new Vector2(760f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            _needsRefresh = true;
            PlaySenseManager.SessionSaved += OnSessionSaved;
        }

        private void OnDisable()
        {
            PlaySenseManager.SessionSaved -= OnSessionSaved;
        }

        private void OnFocus()
        {
            _needsRefresh = true;
        }

        private void OnSessionSaved(SessionData session, string path)
        {
            _needsRefresh = true;
            Repaint();
        }

        // =====================================================
        // DATA
        // =====================================================

        private void Refresh()
        {
            _needsRefresh = false;

            string selectedPath = _selected != null ? _selected.Path : null;
            Dictionary<string, SessionEntry> previous = new Dictionary<string, SessionEntry>();
            foreach (SessionEntry entry in _entries)
                previous[entry.Path] = entry;

            _entries.Clear();
            _selected = null;

            foreach (string path in SessionStorage.GetSessionFiles())
            {
                DateTime modified = File.GetLastWriteTimeUtc(path);
                SessionEntry entry;

                // Only re-read files that changed.
                if (!previous.TryGetValue(path, out entry) || entry.Modified != modified)
                {
                    SessionData session = SessionStorage.Load(path);
                    if (session == null)
                        continue;

                    entry = new SessionEntry
                    {
                        Path = path,
                        FileName = System.IO.Path.GetFileNameWithoutExtension(path),
                        Session = session,
                        Modified = modified
                    };
                }

                _entries.Add(entry);

                if (entry.Path == selectedPath)
                    _selected = entry;
            }

            _report = _selected != null ? new SessionBehaviourAnalyzer().Analyze(_selected.Session) : null;
        }

        private void Select(SessionEntry entry)
        {
            _selected = entry;
            _report = entry != null ? new SessionBehaviourAnalyzer().Analyze(entry.Session) : null;
            _detailScroll = Vector2.zero;
        }

        // =====================================================
        // GUI
        // =====================================================

        private void EnsureStyles()
        {
            if (_titleStyle != null)
                return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16 };
            _metricValueStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 };
            _wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (Event.current.type == EventType.Layout)
            {
                if (_hasPendingSelect)
                {
                    _hasPendingSelect = false;
                    Select(_pendingSelect);
                }

                if (_needsRefresh)
                    Refresh();
            }

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawSessionList();
            DrawDetails();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label(_entries.Count + " session(s)", EditorStyles.miniLabel);

            if (Application.isPlaying && PlaySenseManager.Instance != null && PlaySenseManager.Instance.IsRecording)
            {
                GUILayout.Label("Recording " + PlaySenseManager.Instance.SessionTime.ToString("F0") + "s", EditorStyles.miniBoldLabel);
                Repaint();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton))
            {
                Directory.CreateDirectory(SessionStorage.SessionsFolder);
                EditorUtility.RevealInFinder(SessionStorage.SessionsFolder);
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                _needsRefresh = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSessionList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(280f));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            if (_entries.Count == 0)
            {
                GUILayout.Space(20f);
                EditorGUILayout.HelpBox(
                    "No sessions yet.\n\nAdd a PlaySenseManager to your scene (Window > PlaySense > Set Up Scene) and press Play. " +
                    "A session is saved when you exit Play mode.",
                    MessageType.Info);
            }

            SessionEntry toDelete = null;

            foreach (SessionEntry entry in _entries)
            {
                bool isSelected = entry == _selected;
                Color previous = GUI.backgroundColor;
                if (isSelected)
                    GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = previous;

                SessionData s = entry.Session;
                GUILayout.Label(string.IsNullOrEmpty(s.SceneName) ? "Unknown scene" : s.SceneName, EditorStyles.boldLabel);
                GUILayout.Label(
                    (string.IsNullOrEmpty(s.StartedAt) ? entry.FileName : s.StartedAt.Replace('T', ' ')) +
                    "\n" + FormatTime(s.Duration) + "  |  " + s.Metrics.TotalDistance.ToString("F0") + " m  |  " +
                    s.Events.Count + " events  |  " + s.BehaviourObservations.Count + " looks",
                    EditorStyles.miniLabel);

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(isSelected))
                {
                    if (GUILayout.Button(isSelected ? "Selected" : "Select", EditorStyles.miniButtonLeft))
                        RequestSelect(entry);
                }

                if (GUILayout.Button("Replay", EditorStyles.miniButtonMid))
                    PlaySenseReplayWindow.Open(entry.Path);

                if (GUILayout.Button("Delete", EditorStyles.miniButtonRight))
                    toDelete = entry;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (toDelete != null)
            {
                DeleteSession(toDelete);
                GUIUtility.ExitGUI();
            }
        }

        private void DrawDetails()
        {
            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            if (_selected == null || _report == null)
            {
                GUILayout.Space(60f);
                GUILayout.Label("Select a session to inspect it.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            SessionData session = _selected.Session;

            GUILayout.Label(_selected.FileName, _titleStyle);
            GUILayout.Label(
                "Scene " + session.SceneName + "  |  " + session.Platform + "  |  Unity " + session.UnityVersion +
                (session.Version < SessionData.CurrentVersion ? "  |  older file format (some fields missing)" : string.Empty),
                EditorStyles.miniLabel);

            GUILayout.Space(6f);
            DrawActions();
            GUILayout.Space(8f);

            // Metrics
            EditorGUILayout.BeginHorizontal();
            DrawMetric("Duration", FormatTime(session.Duration));
            DrawMetric("Distance", session.Metrics.TotalDistance.ToString("F1") + " m");
            DrawMetric("Avg speed", session.Metrics.AverageSpeed.ToString("F2") + " m/s");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawMetric("Active", _report.ActivePercentage.ToString("F0") + "%");
            DrawMetric("Events", _report.EventCount.ToString());
            DrawMetric("Attention", _report.AttentionCount + " / " + _report.UniqueAttendedObjects + " objects");
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10f);
            PlaySenseMovementChart.Draw(session);

            // Findings
            GUILayout.Space(10f);
            GUILayout.Label("Findings", EditorStyles.boldLabel);
            foreach (string finding in _report.Findings)
                GUILayout.Label("•  " + finding, _wrapStyle);

            // Attention
            GUILayout.Space(10f);
            GUILayout.Label("Most attended", EditorStyles.boldLabel);
            if (_report.Targets.Count == 0)
            {
                GUILayout.Label("No attention recorded.", EditorStyles.miniLabel);
            }
            else
            {
                float longest = Mathf.Max(_report.Targets[0].TotalDuration, 0.01f);
                int shown = Mathf.Min(_report.Targets.Count, 12);

                for (int i = 0; i < shown; i++)
                {
                    TargetAttention target = _report.Targets[i];
                    Rect row = EditorGUILayout.GetControlRect(false, 18f);
                    Rect bar = new Rect(row.x + 180f, row.y + 3f, (row.width - 330f) * (target.TotalDuration / longest), row.height - 6f);

                    GUI.Label(new Rect(row.x, row.y, 175f, row.height),
                        target.Name + (string.IsNullOrEmpty(target.Category) ? string.Empty : "  (" + target.Category + ")"));
                    EditorGUI.DrawRect(bar, new Color(0.3f, 0.7f, 1f, 0.8f));
                    GUI.Label(new Rect(row.xMax - 145f, row.y, 145f, row.height),
                        target.TotalDuration.ToString("F1") + "s  " + target.Count + "x", EditorStyles.miniLabel);
                }
            }

            // Events
            GUILayout.Space(10f);
            GUILayout.Label("Gameplay events", EditorStyles.boldLabel);
            if (session.Events.Count == 0)
            {
                EditorGUILayout.HelpBox("No events recorded. Call PlaySenseEvents.Log(\"Category\", \"Action\", \"Target\") from your game.", MessageType.None);
            }
            else
            {
                int shown = Mathf.Min(session.Events.Count, 200);
                for (int i = 0; i < shown; i++)
                {
                    GameEventData gameEvent = session.Events[i];
                    if (gameEvent != null)
                        GUILayout.Label(gameEvent.Timestamp.ToString("F1").PadLeft(7) + "s   " + gameEvent, EditorStyles.miniLabel);
                }

                if (session.Events.Count > shown)
                    GUILayout.Label("… " + (session.Events.Count - shown) + " more", EditorStyles.miniLabel);
            }

            GUILayout.Space(20f);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            string pdfPath = Path.ChangeExtension(_selected.Path, ".pdf");
            bool pdfExists = File.Exists(pdfPath);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Replay in Scene View", GUILayout.Height(26f)))
                PlaySenseReplayWindow.Open(_selected.Path);

            if (GUILayout.Button(pdfExists ? "Regenerate PDF" : "Generate PDF Report", GUILayout.Height(26f)))
                GenerateReport(pdfPath);

            using (new EditorGUI.DisabledScope(!pdfExists))
            {
                if (GUILayout.Button("Open PDF", GUILayout.Height(26f), GUILayout.Width(90f)))
                    EditorUtility.OpenWithDefaultApp(pdfPath);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawMetric(string label, string value)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.MinWidth(120f));
            GUILayout.Label(label, EditorStyles.miniLabel);
            GUILayout.Label(value, _metricValueStyle);
            EditorGUILayout.EndVertical();
        }

        // =====================================================
        // ACTIONS
        // =====================================================

        private void GenerateReport(string pdfPath)
        {
            try
            {
                string generated = PlaySensePDFExporter.Export(_selected.Session, pdfPath);

                if (string.IsNullOrEmpty(generated))
                {
                    EditorUtility.DisplayDialog("PlaySense", "PDF generation failed. See the Console for details.", "OK");
                    return;
                }

                if (EditorUtility.DisplayDialog("PlaySense", "PDF report created:\n" + generated, "Open", "Close"))
                    EditorUtility.OpenWithDefaultApp(generated);
            }
            catch (Exception exception)
            {
                Debug.LogError("[PlaySense] PDF generation failed:\n" + exception);
            }
        }

        private void DeleteSession(SessionEntry entry)
        {
            if (!EditorUtility.DisplayDialog("Delete session", "Delete " + entry.FileName + " and its PDF report?", "Delete", "Cancel"))
                return;

            try
            {
                File.Delete(entry.Path);

                string pdf = Path.ChangeExtension(entry.Path, ".pdf");
                if (File.Exists(pdf))
                    File.Delete(pdf);
            }
            catch (Exception exception)
            {
                Debug.LogError("[PlaySense] Could not delete session:\n" + exception);
            }

            if (_selected == entry)
                RequestSelect(null);

            _needsRefresh = true;
            Repaint();
        }

        private void RequestSelect(SessionEntry entry)
        {
            _pendingSelect = entry;
            _hasPendingSelect = true;
            Repaint();
        }

        private static string FormatTime(float seconds)
        {
            TimeSpan span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return span.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)span.TotalHours, span.Minutes, span.Seconds)
                : string.Format("{0}:{1:00}", span.Minutes, span.Seconds);
        }
    }
}
