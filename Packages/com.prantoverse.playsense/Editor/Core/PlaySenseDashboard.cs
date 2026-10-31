using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PlaySense.Core;
using PlaySense.Data.Models;
using PlaySense.Storage;

namespace PlaySense.EditorTools
{
    /// <summary>
    /// Browse recorded sessions one at a time, or see totals across all of them.
    /// Tabs come from PlaySenseDashboardTab subclasses (one per module).
    /// </summary>
    public class PlaySenseDashboard : EditorWindow
    {
        private enum Mode
        {
            Session,
            AllSessions
        }

        [SerializeField] private Mode _mode;
        [SerializeField] private string _selectedPath;
        [SerializeField] private string _tabTitle;
        [SerializeField] private string _sceneFilter = string.Empty;
        [SerializeField] private List<string> _excluded = new List<string>();

        private Vector2 _listScroll;
        private Vector2 _detailScroll;

        // IMGUI needs the same controls in Layout and Repaint, so state
        // changes from clicks are applied at the start of the next Layout.
        private readonly List<System.Action> _pending = new List<System.Action>();

        [MenuItem("Window/PlaySense/Dashboard", priority = 0)]
        public static void ShowWindow()
        {
            PlaySenseDashboard window = GetWindow<PlaySenseDashboard>();
            window.titleContent = new GUIContent("PlaySense");
            window.minSize = new Vector2(780f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            PlaySenseSessionCache.Changed += Repaint;
        }

        private void OnDisable()
        {
            PlaySenseSessionCache.Changed -= Repaint;
        }

        private void OnFocus()
        {
            PlaySenseSessionCache.MarkDirty();
        }

        private void Defer(System.Action action)
        {
            _pending.Add(action);
            Repaint();
        }

        // =====================================================
        // GUI
        // =====================================================

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout && _pending.Count > 0)
            {
                List<System.Action> actions = new List<System.Action>(_pending);
                _pending.Clear();
                foreach (System.Action action in actions)
                    action();
            }

            List<PlaySenseSessionCache.Entry> entries = PlaySenseSessionCache.Entries;

            DrawToolbar(entries);

            if (entries.Count == 0)
            {
                DrawEmpty();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawSessionList(entries);

            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            if (_mode == Mode.Session)
                DrawSessionMode(entries);
            else
                DrawOverviewMode(entries);

            GUILayout.Space(20f);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar(List<PlaySenseSessionCache.Entry> entries)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            Mode mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "Session", "All Sessions" }, EditorStyles.toolbarButton, GUILayout.Width(200f));
            if (mode != _mode)
            {
                Defer(() =>
                {
                    _mode = mode;
                    _detailScroll = Vector2.zero;
                });
            }

            GUILayout.Label(entries.Count + " session(s)", EditorStyles.miniLabel);

