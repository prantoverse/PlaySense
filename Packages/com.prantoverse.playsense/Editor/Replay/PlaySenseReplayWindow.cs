using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlaySense.Analysis;
using PlaySense.Data.Models;

namespace PlaySense.EditorTools
{
    /// <summary>
    /// Draws recorded sessions in the Scene view. Single mode plays one session
    /// on a timeline; All Sessions mode overlays every session recorded in the
    /// open scene (combined heatmap, all paths). What gets drawn comes from
    /// PlaySenseReplayLayer subclasses, so modules add their own layers.
    /// </summary>
    public class PlaySenseReplayWindow : EditorWindow
    {
        [SerializeField] private string _path;
        [SerializeField] private bool _combined;
        [SerializeField] private float _time;
        [SerializeField] private float _speed = -1f;
        [SerializeField] private bool _followAgent;
        [SerializeField] private List<string> _hiddenLayers = new List<string>();
        [SerializeField] private List<string> _shownLayers = new List<string>();

        private readonly PlaySenseReplayContext _context = new PlaySenseReplayContext();
        private bool _playing;
        private double _lastTick;
        private Vector2 _scroll;
        private string _loadedScene;

        [MenuItem("Window/PlaySense/Replay", priority = 1)]
        public static void ShowWindow()
        {
            Open(null);
        }

        /// <summary>Opens the replay window, optionally with a session file loaded.</summary>
        public static void Open(string path)
        {
            PlaySenseReplayWindow window = GetWindow<PlaySenseReplayWindow>();
            window.titleContent = new GUIContent("PlaySense Replay");
            window.minSize = new Vector2(340f, 420f);
            window.Show();

            if (!string.IsNullOrEmpty(path))
            {
                window._path = path;
                window._combined = false;
                window._time = 0f;
                window.Reload();
            }
        }

        private void OnEnable()
        {
            if (_speed <= 0f)
                _speed = PlaySenseSettingsSpeed();

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += Tick;
            PlaySenseSessionCache.Changed += OnSessionsChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneChangedInEditor;
            Reload();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= Tick;
            PlaySenseSessionCache.Changed -= OnSessionsChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneChangedInEditor;
            SceneView.RepaintAll();
        }

        private static float PlaySenseSettingsSpeed()
        {
            return Settings.PlaySenseSettings.Active.replaySpeed;
        }

        private void OnSessionsChanged()
        {
            if (_combined)
                Reload();

            Repaint();
        }

        private void OnSceneChangedInEditor(Scene previous, Scene next)
        {
            Reload();
        }

        // =====================================================
        // LOADING
        // =====================================================

        private void Reload()
        {
            _context.Sessions.Clear();
            _context.Combined = _combined;
            _context.SceneName = SceneManager.GetActiveScene().name;
            _loadedScene = _context.SceneName;
            _playing = false;

            if (_combined)
            {
                foreach (PlaySenseSessionCache.Entry entry in PlaySenseSessionCache.Entries)
                {
                    if (entry.Session.GetSceneNames().Contains(_context.SceneName))
                        _context.Sessions.Add(Build(entry, false));
                }
            }
            else if (!string.IsNullOrEmpty(_path))
            {
                PlaySenseSessionCache.Entry entry = PlaySenseSessionCache.Find(_path);
                if (entry != null)
                    _context.Sessions.Add(Build(entry, true));
            }

            _time = Mathf.Clamp(_time, 0f, Duration);
            _context.Time = _time;

            foreach (PlaySenseReplayLayer layer in PlaySenseExtensionRegistry.ReplayLayers)
                layer.OnLoad(_context);

            Repaint();
            SceneView.RepaintAll();
        }

        private PlaySenseReplaySession Build(PlaySenseSessionCache.Entry entry, bool primary)
        {
            PlaySenseReplaySession replay = new PlaySenseReplaySession
            {
                Path = entry.Path,
                Data = entry.Session,
                IsPrimary = primary
            };

            foreach (string agent in SessionQueries.GetAgentIds(entry.Session))
            {
                List<FrameData> frames = SessionQueries.GetFrames(entry.Session, agent, _context.SceneName);
                if (frames.Count == 0)
                    continue;

                replay.Agents.Add(agent);
                replay.Frames[agent] = frames;
            }

            return replay;
        }

        private float Duration
        {
            get
            {
                PlaySenseReplaySession primary = _context.Primary;
                return primary != null && !_combined ? primary.Data.Duration : 0f;
            }
        }

