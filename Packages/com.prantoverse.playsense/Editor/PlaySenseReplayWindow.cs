using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlaySense.Analysis;
using PlaySense.Data.Models;
using PlaySense.Settings;
using PlaySense.Storage;

namespace PlaySense.EditorTools
{
    /// <summary>
    /// Plays a recorded session back in the Scene view: the path, a time-spent
    /// heatmap, what the player looked at and where events happened.
    /// </summary>
    public class PlaySenseReplayWindow : EditorWindow
    {
        private static readonly Color[] AgentColors =
        {
            new Color(0.2f, 0.75f, 1f),
            new Color(1f, 0.55f, 0.2f),
            new Color(0.6f, 1f, 0.3f),
            new Color(0.95f, 0.35f, 0.85f)
        };

        private static readonly Color AttentionColor = new Color(0.3f, 1f, 0.45f);
        private static readonly Color EventColor = new Color(1f, 0.85f, 0.2f);

        private const float EventHighlightSeconds = 2f;

        // Session
        [SerializeField] private string _path;
        private SessionData _session;
        private readonly List<string> _agents = new List<string>();
        private readonly Dictionary<string, List<FrameData>> _frames = new Dictionary<string, List<FrameData>>();
        private readonly Dictionary<string, List<Vector3[]>> _pathSegments = new Dictionary<string, List<Vector3[]>>();
        private string[] _sessionFiles = new string[0];
        private string[] _sessionLabels = new string[0];

        // Playback
        [SerializeField] private float _time;
        [SerializeField] private float _speed = -1f;
        private bool _playing;
        private double _lastTick;

        // Display
        [SerializeField] private bool _showPath = true;
        [SerializeField] private bool _showHeatmap = true;
        [SerializeField] private bool _showAttention = true;
        [SerializeField] private bool _showEvents = true;
        [SerializeField] private bool _showViewCone = true;
        [SerializeField] private bool _followAgent;
        [SerializeField] private float _cellSize = -1f;
        [SerializeField] private float _heatmapOpacity = 0.45f;

        // Heatmap
        private struct HeatCell
        {
            public float Time;
            public float HeightSum;
            public int Samples;
        }

        private readonly Dictionary<Vector2Int, HeatCell> _heat = new Dictionary<Vector2Int, HeatCell>();
        private float _heatMax;

        private Vector2 _listScroll;

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
                window.Load(path);
        }

        private void OnEnable()
        {
            PlaySenseSettings settings = PlaySenseSettings.Active;
            if (_speed <= 0f)
                _speed = settings.replaySpeed;
            if (_cellSize <= 0f)
                _cellSize = settings.heatmapCellSize;

            RefreshFileList();

            if (!string.IsNullOrEmpty(_path) && File.Exists(_path))
                Load(_path, keepTime: true);

            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= Tick;
            SceneView.RepaintAll();
        }

        // =====================================================
        // LOADING
        // =====================================================

        private void RefreshFileList()
        {
            _sessionFiles = SessionStorage.GetSessionFiles();
            _sessionLabels = new string[_sessionFiles.Length];
            for (int i = 0; i < _sessionFiles.Length; i++)
                _sessionLabels[i] = Path.GetFileNameWithoutExtension(_sessionFiles[i]);
        }

        private void Load(string path, bool keepTime = false)
        {
            SessionData session = SessionStorage.Load(path);
            if (session == null)
                return;

            _path = path;
            _session = session;
            _playing = false;

            if (!keepTime)
                _time = 0f;

            _agents.Clear();
            _frames.Clear();
            _pathSegments.Clear();

            foreach (string agent in SessionQueries.GetAgentIds(session))
            {
                List<FrameData> frames = SessionQueries.GetFrames(session, agent);
                _agents.Add(agent);
                _frames[agent] = frames;
                _pathSegments[agent] = BuildSegments(frames);
            }

            _time = Mathf.Clamp(_time, 0f, Duration);
            BuildHeatmap();
            RefreshFileList();
            Repaint();
            SceneView.RepaintAll();
        }

