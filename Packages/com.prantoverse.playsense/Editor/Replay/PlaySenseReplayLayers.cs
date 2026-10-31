using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PlaySense.Analysis;
using PlaySense.Data.Models;

namespace PlaySense.EditorTools
{
    /// <summary>Where players spent time. In All Sessions mode, every session is added together.</summary>
    public class HeatmapReplayLayer : PlaySenseReplayLayer
    {
        private struct Cell
        {
            public float Time;
            public float HeightSum;
            public int Samples;
        }

        private readonly Dictionary<Vector2Int, Cell> _cells = new Dictionary<Vector2Int, Cell>();
        private float _max;
        private float _cellSize = -1f;
        private float _opacity = 0.45f;
        private PlaySenseReplayContext _lastContext;

        public override string Title { get { return "Heatmap (time spent)"; } }
        public override int Order { get { return 0; } }

        public override void OnLoad(PlaySenseReplayContext context)
        {
            _lastContext = context;
            if (_cellSize <= 0f)
                _cellSize = context.Settings.heatmapCellSize;

            Build(context);
        }

        public override void DrawOptions(PlaySenseReplayContext context)
        {
            float cell = EditorGUILayout.Slider("Cell size (m)", _cellSize, 0.25f, 5f);
            if (!Mathf.Approximately(cell, _cellSize))
            {
                _cellSize = cell;
                Build(_lastContext ?? context);
            }

            _opacity = EditorGUILayout.Slider("Opacity", _opacity, 0.05f, 1f);
        }

        private void Build(PlaySenseReplayContext context)
        {
            _cells.Clear();
            _max = 0f;
            float size = Mathf.Max(0.1f, _cellSize);

            foreach (PlaySenseReplaySession session in context.Sessions)
            {
                foreach (List<FrameData> frames in session.Frames.Values)
                {
                    for (int i = 0; i < frames.Count; i++)
                    {
                        // Time spent here = time until the next sample.
                        float dt = i + 1 < frames.Count ? frames[i + 1].Timestamp - frames[i].Timestamp : 0f;
                        if (dt <= 0f || dt > 1f)
                            dt = 0.05f;

                        Vector3 p = frames[i].Position;
                        Vector2Int key = new Vector2Int(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));

                        Cell cell;
                        _cells.TryGetValue(key, out cell);
                        cell.Time += dt;
                        cell.HeightSum += p.y;
                        cell.Samples++;
                        _cells[key] = cell;

                        if (cell.Time > _max)
                            _max = cell.Time;
                    }
                }
            }
        }

        public override void DrawScene(PlaySenseReplayContext context)
        {
            if (_max <= 0f)
                return;

            float size = Mathf.Max(0.1f, _cellSize);
            Vector3[] quad = new Vector3[4];

            foreach (KeyValuePair<Vector2Int, Cell> pair in _cells)
            {
                float t = Mathf.Sqrt(pair.Value.Time / _max);
                float y = pair.Value.HeightSum / Mathf.Max(1, pair.Value.Samples) + 0.02f;
                float x0 = pair.Key.x * size;
                float z0 = pair.Key.y * size;

                quad[0] = new Vector3(x0, y, z0);
                quad[1] = new Vector3(x0 + size, y, z0);
                quad[2] = new Vector3(x0 + size, y, z0 + size);
                quad[3] = new Vector3(x0, y, z0 + size);

                Color color = Heat(t);
                color.a = _opacity * Mathf.Lerp(0.35f, 1f, t);
                Handles.DrawSolidRectangleWithOutline(quad, color, Color.clear);
            }
        }