            PlaySenseManager manager = PlaySenseManager.Instance;
            if (Application.isPlaying && manager != null && manager.IsRecording)
            {
                GUILayout.Label("● Recording " + PlaySenseGUI.FormatTime(manager.SessionTime), EditorStyles.miniBoldLabel);
                Repaint();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Welcome", EditorStyles.toolbarButton))
                PlaySenseWelcomeWindow.ShowWindow();

            if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton))
            {
                System.IO.Directory.CreateDirectory(SessionStorage.SessionsFolder);
                EditorUtility.RevealInFinder(SessionStorage.SessionsFolder);
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                PlaySenseSessionCache.MarkDirty();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEmpty()
        {
            GUILayout.Space(30f);
            EditorGUILayout.HelpBox(
                "No sessions recorded yet.\n\n" +
                "Open the Welcome window for a step-by-step setup, or: select your player, run " +
                "Window > PlaySense > Set Up Scene, press Play, then exit Play mode.",
                MessageType.Info);

            if (GUILayout.Button("Open Welcome & Setup Checklist", GUILayout.Height(28f)))
                PlaySenseWelcomeWindow.ShowWindow();
        }

        // =====================================================
        // SESSION LIST
        // =====================================================

        private void DrawSessionList(List<PlaySenseSessionCache.Entry> entries)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(270f));

            if (_mode == Mode.AllSessions)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Include", EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("All", EditorStyles.miniButtonLeft))
                    Defer(() => _excluded.Clear());
                if (GUILayout.Button("None", EditorStyles.miniButtonRight))
                    Defer(() =>
                    {
                        _excluded.Clear();
                        foreach (PlaySenseSessionCache.Entry e in PlaySenseSessionCache.Entries)
                            _excluded.Add(e.Path);
                    });
                EditorGUILayout.EndHorizontal();
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            PlaySenseSessionCache.Entry toDelete = null;

            foreach (PlaySenseSessionCache.Entry entry in entries)
            {
                bool selected = _mode == Mode.Session && entry.Path == _selectedPath;
                Color previous = GUI.backgroundColor;
                if (selected)
                    GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = previous;

                SessionData s = entry.Session;
                List<string> scenes = s.GetSceneNames();

                EditorGUILayout.BeginHorizontal();
                if (_mode == Mode.AllSessions)
                {
                    bool included = !_excluded.Contains(entry.Path);
                    bool now = EditorGUILayout.Toggle(included, GUILayout.Width(16f));
                    if (now != included)
                    {
                        string path = entry.Path;
                        Defer(() =>
                        {
                            if (now) _excluded.Remove(path);
                            else _excluded.Add(path);
                        });
                    }
                }

                GUILayout.Label(scenes.Count > 1 ? scenes[0] + " (+" + (scenes.Count - 1) + " more)" : scenes[0], EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                GUILayout.Label(
                    (string.IsNullOrEmpty(s.StartedAt) ? entry.FileName : s.StartedAt.Replace('T', ' ')) + "\n" +
                    PlaySenseGUI.FormatTime(s.Duration) + "  |  " + s.Events.Count + " events  |  " +
                    s.BehaviourObservations.Count + " looks",
                    EditorStyles.miniLabel);

                if (_mode == Mode.Session)
                {
                    EditorGUILayout.BeginHorizontal();
                    using (new EditorGUI.DisabledScope(selected))
                    {
                        if (GUILayout.Button(selected ? "Selected" : "Select", EditorStyles.miniButtonLeft))
                        {
                            string path = entry.Path;
                            Defer(() =>
                            {
                                _selectedPath = path;
                                _detailScroll = Vector2.zero;
                            });
                        }
                    }

                    if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(60f)))
                        toDelete = entry;

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            if (toDelete != null)
            {
                if (EditorUtility.DisplayDialog("Delete session", "Delete " + toDelete.FileName + " and its PDF report?", "Delete", "Cancel"))
                    PlaySenseSessionCache.Delete(toDelete);

                GUIUtility.ExitGUI();
            }
        }

        // =====================================================
        // SESSION MODE
        // =====================================================

        private void DrawSessionMode(List<PlaySenseSessionCache.Entry> entries)
        {
            PlaySenseSessionCache.Entry entry = PlaySenseSessionCache.Find(_selectedPath);
            if (entry == null)
            {
                if (entries.Count > 0)
                {
                    string first = entries[0].Path;
                    Defer(() => _selectedPath = first);
                }

                GUILayout.Label("Select a session.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            SessionData session = entry.Session;

            GUILayout.Label(entry.FileName, PlaySenseGUI.TitleStyle);
            GUILayout.Label(
                string.Join(" → ", session.GetSceneNames().ToArray()) + "  |  " + session.Platform + "  |  Unity " + session.UnityVersion +
                (session.Modules.Count > 0 ? "  |  Modules: " + string.Join(", ", session.Modules.ToArray()) : string.Empty),
                EditorStyles.miniLabel);

            // Actions from modules (Replay, PDF, ...)
            List<PlaySenseSessionAction> actions = PlaySenseExtensionRegistry.Actions;
            if (actions.Count > 0)
            {
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                foreach (PlaySenseSessionAction action in actions)
                {
                    if (GUILayout.Button(action.Title, GUILayout.Height(24f)))
                    {
                        PlaySenseSessionAction run = action;
                        EditorApplication.delayCall += () => run.Run(session, entry.Path);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            List<PlaySenseDashboardTab> tabs = PlaySenseExtensionRegistry.Tabs.FindAll(t => t.IsAvailable(session));
            PlaySenseDashboardTab tab = DrawTabs(tabs);
            if (tab == null)
                return;

            GUILayout.Space(6f);
            tab.DrawSession(session, entry.Path);
        }

        // =====================================================
        // OVERVIEW MODE
        // =====================================================

        private void DrawOverviewMode(List<PlaySenseSessionCache.Entry> entries)
        {
            List<string> scenes = PlaySenseSessionCache.GetSceneNames();
            List<string> options = new List<string> { "All scenes" };
            options.AddRange(scenes);

            int current = string.IsNullOrEmpty(_sceneFilter) ? 0 : Mathf.Max(0, scenes.IndexOf(_sceneFilter) + 1);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("All Sessions", PlaySenseGUI.TitleStyle);
            GUILayout.FlexibleSpace();
            int picked = EditorGUILayout.Popup(current, options.ToArray(), GUILayout.Width(200f));
            if (picked != current)
            {
                string filter = picked == 0 ? string.Empty : scenes[picked - 1];
                Defer(() => _sceneFilter = filter);
            }
            EditorGUILayout.EndHorizontal();

            List<SessionData> sessions = new List<SessionData>();
            foreach (PlaySenseSessionCache.Entry entry in entries)
            {
                if (_excluded.Contains(entry.Path))
                    continue;

                if (!string.IsNullOrEmpty(_sceneFilter) && !entry.Session.GetSceneNames().Contains(_sceneFilter))
                    continue;

                sessions.Add(entry.Session);
            }

            GUILayout.Label(
                sessions.Count + " session(s)" + (string.IsNullOrEmpty(_sceneFilter) ? string.Empty : " that visited " + _sceneFilter) +
                ". Each session is one playthrough, so \"% of sessions\" reads as \"% of players\" when every tester plays once.",
                EditorStyles.wordWrappedMiniLabel);

            if (sessions.Count == 0)
            {
                EditorGUILayout.HelpBox("No sessions match. Tick sessions on the left or change the scene filter.", MessageType.Info);
                return;
            }

            List<PlaySenseDashboardTab> tabs = PlaySenseExtensionRegistry.Tabs.FindAll(t => t.SupportsOverview);
            PlaySenseDashboardTab tab = DrawTabs(tabs);
            if (tab == null)
                return;

            GUILayout.Space(6f);
            tab.DrawOverview(sessions, string.IsNullOrEmpty(_sceneFilter) ? null : _sceneFilter);
        }

        private PlaySenseDashboardTab DrawTabs(List<PlaySenseDashboardTab> tabs)
        {
            if (tabs.Count == 0)
                return null;

            int index = Mathf.Max(0, tabs.FindIndex(t => t.Title == _tabTitle));
            string[] titles = new string[tabs.Count];
            for (int i = 0; i < tabs.Count; i++)
                titles[i] = tabs[i].Title;

            GUILayout.Space(8f);
            int picked = GUILayout.Toolbar(index, titles, GUILayout.Height(24f));
            if (picked != index)
            {
                string title = titles[picked];
                Defer(() =>
                {
                    _tabTitle = title;
                    _detailScroll = Vector2.zero;
                });
            }

            return tabs[index];
        }
    }
}