        private float Duration
        {
            get
            {
                if (_session == null)
                    return 0f;

                float duration = _session.Duration;
                foreach (List<FrameData> frames in _frames.Values)
                {
                    if (frames.Count > 0)
                        duration = Mathf.Max(duration, frames[frames.Count - 1].Timestamp);
                }

                return duration;
            }
        }

        // Split the path where the agent teleported so we don't draw a line across the level.
        private static List<Vector3[]> BuildSegments(List<FrameData> frames)
        {
            List<Vector3[]> segments = new List<Vector3[]>();
            List<Vector3> current = new List<Vector3>();
            float maxSpeed = PlaySenseSettings.Active.maxPlausibleSpeed;

            for (int i = 0; i < frames.Count; i++)
            {
                if (i > 0)
                {
                    float dt = frames[i].Timestamp - frames[i - 1].Timestamp;
                    float step = Vector3.Distance(frames[i].Position, frames[i - 1].Position);

                    if (dt <= 0f || step / dt > maxSpeed)
                    {
                        if (current.Count > 1)
                            segments.Add(current.ToArray());
                        current.Clear();
                    }
                }

                current.Add(frames[i].Position);
            }

            if (current.Count > 1)
                segments.Add(current.ToArray());

            return segments;
        }

        private void BuildHeatmap()
        {
            _heat.Clear();
            _heatMax = 0f;

            if (_session == null)
                return;

            float cell = Mathf.Max(0.1f, _cellSize);

            foreach (List<FrameData> frames in _frames.Values)
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    // Time spent here = time until the next sample.
                    float dt = i + 1 < frames.Count ? frames[i + 1].Timestamp - frames[i].Timestamp : 0f;
                    if (dt <= 0f || dt > 1f)
                        dt = 0.05f;

                    Vector3 p = frames[i].Position;
                    Vector2Int key = new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.z / cell));

                    HeatCell heat;
                    _heat.TryGetValue(key, out heat);
                    heat.Time += dt;
                    heat.HeightSum += p.y;
                    heat.Samples++;
                    _heat[key] = heat;

                    if (heat.Time > _heatMax)
                        _heatMax = heat.Time;
                }
            }
        }

        // =====================================================
        // PLAYBACK
        // =====================================================

        private void Tick()
        {
            if (!_playing || _session == null)
            {
                _lastTick = EditorApplication.timeSinceStartup;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float delta = (float)(now - _lastTick);
            _lastTick = now;

            _time += delta * _speed;
            if (_time >= Duration)
            {
                _time = Duration;
                _playing = false;
            }

            if (_followAgent)
                FollowPrimaryAgent();

            Repaint();
            SceneView.RepaintAll();
        }

        private void FollowPrimaryAgent()
        {
            if (_agents.Count == 0 || SceneView.lastActiveSceneView == null)
                return;

            Vector3 position;
            Quaternion rotation;
            Vector3 view;

            if (SessionQueries.SampleAt(_frames[_agents[0]], _time, out position, out rotation, out view))
                SceneView.lastActiveSceneView.LookAt(position, SceneView.lastActiveSceneView.rotation, SceneView.lastActiveSceneView.size, false, true);
        }

        private void FrameSession()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null || _session == null)
                return;

            bool hasBounds = false;
            Bounds bounds = new Bounds();

            foreach (List<FrameData> frames in _frames.Values)
            {
                foreach (FrameData frame in frames)
                {
                    if (!hasBounds)
                    {
                        bounds = new Bounds(frame.Position, Vector3.one);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(frame.Position);
                    }
                }
            }

            if (hasBounds)
            {
                bounds.Expand(4f);
                sceneView.Frame(bounds, false);
            }
        }

        // =====================================================
        // WINDOW GUI
        // =====================================================

        private void OnGUI()
        {
            DrawSessionPicker();

            if (_session == null)
            {
                EditorGUILayout.HelpBox("Pick a recorded session to replay it in the Scene view.", MessageType.Info);
                return;
            }

            if (_session.SceneName != SceneManager.GetActiveScene().name)
            {
                EditorGUILayout.HelpBox(
                    "This session was recorded in scene \"" + _session.SceneName + "\". Open it so the replay lines up with the level.",
                    MessageType.Warning);

                if (GUILayout.Button("Open " + _session.SceneName))
                    OpenRecordedScene();
            }

            DrawTimeline();
            GUILayout.Space(6f);
            DrawDisplayOptions();
            GUILayout.Space(6f);
            DrawMoments();
        }

        private void DrawSessionPicker()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            int index = System.Array.IndexOf(_sessionFiles, _path);
            int picked = EditorGUILayout.Popup(index, _sessionLabels, EditorStyles.toolbarPopup);
            if (picked != index && picked >= 0)
                Load(_sessionFiles[picked]);

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                RefreshFileList();

            using (new EditorGUI.DisabledScope(_session == null))
            {
                if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(50f)))
                    FrameSession();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTimeline()
        {
            float duration = Duration;

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("|<", GUILayout.Width(30f)))
                SetTime(0f);

            if (GUILayout.Button(_playing ? "Pause" : "Play", GUILayout.Width(60f)))
            {
                if (!_playing && _time >= duration)
                    _time = 0f;

                _playing = !_playing;
                _lastTick = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button(">|", GUILayout.Width(30f)))
                SetTime(duration);

            GUILayout.Label(_time.ToString("F1") + " / " + duration.ToString("F1") + " s", GUILayout.Width(110f));

            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            float time = EditorGUILayout.Slider(_time, 0f, Mathf.Max(duration, 0.01f));
            if (EditorGUI.EndChangeCheck())
                SetTime(time);

            _speed = EditorGUILayout.Slider("Speed", _speed, 0.1f, 10f);
        }

        private void DrawDisplayOptions()
        {
            EditorGUI.BeginChangeCheck();

            _showPath = EditorGUILayout.ToggleLeft("Path", _showPath);
            _showHeatmap = EditorGUILayout.ToggleLeft("Heatmap (time spent)", _showHeatmap);

            if (_showHeatmap)
            {
                EditorGUI.indentLevel++;
                float cell = EditorGUILayout.Slider("Cell size (m)", _cellSize, 0.25f, 5f);
                if (!Mathf.Approximately(cell, _cellSize))
                {
                    _cellSize = cell;
                    BuildHeatmap();
                }

                _heatmapOpacity = EditorGUILayout.Slider("Opacity", _heatmapOpacity, 0.05f, 1f);
                EditorGUI.indentLevel--;
            }

            _showAttention = EditorGUILayout.ToggleLeft("Attention", _showAttention);
            _showEvents = EditorGUILayout.ToggleLeft("Events", _showEvents);
            _showViewCone = EditorGUILayout.ToggleLeft("View direction", _showViewCone);
            _followAgent = EditorGUILayout.ToggleLeft("Follow player while playing", _followAgent);

            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }

        // Clickable list of attention and events, highlighted when active.
        private void DrawMoments()
        {
            GUILayout.Label("Moments", EditorStyles.boldLabel);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            List<KeyValuePair<float, string>> moments = new List<KeyValuePair<float, string>>();

            foreach (BehaviourObservation observation in _session.BehaviourObservations)
            {
                if (observation != null)
                    moments.Add(new KeyValuePair<float, string>(observation.Timestamp,
                        "Looked at " + observation.Subject + " (" + observation.Duration.ToString("F1") + "s)" +
                        (observation.IsReinspection ? "  again" : string.Empty)));
            }

            foreach (GameEventData gameEvent in _session.Events)
            {
                if (gameEvent != null)
                    moments.Add(new KeyValuePair<float, string>(gameEvent.Timestamp, gameEvent.ToString()));
            }

            moments.Sort((a, b) => a.Key.CompareTo(b.Key));

            if (moments.Count == 0)
                GUILayout.Label("No attention or events in this session.", EditorStyles.miniLabel);

            foreach (KeyValuePair<float, string> moment in moments)
            {
                bool past = moment.Key <= _time;
                GUIStyle style = past ? EditorStyles.label : EditorStyles.miniLabel;

                if (GUILayout.Button(moment.Key.ToString("F1").PadLeft(6) + "s   " + moment.Value, style))
                    SetTime(moment.Key);
            }

            EditorGUILayout.EndScrollView();
        }

        private void SetTime(float time)
        {
            _time = Mathf.Clamp(time, 0f, Duration);
            Repaint();
            SceneView.RepaintAll();
        }

        private void OpenRecordedScene()
        {
            string[] guids = AssetDatabase.FindAssets(_session.SceneName + " t:Scene");

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(assetPath) != _session.SceneName)
                    continue;

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(assetPath);

                return;
            }

            EditorUtility.DisplayDialog("PlaySense", "Could not find a scene named \"" + _session.SceneName + "\" in this project.", "OK");
        }

        // =====================================================
        // SCENE VIEW DRAWING
        // =====================================================

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_session == null || Event.current.type != EventType.Repaint)
                return;

            Color previousColor = Handles.color;
            UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

            if (_showHeatmap)
                DrawHeatmap();

            if (_showPath)
                DrawPaths();

            if (_showAttention)
                DrawAttention();

            if (_showEvents)
                DrawEvents();

            DrawAgents();

            Handles.zTest = previousZTest;
            Handles.color = previousColor;
        }

        private void DrawHeatmap()
        {
            if (_heatMax <= 0f)
                return;

            float cell = Mathf.Max(0.1f, _cellSize);
            Vector3[] quad = new Vector3[4];

            foreach (KeyValuePair<Vector2Int, HeatCell> pair in _heat)
            {
                float t = Mathf.Sqrt(pair.Value.Time / _heatMax);
                float y = pair.Value.HeightSum / Mathf.Max(1, pair.Value.Samples) + 0.02f;
                float x0 = pair.Key.x * cell;
                float z0 = pair.Key.y * cell;

                quad[0] = new Vector3(x0, y, z0);
                quad[1] = new Vector3(x0 + cell, y, z0);
                quad[2] = new Vector3(x0 + cell, y, z0 + cell);
                quad[3] = new Vector3(x0, y, z0 + cell);

                Color color = Heat(t);
                color.a = _heatmapOpacity * Mathf.Lerp(0.35f, 1f, t);
                Handles.DrawSolidRectangleWithOutline(quad, color, Color.clear);
            }
        }

        private static Color Heat(float t)
        {
            if (t < 0.5f)
                return Color.Lerp(new Color(0.1f, 0.3f, 1f), new Color(1f, 0.9f, 0.1f), t * 2f);

            return Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(1f, 0.15f, 0.1f), (t - 0.5f) * 2f);
        }

        private void DrawPaths()
        {
            for (int a = 0; a < _agents.Count; a++)
            {
                Color color = AgentColors[a % AgentColors.Length];
                List<FrameData> frames = _frames[_agents[a]];

                // Whole path, faint.
                Handles.color = new Color(color.r, color.g, color.b, 0.25f);
                foreach (Vector3[] segment in _pathSegments[_agents[a]])
                    Handles.DrawAAPolyLine(2f, segment);

                // Travelled so far, bright.
                List<Vector3> travelled = new List<Vector3>();
                float maxSpeed = PlaySenseSettings.Active.maxPlausibleSpeed;
                Handles.color = color;

                for (int i = 0; i < frames.Count && frames[i].Timestamp <= _time; i++)
                {
                    if (i > 0)
                    {
                        float dt = frames[i].Timestamp - frames[i - 1].Timestamp;
                        if (dt <= 0f || Vector3.Distance(frames[i].Position, frames[i - 1].Position) / dt > maxSpeed)
                        {
                            if (travelled.Count > 1)
                                Handles.DrawAAPolyLine(4f, travelled.ToArray());
                            travelled.Clear();
                        }
                    }

                    travelled.Add(frames[i].Position);
                }

                if (travelled.Count > 1)
                    Handles.DrawAAPolyLine(4f, travelled.ToArray());
            }
        }

        private void DrawAgents()
        {
            float attentionAngle = PlaySenseSettings.Active.attentionAngle;

            for (int a = 0; a < _agents.Count; a++)
            {
                Vector3 position;
                Quaternion rotation;
                Vector3 view;

                if (!SessionQueries.SampleAt(_frames[_agents[a]], _time, out position, out rotation, out view))
                    continue;

                Color color = AgentColors[a % AgentColors.Length];
                float size = HandleUtility.GetHandleSize(position) * 0.25f;

                Handles.color = color;
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);

                if (_showViewCone && view.sqrMagnitude > 0.0001f)
                {
                    Vector3 direction = view.normalized;
                    float length = HandleUtility.GetHandleSize(position) * 2.5f;

                    Handles.DrawAAPolyLine(3f, position, position + direction * length);

                    // Attention cone outline.
                    Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                    Vector3 right = Vector3.Cross(up, direction).normalized;
                    Handles.color = new Color(color.r, color.g, color.b, 0.5f);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(attentionAngle, up) * direction * length);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(-attentionAngle, up) * direction * length);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(attentionAngle, right) * direction * length);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(-attentionAngle, right) * direction * length);
                }

                if (_agents.Count > 1)
                    Handles.Label(position + Vector3.up * size * 2f, _agents[a], EditorStyles.whiteMiniLabel);
            }
        }

        private void DrawAttention()
        {
            Dictionary<string, Vector3> agentPositions = new Dictionary<string, Vector3>();

            foreach (BehaviourObservation observation in _session.BehaviourObservations)
            {
                if (observation == null || observation.Timestamp > _time)
                    continue;

                bool active = _time <= observation.EndTime;
                float radius = 0.25f + Mathf.Min(observation.Duration, 10f) * 0.08f;

                Handles.color = active ? AttentionColor : new Color(AttentionColor.r, AttentionColor.g, AttentionColor.b, 0.35f);
                Handles.DrawWireDisc(observation.Position, Vector3.up, radius, active ? 3f : 1.5f);

                if (!active)
                    continue;

                Handles.Label(observation.Position + Vector3.up * (radius + 0.2f),
                    observation.Subject + "  " + (_time - observation.Timestamp).ToString("F1") + "s",
                    EditorStyles.whiteBoldLabel);

                // Line from the looker to the target.
                string agent = string.IsNullOrEmpty(observation.AgentId) && _agents.Count > 0 ? _agents[0] : observation.AgentId;
                Vector3 agentPosition;

                if (!agentPositions.TryGetValue(agent, out agentPosition))
                {
                    List<FrameData> frames;
                    Quaternion rotation;
                    Vector3 view;

                    if (!_frames.TryGetValue(agent, out frames) ||
                        !SessionQueries.SampleAt(frames, _time, out agentPosition, out rotation, out view))
                        continue;

                    agentPositions[agent] = agentPosition;
                }

                Handles.DrawDottedLine(agentPosition, observation.Position, 4f);
            }
        }

        private void DrawEvents()
        {
            foreach (GameEventData gameEvent in _session.Events)
            {
                if (gameEvent == null || gameEvent.Timestamp > _time)
                    continue;

                bool hasPosition = gameEvent.HasPosition || gameEvent.Position != Vector3.zero;
                if (!hasPosition)
                    continue;

                bool recent = _time - gameEvent.Timestamp <= EventHighlightSeconds;
                float size = HandleUtility.GetHandleSize(gameEvent.Position) * (recent ? 0.22f : 0.12f);

                Handles.color = recent ? EventColor : new Color(EventColor.r, EventColor.g, EventColor.b, 0.45f);
                Handles.CubeHandleCap(0, gameEvent.Position, Quaternion.identity, size, EventType.Repaint);

                if (recent)
                    Handles.Label(gameEvent.Position + Vector3.up * size * 2f, gameEvent.ToString(), EditorStyles.whiteBoldLabel);
            }
        }
    }
}