        private static Color Heat(float t)
        {
            return t < 0.5f
                ? Color.Lerp(new Color(0.1f, 0.3f, 1f), new Color(1f, 0.9f, 0.1f), t * 2f)
                : Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(1f, 0.15f, 0.1f), (t - 0.5f) * 2f);
        }
    }

    /// <summary>Where players walked. Single mode highlights the part travelled so far.</summary>
    public class PathReplayLayer : PlaySenseReplayLayer
    {
        public override string Title { get { return "Paths"; } }
        public override int Order { get { return 10; } }

        public override void DrawScene(PlaySenseReplayContext context)
        {
            float maxSpeed = context.Settings.maxPlausibleSpeed;

            for (int s = 0; s < context.Sessions.Count; s++)
            {
                PlaySenseReplaySession session = context.Sessions[s];

                for (int a = 0; a < session.Agents.Count; a++)
                {
                    List<FrameData> frames = session.Frames[session.Agents[a]];
                    Color color = PlaySenseReplayContext.AgentColor(context.Combined ? s : a);

                    // Whole path, faint (fainter still when overlaying many sessions).
                    Handles.color = new Color(color.r, color.g, color.b, context.Combined ? 0.35f : 0.25f);
                    DrawSegments(frames, float.MaxValue, maxSpeed, 2f);

                    if (!context.Combined)
                    {
                        Handles.color = color;
                        DrawSegments(frames, context.Time, maxSpeed, 4f);
                    }
                }
            }
        }

        private static void DrawSegments(List<FrameData> frames, float untilTime, float maxSpeed, float width)
        {
            List<Vector3> points = new List<Vector3>();

            for (int i = 0; i < frames.Count && frames[i].Timestamp <= untilTime; i++)
            {
                if (i > 0 && SessionQueries.IsBreak(frames[i - 1], frames[i], maxSpeed))
                {
                    if (points.Count > 1)
                        Handles.DrawAAPolyLine(width, points.ToArray());
                    points.Clear();
                }

                points.Add(frames[i].Position);
            }

            if (points.Count > 1)
                Handles.DrawAAPolyLine(width, points.ToArray());
        }
    }

    /// <summary>The player's position and view direction at the current time (single mode).</summary>
    public class PlayerReplayLayer : PlaySenseReplayLayer
    {
        public override string Title { get { return "Player & view direction"; } }
        public override int Order { get { return 20; } }

        public override void DrawScene(PlaySenseReplayContext context)
        {
            if (context.Combined || context.Primary == null)
                return;

            PlaySenseReplaySession session = context.Primary;
            float attentionAngle = context.Settings.attentionAngle;

            for (int a = 0; a < session.Agents.Count; a++)
            {
                List<FrameData> frames = session.Frames[session.Agents[a]];

                // Hide the marker while this agent is in another scene.
                if (frames.Count == 0 || context.Time < frames[0].Timestamp - 0.5f || context.Time > frames[frames.Count - 1].Timestamp + 0.5f)
                    continue;

                Vector3 position;
                Quaternion rotation;
                Vector3 view;
                if (!SessionQueries.SampleAt(frames, context.Time, out position, out rotation, out view))
                    continue;

                Color color = PlaySenseReplayContext.AgentColor(a);
                float size = HandleUtility.GetHandleSize(position) * 0.25f;

                Handles.color = color;
                Handles.SphereHandleCap(0, position, Quaternion.identity, size, EventType.Repaint);

                if (view.sqrMagnitude > 0.0001f)
                {
                    Vector3 direction = view.normalized;
                    float length = HandleUtility.GetHandleSize(position) * 2.5f;
                    Handles.DrawAAPolyLine(3f, position, position + direction * length);

                    Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                    Vector3 right = Vector3.Cross(up, direction).normalized;
                    Handles.color = new Color(color.r, color.g, color.b, 0.5f);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(attentionAngle, up) * direction * length);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(-attentionAngle, up) * direction * length);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(attentionAngle, right) * direction * length);
                    Handles.DrawAAPolyLine(1.5f, position, position + Quaternion.AngleAxis(-attentionAngle, right) * direction * length);
                }

                if (session.Agents.Count > 1)
                    Handles.Label(position + Vector3.up * size * 2f, session.Agents[a], EditorStyles.whiteMiniLabel);
            }
        }
    }

    /// <summary>Gameplay events with a position. All Sessions mode shows every event in the scene.</summary>
    public class EventsReplayLayer : PlaySenseReplayLayer
    {
        private static readonly Color EventColor = new Color(1f, 0.85f, 0.2f);
        private const float HighlightSeconds = 2f;

        private bool _showLabels = true;

        public override string Title { get { return "Events"; } }
        public override int Order { get { return 40; } }

        public override void DrawOptions(PlaySenseReplayContext context)
        {
            _showLabels = EditorGUILayout.Toggle("Labels", _showLabels);
        }

        public override void DrawScene(PlaySenseReplayContext context)
        {
            foreach (PlaySenseReplaySession session in context.Sessions)
            {
                foreach (GameEventData gameEvent in session.Data.Events)
                {
                    if (gameEvent == null || !context.InScene(session.Data, gameEvent.Scene))
                        continue;

                    if (!gameEvent.HasPosition && gameEvent.Position == Vector3.zero)
                        continue;

                    if (!context.Combined && gameEvent.Timestamp > context.Time)
                        continue;

                    bool recent = !context.Combined && context.Time - gameEvent.Timestamp <= HighlightSeconds;
                    float size = HandleUtility.GetHandleSize(gameEvent.Position) * (recent ? 0.22f : 0.12f);

                    Handles.color = recent ? EventColor : new Color(EventColor.r, EventColor.g, EventColor.b, 0.5f);
                    Handles.CubeHandleCap(0, gameEvent.Position, Quaternion.identity, size, EventType.Repaint);

                    if (_showLabels && recent)
                        Handles.Label(gameEvent.Position + Vector3.up * size * 2f, gameEvent.ToString(), EditorStyles.whiteBoldLabel);
                }
            }
        }
    }
}
