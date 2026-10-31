using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PlaySense.Analysis;
using PlaySense.Attention;
using PlaySense.Data.Models;

namespace PlaySense.EditorTools
{
    // =====================================================================
    // Dashboard tab
    // =====================================================================

    /// <summary>What players looked at.</summary>
    public class AttentionDashboardTab : PlaySenseDashboardTab
    {
        private class TargetTotals
        {
            public string Name;
            public string Category;
            public int Sessions;
            public int Looks;
            public float Seconds;
        }

        public override string Title { get { return "Attention"; } }
        public override int Order { get { return 20; } }
        public override bool SupportsOverview { get { return true; } }

        public override void DrawSession(SessionData session, string path)
        {
            BehaviourReport report = new SessionBehaviourAnalyzer().Analyze(session);

            if (report.AttentionCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No attention recorded. Attention needs PlaySensePerception on the player, PlaySenseView on the camera, " +
                    "and PlaySenseTarget on the objects you care about. The Welcome window walks through it.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            PlaySenseGUI.Metric("Looks", report.AttentionCount.ToString());
            PlaySenseGUI.Metric("Objects", report.UniqueAttendedObjects.ToString());
            PlaySenseGUI.Metric("Total time", report.TotalAttentionTime.ToString("F1") + " s");
            PlaySenseGUI.Metric("Reinspections", report.ReinspectionCount.ToString());
            EditorGUILayout.EndHorizontal();

            PlaySenseGUI.Section("Most attended");
            float longest = Mathf.Max(report.Targets[0].TotalDuration, 0.01f);
            foreach (TargetAttention target in report.Targets)
            {
                PlaySenseGUI.BarRow(
                    target.Name + (string.IsNullOrEmpty(target.Category) ? string.Empty : "  (" + target.Category + ")"),
                    target.TotalDuration / longest,
                    target.TotalDuration.ToString("F1") + " s, " + target.Count + " look(s)",
                    PlaySenseGUI.Accent);
            }

            PlaySenseGUI.Section("Timeline");
            foreach (BehaviourObservation observation in session.BehaviourObservations)
            {
                if (observation == null)
                    continue;

                GUILayout.Label(
                    observation.Timestamp.ToString("F1").PadLeft(7) + "s   " + observation.Subject +
                    "   " + observation.Duration.ToString("F1") + " s at " + observation.Distance.ToString("F1") + " m" +
                    (observation.IsReinspection ? "   (again)" : string.Empty),
                    EditorStyles.miniLabel);
            }
        }

        public override void DrawOverview(IList<SessionData> sessions, string sceneFilter)
        {
            Dictionary<string, TargetTotals> totals = new Dictionary<string, TargetTotals>();
            HashSet<string> seen = new HashSet<string>();

            foreach (SessionData session in sessions)
            {
                seen.Clear();
                foreach (BehaviourObservation observation in session.BehaviourObservations)
                {
                    if (observation == null)
                        continue;

                    if (sceneFilter != null && session.GetSceneName(observation.Scene) != sceneFilter)
                        continue;

                    string key = observation.Key ?? string.Empty;
                    TargetTotals target;
                    if (!totals.TryGetValue(key, out target))
                    {
                        target = new TargetTotals { Name = observation.Subject, Category = observation.Category };
                        totals.Add(key, target);
                    }

                    target.Looks++;
                    target.Seconds += observation.Duration;
                    if (seen.Add(key))
                        target.Sessions++;
                }
            }

            if (totals.Count == 0)
            {
                EditorGUILayout.HelpBox("No attention recorded in these sessions.", MessageType.Info);
                return;
            }

            List<TargetTotals> list = new List<TargetTotals>(totals.Values);
            list.Sort((a, b) => b.Sessions != a.Sessions ? b.Sessions.CompareTo(a.Sessions) : b.Seconds.CompareTo(a.Seconds));

            PlaySenseGUI.Section("What players noticed");
            GUILayout.Label("Bar = share of sessions where the player looked at it. Objects nobody looked at don't appear: compare with your list of targets.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(4f);

            float count = sessions.Count;
            foreach (TargetTotals target in list)
            {
                PlaySenseGUI.BarRow(
                    target.Name + (string.IsNullOrEmpty(target.Category) ? string.Empty : "  (" + target.Category + ")"),
                    target.Sessions / count,
                    target.Sessions + "/" + sessions.Count + " (" + PlaySenseGUI.Percent(target.Sessions / count * 100f) + "), avg " +
                    (target.Seconds / Mathf.Max(1, target.Sessions)).ToString("F1") + " s",
                    PlaySenseGUI.Accent);
            }
        }
    }

    // =====================================================================
    // Replay layer
    // =====================================================================

    /// <summary>Attention markers. All Sessions mode sizes each target by how many players looked at it.</summary>
    public class AttentionReplayLayer : PlaySenseReplayLayer
    {
        private static readonly Color AttentionColor = new Color(0.3f, 1f, 0.45f);

        public override string Title { get { return "Attention"; } }
        public override int Order { get { return 30; } }

        public override void DrawScene(PlaySenseReplayContext context)
        {
            if (context.Combined)
                DrawCombined(context);
            else
                DrawSingle(context);
        }

        private static void DrawSingle(PlaySenseReplayContext context)
        {
            PlaySenseReplaySession session = context.Primary;
            if (session == null)
                return;

            foreach (BehaviourObservation observation in session.Data.BehaviourObservations)
            {
                if (observation == null || observation.Timestamp > context.Time || !context.InScene(session.Data, observation.Scene))
                    continue;

                bool active = context.Time <= observation.EndTime;
                float radius = 0.25f + Mathf.Min(observation.Duration, 10f) * 0.08f;

                Handles.color = active ? AttentionColor : new Color(AttentionColor.r, AttentionColor.g, AttentionColor.b, 0.35f);
                Handles.DrawWireDisc(observation.Position, Vector3.up, radius, active ? 3f : 1.5f);

                if (!active)
                    continue;

                Handles.Label(observation.Position + Vector3.up * (radius + 0.2f),
                    observation.Subject + "  " + (context.Time - observation.Timestamp).ToString("F1") + "s",
                    EditorStyles.whiteBoldLabel);

                string agent = string.IsNullOrEmpty(observation.AgentId) && session.Agents.Count > 0 ? session.Agents[0] : observation.AgentId;
                List<FrameData> frames;
                Vector3 position;
                Quaternion rotation;
                Vector3 view;

                if (session.Frames.TryGetValue(agent, out frames) &&
                    SessionQueries.SampleAt(frames, context.Time, out position, out rotation, out view))
                {
                    Handles.DrawDottedLine(position, observation.Position, 4f);
                }
            }
        }

        private static void DrawCombined(PlaySenseReplayContext context)
        {
            Dictionary<string, KeyValuePair<Vector3, string>> where = new Dictionary<string, KeyValuePair<Vector3, string>>();
            Dictionary<string, int> sessionsPerTarget = new Dictionary<string, int>();
            HashSet<string> seen = new HashSet<string>();

            foreach (PlaySenseReplaySession session in context.Sessions)
            {
                seen.Clear();
                foreach (BehaviourObservation observation in session.Data.BehaviourObservations)
                {
                    if (observation == null || !context.InScene(session.Data, observation.Scene))
                        continue;

                    string key = observation.Key ?? string.Empty;
                    where[key] = new KeyValuePair<Vector3, string>(observation.Position, observation.Subject);

                    if (seen.Add(key))
                    {
                        int count;
                        sessionsPerTarget.TryGetValue(key, out count);
                        sessionsPerTarget[key] = count + 1;
                    }
                }
            }

            float total = Mathf.Max(1, context.Sessions.Count);
            foreach (KeyValuePair<string, int> pair in sessionsPerTarget)
            {
                Vector3 position = where[pair.Key].Key;
                float share = pair.Value / total;
                float radius = 0.3f + share * 1.2f;

                Handles.color = new Color(AttentionColor.r, AttentionColor.g, AttentionColor.b, 0.4f + share * 0.6f);
                Handles.DrawWireDisc(position, Vector3.up, radius, 2f + share * 3f);
                Handles.Label(position + Vector3.up * (radius + 0.2f),
                    where[pair.Key].Value + "  " + pair.Value + "/" + context.Sessions.Count, EditorStyles.whiteBoldLabel);
            }
        }
    }

    // =====================================================================
    // Setup steps + menu
    // =====================================================================

    public class PerceptionSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Track what the player looks at"; } }
        public override string Description { get { return "Adds PlaySensePerception (the Attention module) to your player."; } }
        public override int Order { get { return 30; } }

        public override bool IsDone()
        {
            return Object.FindFirstObjectByType<PlaySensePerception>() != null;
        }

        public override string Run()
        {
            GameObject player = PlaySenseSetup.FindPlayer();
            if (player == null)
                return "Mark your player first (select it in the Hierarchy).";

            PlaySenseSetup.AddIfMissing<Core.PlaySenseAgent>(player);
            PlaySenseSetup.AddIfMissing<PlaySensePerception>(player);
            return null;
        }
    }

