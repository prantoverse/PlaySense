using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PlaySense.Core;
using PlaySense.Settings;

namespace PlaySense.EditorTools
{
    /// <summary>Setup helpers and the "Set Up Scene" menu, which runs every automatic setup step.</summary>
    public static class PlaySenseSetup
    {
        public const string SettingsFolder = "Assets/Resources";
        public const string SettingsPath = SettingsFolder + "/" + PlaySenseSettings.ResourcesName + ".asset";
        public const string OpenedDashboardKey = "PlaySense.OpenedDashboard";

        [MenuItem("Window/PlaySense/Set Up Scene", priority = 20)]
        public static void SetUpScene()
        {
            List<string> messages = new List<string>();
            List<string> waiting = new List<string>();

            foreach (PlaySenseSetupStep step in PlaySenseExtensionRegistry.SetupSteps)
            {
                if (step.IsOptional || !step.IsAutomatic || step.IsDone())
                    continue;

                string message = step.Run();

                if (step.IsDone())
                    messages.Add(step.Title);
                else
                    waiting.Add(step.Title + (string.IsNullOrEmpty(message) ? string.Empty : ": " + message));
            }

            string text;
            if (messages.Count == 0 && waiting.Count == 0)
                text = "This scene is already set up. Press Play to record a session.";
            else
                text = (messages.Count > 0 ? "Done:\n- " + string.Join("\n- ", messages.ToArray()) : string.Empty) +
                       (waiting.Count > 0 ? "\n\nStill to do:\n- " + string.Join("\n- ", waiting.ToArray()) : string.Empty);

            EditorUtility.DisplayDialog("PlaySense", text.Trim(), "OK");
        }

        [MenuItem("Window/PlaySense/Select Settings", priority = 21)]
        public static void SelectSettings()
        {
            EnsureSettingsAsset();
            PlaySenseSettings settings = Resources.Load<PlaySenseSettings>(PlaySenseSettings.ResourcesName);
            if (settings == null)
                return;

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        /// <summary>Creates Assets/Resources/PlaySenseSettings.asset if none exists. Returns true if created.</summary>
        public static bool EnsureSettingsAsset()
        {
            if (Resources.Load<PlaySenseSettings>(PlaySenseSettings.ResourcesName) != null)
                return false;

            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PlaySenseSettings>(), SettingsPath);
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>The scene's PlaySenseManager, or null.</summary>
        public static PlaySenseManager FindManager()
        {
            return Object.FindFirstObjectByType<PlaySenseManager>();
        }

        /// <summary>The player: the first PlaySenseAgent, else the selected scene object.</summary>
        public static GameObject FindPlayer()
        {
            PlaySenseAgent agent = Object.FindFirstObjectByType<PlaySenseAgent>();
            if (agent != null)
                return agent.gameObject;

            GameObject selected = Selection.activeGameObject;
            return selected != null && !EditorUtility.IsPersistent(selected) ? selected : null;
        }

        public static T AddIfMissing<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing != null ? existing : Undo.AddComponent<T>(target);
        }
    }

    // =====================================================================
    // Core setup steps (modules add their own in their Editor assemblies).
    // =====================================================================

    public class SettingsSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Create the settings asset"; } }
        public override string Description { get { return "One asset holds every PlaySense threshold: sampling rate, attention angle, analysis cut-offs."; } }
        public override int Order { get { return 0; } }

        public override bool IsDone()
        {
            return Resources.Load<PlaySenseSettings>(PlaySenseSettings.ResourcesName) != null;
        }

        public override string Run()
        {
            PlaySenseSetup.EnsureSettingsAsset();
            return null;
        }
    }

    public class ManagerSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Add a PlaySense Manager"; } }
        public override string Description { get { return "Runs recording sessions. Put it in your first scene; it follows the player through scene loads."; } }
        public override int Order { get { return 10; } }

        public override bool IsDone()
        {
            return PlaySenseSetup.FindManager() != null;
        }

        public override string Run()
        {
            GameObject managerObject = new GameObject("PlaySenseManager");
            Undo.RegisterCreatedObjectUndo(managerObject, "Add PlaySense Manager");
            managerObject.AddComponent<PlaySenseManager>();
            PlaySenseSetup.AddIfMissing<PlaySenseMovementModule>(managerObject);
            return null;
        }
    }

    public class AgentSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Mark your player"; } }
        public override string Description { get { return "Select your player in the Hierarchy, then click. Adds PlaySenseAgent so its path is recorded."; } }
        public override int Order { get { return 20; } }
        public override string ButtonLabel { get { return "Use selection"; } }

        public override bool IsDone()
        {
            return Object.FindFirstObjectByType<PlaySenseAgent>() != null;
        }

        public override string Run()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected == null || EditorUtility.IsPersistent(selected))
                return "Select your player in the Hierarchy first.";

            PlaySenseSetup.AddIfMissing<PlaySenseAgent>(selected);
            return null;
        }
    }

    public class EventSetupStep : PlaySenseSetupStep
    {
        public const string Snippet =
            "using PlaySense;\n\n" +
            "PlaySenseEvents.Log(\"Puzzle\", \"Solved\", \"Lever Door\");\n" +
            "PlaySenseEvents.Log(\"Combat\", \"Kill\", enemy, \"method\", \"gun\");";

        public override string Title { get { return "Log a gameplay event"; } }
        public override string Description { get { return "Add a PlaySenseEvents.Log call where something interesting happens (kills, pickups, puzzles). Click to copy an example."; } }
        public override int Order { get { return 60; } }
        public override bool IsAutomatic { get { return false; } }
        public override string ButtonLabel { get { return "Copy example"; } }

        public override bool IsDone()
        {
            foreach (PlaySenseSessionCache.Entry entry in PlaySenseSessionCache.Entries)
            {
                foreach (Data.Models.GameEventData gameEvent in entry.Session.Events)
                {
                    if (gameEvent != null && gameEvent.Category != "Scene")
                        return true;
                }
            }

            return false;
        }

        public override string Run()
        {
            EditorGUIUtility.systemCopyBuffer = Snippet;
            return "Example copied to the clipboard.";
        }
    }

    public class RecordSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Record a session"; } }
        public override string Description { get { return "Press Play, play for a minute, then exit Play mode. The session is saved automatically."; } }
        public override int Order { get { return 80; } }
        public override bool IsAutomatic { get { return false; } }
        public override string ButtonLabel { get { return "Play"; } }

        public override bool IsDone()
        {
            return PlaySenseSessionCache.Entries.Count > 0;
        }

        public override string Run()
        {
            if (PlaySenseSetup.FindManager() == null)
                return "Add a PlaySense Manager first.";

            EditorApplication.isPlaying = true;
            return null;
        }
    }

    public class DashboardSetupStep : PlaySenseSetupStep
    {
        public override string Title { get { return "Look at the results"; } }
        public override string Description { get { return "The Dashboard shows each session and totals across all of them. Replay draws the path and heatmap in the Scene view."; } }
        public override int Order { get { return 90; } }
        public override bool IsAutomatic { get { return false; } }
        public override string ButtonLabel { get { return "Open Dashboard"; } }

        public override bool IsDone()
        {
            return EditorPrefs.GetBool(PlaySenseSetup.OpenedDashboardKey, false);
        }

        public override string Run()
        {
            EditorPrefs.SetBool(PlaySenseSetup.OpenedDashboardKey, true);
            PlaySenseDashboard.ShowWindow();
            return null;
        }
    }
}
