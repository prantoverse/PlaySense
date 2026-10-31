using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PlaySense.Core;
using PlaySense.Perception;
using PlaySense.Settings;

namespace PlaySense.EditorTools
{
    /// <summary>Menu items that wire PlaySense into a scene in one click.</summary>
    public static class PlaySenseSetupMenu
    {
        private const string SettingsFolder = "Assets/Resources";
        private const string SettingsPath = SettingsFolder + "/" + PlaySenseSettings.ResourcesName + ".asset";

        [MenuItem("Window/PlaySense/Set Up Scene (select your player first)", priority = 20)]
        public static void SetUpScene()
        {
            List<string> done = new List<string>();

            if (EnsureSettingsAsset())
                done.Add("Created " + SettingsPath);

            if (Object.FindFirstObjectByType<PlaySenseManager>() == null)
            {
                GameObject managerObject = new GameObject("PlaySenseManager");
                managerObject.AddComponent<PlaySenseManager>();
                Undo.RegisterCreatedObjectUndo(managerObject, "Add PlaySense Manager");
                done.Add("Added a PlaySenseManager");
            }

            GameObject player = Selection.activeGameObject;
            if (player != null && !EditorUtility.IsPersistent(player))
            {
                done.AddRange(SetUpPlayer(player));
            }
            else if (Object.FindFirstObjectByType<PlaySenseAgent>() == null)
            {
                done.Add("No player selected: select your player and run this again to add the agent components");
            }

            EditorUtility.DisplayDialog(
                "PlaySense",
                done.Count == 0
                    ? "This scene is already set up."
                    : "- " + string.Join("\n- ", done.ToArray()) +
                      "\n\nNext: add PlaySenseTarget to the objects you want attention data on " +
                      "(GameObject > PlaySense > Mark as Attention Target), then press Play.",
                "OK");
        }

        [MenuItem("GameObject/PlaySense/Mark as Attention Target", false, 20)]
        public static void MarkAsTarget()
        {
            foreach (GameObject gameObject in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(gameObject) || gameObject.GetComponent<PlaySenseTarget>() != null)
                    continue;

                Undo.AddComponent<PlaySenseTarget>(gameObject);

                if (gameObject.GetComponentInChildren<Collider>() == null)
                    Debug.LogWarning("[PlaySense] '" + gameObject.name + "' has no collider. Targets need one to be detected.", gameObject);
            }
        }

        [MenuItem("GameObject/PlaySense/Mark as Attention Target", true)]
        private static bool CanMarkAsTarget()
        {
            return Selection.gameObjects.Length > 0;
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

        private static IEnumerable<string> SetUpPlayer(GameObject player)
        {
            if (player.GetComponent<PlaySenseAgent>() == null)
            {
                Undo.AddComponent<PlaySenseAgent>(player);
                yield return "Added PlaySenseAgent to " + player.name;
            }

            if (player.GetComponent<PlaySensePerception>() == null)
            {
                Undo.AddComponent<PlaySensePerception>(player);
                yield return "Added PlaySensePerception to " + player.name;
            }

            Camera camera = player.GetComponentInChildren<Camera>();
            if (camera == null)
                camera = Camera.main;

            if (camera == null)
            {
                yield return "No camera found: add PlaySenseView to your gameplay camera";
            }
            else if (camera.GetComponent<PlaySenseView>() == null)
            {
                Undo.AddComponent<PlaySenseView>(camera.gameObject);
                yield return "Added PlaySenseView to camera " + camera.name;
            }
        }

        /// <summary>Creates Assets/Resources/PlaySenseSettings.asset if no settings asset exists. Returns true if created.</summary>
        private static bool EnsureSettingsAsset()
        {
            if (Resources.Load<PlaySenseSettings>(PlaySenseSettings.ResourcesName) != null)
                return false;

            Directory.CreateDirectory(SettingsFolder);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<PlaySenseSettings>(), SettingsPath);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