        private bool IsLayerVisible(PlaySenseReplayLayer layer)
        {
            return layer.VisibleByDefault ? !_hiddenLayers.Contains(layer.Title) : _shownLayers.Contains(layer.Title);
        }

        private void SetLayerVisible(PlaySenseReplayLayer layer, bool visible)
        {
            _hiddenLayers.Remove(layer.Title);
            _shownLayers.Remove(layer.Title);

            if (layer.VisibleByDefault && !visible)
                _hiddenLayers.Add(layer.Title);
            else if (!layer.VisibleByDefault && visible)
                _shownLayers.Add(layer.Title);
        }

        // =====================================================
        // PLAYBACK
        // =====================================================

        private void Tick()
        {
            if (_loadedScene != SceneManager.GetActiveScene().name)
                Reload();

            if (!_playing || _combined)
            {
                _lastTick = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            _time += (float)(now - _lastTick) * _speed;
            _lastTick = now;

            if (_time >= Duration)
            {
                _time = Duration;
                _playing = false;
            }

            _context.Time = _time;

            if (_followAgent)
                FollowPrimaryAgent();

            Repaint();
            SceneView.RepaintAll();
        }

        private void SetTime(float time)
        {
            _time = Mathf.Clamp(time, 0f, Duration);
            _context.Time = _time;
            Repaint();
            SceneView.RepaintAll();
        }

        private void FollowPrimaryAgent()
        {
            PlaySenseReplaySession primary = _context.Primary;
            SceneView view = SceneView.lastActiveSceneView;
            if (primary == null || primary.Agents.Count == 0 || view == null)
                return;

            Vector3 position;
            Quaternion rotation;
            Vector3 direction;
            if (SessionQueries.SampleAt(primary.Frames[primary.Agents[0]], _time, out position, out rotation, out direction))
                view.LookAt(position, view.rotation, view.size, false, true);
        }

        private void FrameAll()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
                return;

            bool has = false;
            Bounds bounds = new Bounds();

            foreach (PlaySenseReplaySession session in _context.Sessions)
            {
                foreach (List<FrameData> frames in session.Frames.Values)
                {
                    foreach (FrameData frame in frames)
                    {
                        if (!has)
                        {
                            bounds = new Bounds(frame.Position, Vector3.one);
                            has = true;
                        }
                        else
                        {
                            bounds.Encapsulate(frame.Position);
                        }
                    }
                }
            }

            if (has)
            {
                bounds.Expand(4f);
                view.Frame(bounds, false);
            }
        }

        // =====================================================
        // WINDOW GUI
        // =====================================================

        private void OnGUI()
        {
            DrawToolbar();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_context.Sessions.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _combined
                        ? "No sessions were recorded in the open scene (" + _context.SceneName + "). Open the scene you playtested."
                        : "Pick a session above, or switch to All Sessions to overlay everything recorded in this scene.",
                    MessageType.Info);
            }
            else
            {
                if (!_combined)
                    DrawSingle();
                else
                    GUILayout.Label(_context.Sessions.Count + " session(s) recorded in " + _context.SceneName + ", overlaid.", EditorStyles.miniLabel);

                GUILayout.Space(6f);
                DrawLayers();

                if (!_combined)
                {
                    GUILayout.Space(6f);
                    DrawMoments();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int mode = GUILayout.Toolbar(_combined ? 1 : 0, new[] { "Single", "All Sessions" }, EditorStyles.toolbarButton, GUILayout.Width(170f));
            if ((mode == 1) != _combined)
            {
                bool combined = mode == 1;
                EditorApplication.delayCall += () =>
                {
                    _combined = combined;
                    Reload();
                };
            }

            if (!_combined)
            {
                List<PlaySenseSessionCache.Entry> entries = PlaySenseSessionCache.Entries;
                string[] labels = new string[entries.Count];
                int index = -1;
                for (int i = 0; i < entries.Count; i++)
                {
                    labels[i] = entries[i].FileName + "  (" + string.Join(", ", entries[i].Session.GetSceneNames().ToArray()) + ")";
                    if (entries[i].Path == _path)
                        index = i;
                }

                int picked = EditorGUILayout.Popup(index, labels, EditorStyles.toolbarPopup);
                if (picked != index && picked >= 0)
                {
                    string path = entries[picked].Path;
                    EditorApplication.delayCall += () =>
                    {
                        _path = path;
                        _time = 0f;
                        Reload();
                    };
                }
            }
            else
            {
                GUILayout.FlexibleSpace();
            }

            if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                FrameAll();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSingle()
        {
            SessionData session = _context.Primary.Data;

            if (_context.Primary.Agents.Count == 0)
            {
                List<string> scenes = session.GetSceneNames();
                EditorGUILayout.HelpBox(
                    "This session has no movement in the open scene (" + _context.SceneName + "). It visited: " + string.Join(", ", scenes.ToArray()) + ".",
                    MessageType.Warning);

                foreach (string scene in scenes)
                {
                    if (GUILayout.Button("Open " + scene))
                        OpenScene(scene);
                }
            }

            float duration = Duration;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("|<", GUILayout.Width(30f)))
                SetTime(0f);

            if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(60f)))
            {
                if (!_playing && _time >= duration)
                    SetTime(0f);

                _playing = !_playing;
                _lastTick = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button(">|", GUILayout.Width(30f)))
                SetTime(duration);

