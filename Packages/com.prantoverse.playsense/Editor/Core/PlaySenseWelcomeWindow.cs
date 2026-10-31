using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PlaySense.Core;

namespace PlaySense.EditorTools
{
    /// <summary>
    /// First-run guide: a live setup checklist (built from every module's
    /// PlaySenseSetupStep), the event API, the installed modules and tools.
    /// Opens automatically the first time PlaySense is loaded in a project.
    /// </summary>
    public class PlaySenseWelcomeWindow : EditorWindow
    {
        private const string DocsUrl = "https://github.com/prantoverse/playsense#readme";

        private static string SeenKey
        {
            get { return "PlaySense.Welcome.Seen." + Application.dataPath.GetHashCode(); }
        }

        private static string ShowAtStartupKey
        {
            get { return "PlaySense.Welcome.ShowAtStartup." + Application.dataPath.GetHashCode(); }
        }

        private Vector2 _scroll;
        private string _stepMessage;
        private string _stepMessageTitle;

        [MenuItem("Window/PlaySense/Welcome && Setup Guide", priority = -10)]
        public static void ShowWindow()
        {
            PlaySenseWelcomeWindow window = GetWindow<PlaySenseWelcomeWindow>(true, "Welcome to PlaySense", true);
            window.minSize = new Vector2(520f, 600f);
            window.Show();
        }

        /// <summary>Opens the window once per project (or every launch if the user ticked "Show at startup").</summary>
        [InitializeOnLoadMethod]
        private static void ShowOnFirstLoad()
        {
            if (Application.isBatchMode)
                return;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                // Only once per editor launch, not after every script reload.
                if (SessionState.GetBool("PlaySense.Welcome.ShownThisLaunch", false))
                    return;

                bool firstTime = !EditorPrefs.GetBool(SeenKey, false);
                bool showAtStartup = EditorPrefs.GetBool(ShowAtStartupKey, false);

                if (!firstTime && !showAtStartup)
                    return;

                SessionState.SetBool("PlaySense.Welcome.ShownThisLaunch", true);
                EditorPrefs.SetBool(SeenKey, true);
                ShowWindow();
            };
        }

        private void OnEnable()
        {
            PlaySenseSessionCache.Changed += Repaint;
            EditorApplication.hierarchyChanged += Repaint;
        }

        private void OnDisable()
        {
            PlaySenseSessionCache.Changed -= Repaint;
            EditorApplication.hierarchyChanged -= Repaint;
        }

        private void OnFocus()
        {
            Repaint();
        }

        // =====================================================
        // GUI
        // =====================================================

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Space(10f);

            GUILayout.Label("PlaySense", PlaySenseGUI.TitleStyle);
            GUILayout.Label(
                "Records where players go, what they look at and what they do, then shows it in a Dashboard, " +
                "a Scene-view Replay with heatmaps, and PDF reports. Everything stays on your machine as .pss files.",
                PlaySenseGUI.WrapStyle);

            DrawChecklist();
            DrawEvents();
            DrawModules();
            DrawTools();

            GUILayout.Space(16f);
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawChecklist()
        {
            List<PlaySenseSetupStep> steps = PlaySenseExtensionRegistry.SetupSteps;
            int required = 0;
            int done = 0;

            foreach (PlaySenseSetupStep step in steps)
            {
                if (step.IsOptional)
                    continue;

                required++;
                if (step.IsDone())
                    done++;
            }

            PlaySenseGUI.Section("Get started");

            Rect progress = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.ProgressBar(progress, required > 0 ? done / (float)required : 1f, done + " of " + required + " steps done");

            GUILayout.Label("Steps that change the scene apply to the open scene: " + EditorSceneManager.GetActiveScene().name +
                            ". Put the manager in your first scene.", EditorStyles.miniLabel);

            GUILayout.Space(4f);

            int number = 1;
            foreach (PlaySenseSetupStep step in steps)
            {
                bool isDone = step.IsDone();

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                Color previous = GUI.contentColor;
                GUI.contentColor = isDone ? PlaySenseGUI.Good : Color.white;
                GUILayout.Label(isDone ? "✓" : number.ToString(), EditorStyles.boldLabel, GUILayout.Width(18f));
                GUI.contentColor = previous;

                EditorGUILayout.BeginVertical();
                GUILayout.Label(step.Title + (step.IsOptional ? "  (optional)" : string.Empty), EditorStyles.boldLabel);
                GUILayout.Label(step.Description, EditorStyles.wordWrappedMiniLabel);

                if (_stepMessageTitle == step.Title && !string.IsNullOrEmpty(_stepMessage))
                    EditorGUILayout.HelpBox(_stepMessage, MessageType.None);

                EditorGUILayout.EndVertical();

                using (new EditorGUI.DisabledScope(isDone && step.IsAutomatic))
                {
                    if (GUILayout.Button(isDone && !step.IsAutomatic ? "Again" : step.ButtonLabel, GUILayout.Width(110f), GUILayout.Height(22f)))
                    {
                        PlaySenseSetupStep run = step;
                        EditorApplication.delayCall += () =>
                        {
                            _stepMessageTitle = run.Title;
                            _stepMessage = run.Run();
                            Repaint();
                        };
                    }
                }

                EditorGUILayout.EndHorizontal();
                number++;
            }

            GUILayout.Space(4f);
            if (GUILayout.Button("Run all automatic steps (Set Up Scene)", GUILayout.Height(24f)))
                EditorApplication.delayCall += PlaySenseSetup.SetUpScene;
        }

