using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PlaySense.Data.Models;
using PlaySense.Performance;

namespace PlaySense.EditorTools
{
    /// <summary>Frame-rate tab for sessions recorded with the Performance module.</summary>
    public class PerformanceDashboardTab : PlaySenseDashboardTab
    {
        public override string Title { get { return "Performance"; } }
        public override int Order { get { return 30; } }
        public override bool SupportsOverview { get { return true; } }

        public override bool IsAvailable(SessionData session)
        {
            return session.HasModuleData(PlaySensePerformanceModule.Id);
        }

        public override void DrawSession(SessionData session, string path)
        {
            PerformanceData data = session.GetModuleData<PerformanceData>(PlaySensePerformanceModule.Id);
            if (data == null)
                return;

            EditorGUILayout.BeginHorizontal();
            PlaySenseGUI.Metric("Average FPS", data.AverageFps.ToString("F0"));
            PlaySenseGUI.Metric("1% low", data.OnePercentLowFps.ToString("F0"));
            PlaySenseGUI.Metric("Lowest sample", data.MinFps.ToString("F0"));
            PlaySenseGUI.Metric("Hitches", data.HitchCount + " (>" + data.HitchThresholdMs.ToString("F0") + " ms)");
            EditorGUILayout.EndHorizontal();

            PlaySenseGUI.Section("Frame rate over time");
            PlaySenseGUI.LineChart(data.Times, data.Fps, 160f, "FPS", PlaySenseGUI.Good);

            if (session.Scenes.Count > 1 && data.Scenes.Count == data.Fps.Count)
            {
                PlaySenseGUI.Section("By scene");
                Dictionary<string, List<float>> byScene = new Dictionary<string, List<float>>();
                for (int i = 0; i < data.Fps.Count; i++)
                {
                    string scene = session.GetSceneName(data.Scenes[i]);
                    List<float> values;
                    if (!byScene.TryGetValue(scene, out values))
                        byScene[scene] = values = new List<float>();
                    values.Add(data.Fps[i]);
                }

                foreach (KeyValuePair<string, List<float>> pair in byScene)
                    GUILayout.Label(pair.Key + ":  " + Average(pair.Value).ToString("F0") + " FPS average", EditorStyles.miniLabel);
            }
        }

        public override void DrawOverview(IList<SessionData> sessions, string sceneFilter)
        {
            List<float> averages = new List<float>();
            List<float> lows = new List<float>();

            foreach (SessionData session in sessions)
            {
                PerformanceData data = session.GetModuleData<PerformanceData>(PlaySensePerformanceModule.Id);
                if (data == null || data.Fps.Count == 0)
                    continue;

                averages.Add(data.AverageFps);
                lows.Add(data.OnePercentLowFps);
            }

            if (averages.Count == 0)
            {
                EditorGUILayout.HelpBox("None of these sessions used the Performance module. Add it to the PlaySenseManager object to record frame rate.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            PlaySenseGUI.Metric("Sessions with data", averages.Count + " / " + sessions.Count);
            PlaySenseGUI.Metric("Average FPS", Average(averages).ToString("F0"));
            PlaySenseGUI.Metric("Average 1% low", Average(lows).ToString("F0"));
            EditorGUILayout.EndHorizontal();

            averages.Sort();
            PlaySenseGUI.Section("Spread");
            GUILayout.Label(
                "Worst session " + averages[0].ToString("F0") + " FPS   Median " + averages[averages.Count / 2].ToString("F0") +
                " FPS   Best " + averages[averages.Count - 1].ToString("F0") + " FPS",
                EditorStyles.miniLabel);
        }

        private static float Average(List<float> values)
        {
            if (values.Count == 0)
                return 0f;

            float sum = 0f;
            foreach (float value in values)
                sum += value;
            return sum / values.Count;
        }
    }

    public class PerformanceSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Record frame rate"; } }
        public override string Description { get { return "Adds the Performance module to the manager, so you can see FPS dips next to player behaviour."; } }
        public override int Order { get { return 70; } }
        public override bool IsOptional { get { return true; } }

        public override bool IsDone()
        {
            return Object.FindFirstObjectByType<PlaySensePerformanceModule>() != null;
        }

        public override string Run()
        {
            Core.PlaySenseManager manager = PlaySenseSetup.FindManager();
            if (manager == null)
                return "Add a PlaySense Manager first.";

            PlaySenseSetup.AddIfMissing<PlaySensePerformanceModule>(manager.gameObject);
            return null;
        }
    }
}