            GUILayout.Label(_time.ToString("F1") + " / " + duration.ToString("F1") + " s");
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            float time = EditorGUILayout.Slider(_time, 0f, Mathf.Max(duration, 0.01f));
            if (EditorGUI.EndChangeCheck())
                SetTime(time);

            _speed = EditorGUILayout.Slider("Speed", _speed, 0.1f, 10f);
            _followAgent = EditorGUILayout.ToggleLeft("Follow player while playing", _followAgent);
        }

        private void DrawLayers()
        {
            GUILayout.Label("Layers", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            foreach (PlaySenseReplayLayer layer in PlaySenseExtensionRegistry.ReplayLayers)
            {
                bool visible = IsLayerVisible(layer);
                bool now = EditorGUILayout.ToggleLeft(layer.Title, visible);
                if (now != visible)
                    SetLayerVisible(layer, now);

                if (now)
                {
                    EditorGUI.indentLevel++;
                    layer.DrawOptions(_context);
                    EditorGUI.indentLevel--;
                }
            }

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }

        // Clickable list of attention and events.
        private void DrawMoments()
        {
            GUILayout.Label("Moments", EditorStyles.boldLabel);
            SessionData session = _context.Primary.Data;
            List<KeyValuePair<float, string>> moments = new List<KeyValuePair<float, string>>();

            foreach (BehaviourObservation observation in session.BehaviourObservations)
            {
                if (observation != null && _context.InScene(session, observation.Scene))
                {
                    moments.Add(new KeyValuePair<float, string>(observation.Timestamp,
                        "Looked at " + observation.Subject + " (" + observation.Duration.ToString("F1") + "s)" + (observation.IsReinspection ? " again" : string.Empty)));
                }
            }

            foreach (GameEventData gameEvent in session.Events)
            {
                if (gameEvent != null)
                    moments.Add(new KeyValuePair<float, string>(gameEvent.Timestamp, gameEvent.ToString()));
            }

            moments.Sort((a, b) => a.Key.CompareTo(b.Key));

            if (moments.Count == 0)
                GUILayout.Label("No attention or events in this session.", EditorStyles.miniLabel);

            foreach (KeyValuePair<float, string> moment in moments)
            {
                GUIStyle style = moment.Key <= _time ? EditorStyles.label : EditorStyles.miniLabel;
                if (GUILayout.Button(moment.Key.ToString("F1").PadLeft(6) + "s   " + moment.Value, style))
                    SetTime(moment.Key);
            }
        }

        private static void OpenScene(string sceneName)
        {
            foreach (string guid in AssetDatabase.FindAssets(sceneName + " t:Scene"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(assetPath) != sceneName)
                    continue;

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(assetPath);

                return;
            }

            EditorUtility.DisplayDialog("PlaySense", "Could not find a scene named \"" + sceneName + "\" in this project.", "OK");
        }

        // =====================================================
        // SCENE VIEW
        // =====================================================

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_context.Sessions.Count == 0 || Event.current.type != EventType.Repaint)
                return;

            Color previousColor = Handles.color;
            UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            foreach (PlaySenseReplayLayer layer in PlaySenseExtensionRegistry.ReplayLayers)
            {
                if (!IsLayerVisible(layer))
                    continue;

                try
                {
                    layer.DrawScene(_context);
                }
                catch (System.Exception exception)
                {
                    Debug.LogException(exception);
                }
            }

            Handles.zTest = previousZTest;
            Handles.color = previousColor;
        }
    }

    /// <summary>"Replay in Scene View" button in the Dashboard.</summary>
    public class ReplaySessionAction : PlaySenseSessionAction
    {
        public override string Title
        {
            get { return "Replay in Scene View"; }
        }

        public override int Order
        {
            get { return 0; }
        }

        public override void Run(SessionData session, string path)
        {
            PlaySenseReplayWindow.Open(path);
        }
    }
}