        private void DrawEvents()
        {
            PlaySenseGUI.Section("Track what players do");
            GUILayout.Label(
                "Call PlaySenseEvents.Log anywhere in your game code. It does nothing when PlaySense isn't recording, so it can stay in shipping builds. " +
                "Key/value pairs become breakdowns in the All Sessions view, e.g. \"70% of kills used a gun\".",
                PlaySenseGUI.WrapStyle);
            GUILayout.Space(4f);
            PlaySenseGUI.CodeBlock(
                "using PlaySense;\n\n" +
                "PlaySenseEvents.Log(\"Puzzle\", \"Solved\", \"Lever Door\");\n" +
                "PlaySenseEvents.Log(\"Combat\", \"AbilityUsed\", \"Shockwave\", transform.position);\n" +
                "PlaySenseEvents.Log(\"Combat\", \"Kill\", enemy, \"method\", \"gun\", \"weapon\", \"AR\");");
        }

        private void DrawModules()
        {
            PlaySenseGUI.Section("Modules");
            GUILayout.Label(
                "Each module records one kind of data. Remove a module's folder from the package and its components, tabs and Replay layers go with it. " +
                "Write your own by subclassing PlaySenseModule.",
                PlaySenseGUI.WrapStyle);
            GUILayout.Space(4f);

            PlaySenseManager manager = PlaySenseSetup.FindManager();
            GameObject player = PlaySenseSetup.FindPlayer();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<PlaySenseModule>())
            {
                if (type.IsAbstract)
                    continue;

                PlaySenseModuleInfoAttribute info = (PlaySenseModuleInfoAttribute)Attribute.GetCustomAttribute(type, typeof(PlaySenseModuleInfoAttribute));
                string title = info != null ? info.DisplayName : ObjectNames.NicifyVariableName(type.Name);
                string description = info != null ? info.Description : type.FullName;
                PlaySenseModulePlacement placement = info != null ? info.Placement : PlaySenseModulePlacement.Manager;

                GameObject host = placement == PlaySenseModulePlacement.Player ? player : (manager != null ? manager.gameObject : null);
                bool inScene = UnityEngine.Object.FindFirstObjectByType(type) != null;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.BeginVertical();
                GUILayout.Label(title + (inScene ? "   ✓ in scene" : string.Empty), EditorStyles.boldLabel);
                GUILayout.Label(description + "  (goes on the " + (placement == PlaySenseModulePlacement.Player ? "player" : "manager") + ")", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();

                using (new EditorGUI.DisabledScope(inScene || host == null))
                {
                    if (GUILayout.Button(host == null && !inScene ? (placement == PlaySenseModulePlacement.Player ? "Needs player" : "Needs manager") : "Add", GUILayout.Width(110f), GUILayout.Height(22f)))
                    {
                        Type moduleType = type;
                        GameObject target = host;
                        EditorApplication.delayCall += () =>
                        {
                            Undo.AddComponent(target, moduleType);
                            Repaint();
                        };
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTools()
        {
            PlaySenseGUI.Section("Tools");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dashboard", GUILayout.Height(26f)))
            {
                EditorPrefs.SetBool(PlaySenseSetup.OpenedDashboardKey, true);
                PlaySenseDashboard.ShowWindow();
            }

            if (GUILayout.Button("Settings", GUILayout.Height(26f)))
                PlaySenseSetup.SelectSettings();

            string demo = FindDemoScene();
            using (new EditorGUI.DisabledScope(demo == null))
            {
                if (GUILayout.Button("Open Demo Scene", GUILayout.Height(26f)))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(demo);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Replay and other tools are under Window > PlaySense.", EditorStyles.miniLabel);
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            bool showAtStartup = EditorPrefs.GetBool(ShowAtStartupKey, false);
            bool now = GUILayout.Toggle(showAtStartup, "Show at startup", EditorStyles.toolbarButton);
            if (now != showAtStartup)
                EditorPrefs.SetBool(ShowAtStartupKey, now);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Documentation", EditorStyles.toolbarButton))
                Application.OpenURL(DocsUrl);

            EditorGUILayout.EndHorizontal();
        }

        private static string FindDemoScene()
        {
            foreach (string guid in AssetDatabase.FindAssets("Demo t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Replace('\\', '/').Contains("/Demo/"))
                    return path;
            }

            return null;
        }
    }
}