    public class ViewSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Add a view to your camera"; } }
        public override string Description { get { return "Adds PlaySenseView to the gameplay camera (the player's child camera, else Main Camera) so PlaySense knows what's on screen."; } }
        public override int Order { get { return 40; } }

        public override bool IsDone()
        {
            return Object.FindFirstObjectByType<PlaySenseView>() != null;
        }

        public override string Run()
        {
            GameObject player = PlaySenseSetup.FindPlayer();
            Camera camera = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (camera == null)
                camera = Camera.main;

            if (camera == null)
                return "No camera found. Add PlaySenseView to your gameplay camera by hand.";

            PlaySenseSetup.AddIfMissing<PlaySenseView>(camera.gameObject);
            return null;
        }
    }

    public class TargetsSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Mark objects to track"; } }
        public override string Description { get { return "Select the objects you want attention data on (doors, clues, enemies, signs), then click. Each needs a collider."; } }
        public override int Order { get { return 50; } }
        public override string ButtonLabel { get { return "Use selection"; } }

        public override bool IsDone()
        {
            return Object.FindFirstObjectByType<PlaySenseTarget>() != null;
        }

        public override string Run()
        {
            int added = AttentionMenu.MarkSelection();
            return added == 0 ? "Select one or more objects in the Hierarchy first." : "Marked " + added + " object(s).";
        }
    }

    public static class AttentionMenu
    {
        [MenuItem("GameObject/PlaySense/Mark as Attention Target", false, 20)]
        private static void MarkAsTarget()
        {
            MarkSelection();
        }

        [MenuItem("GameObject/PlaySense/Mark as Attention Target", true)]
        private static bool CanMarkAsTarget()
        {
            return Selection.gameObjects.Length > 0;
        }

        /// <summary>Adds PlaySenseTarget to every selected scene object. Returns how many were added.</summary>
        public static int MarkSelection()
        {
            int added = 0;
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(gameObject) || gameObject.GetComponent<PlaySenseTarget>() != null)
                    continue;

                Undo.AddComponent<PlaySenseTarget>(gameObject);
                added++;

                if (gameObject.GetComponentInChildren<Collider>() == null)
                    Debug.LogWarning("[PlaySense] '" + gameObject.name + "' has no collider. Targets need one to be detected.", gameObject);
            }

            return added;
        }
    }
}
