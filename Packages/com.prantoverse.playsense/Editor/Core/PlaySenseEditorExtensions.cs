using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PlaySense.Data.Models;
using PlaySense.Settings;

namespace PlaySense.EditorTools
{
    // =====================================================================
    // Extension points. Subclass any of these in an Editor assembly and
    // PlaySense finds it automatically (no registration). This is how the
    // built-in modules add their tabs, Replay layers, buttons and setup
    // steps, so deleting a module's folder removes all of it cleanly.
    // =====================================================================

    /// <summary>A tab in the PlaySense Dashboard.</summary>
    public abstract class PlaySenseDashboardTab
    {
        public abstract string Title { get; }

        /// <summary>Lower appears first. Summary is 0, Events 10.</summary>
        public virtual int Order
        {
            get { return 100; }
        }

        /// <summary>Hide the tab for sessions without this module's data.</summary>
        public virtual bool IsAvailable(SessionData session)
        {
            return true;
        }

        /// <summary>Draws the tab for one session.</summary>
        public abstract void DrawSession(SessionData session, string path);

        /// <summary>True if the tab has an "All sessions" view.</summary>
        public virtual bool SupportsOverview
        {
            get { return false; }
        }

        /// <summary>Draws the tab across many sessions. sceneFilter is null for all scenes.</summary>
        public virtual void DrawOverview(IList<SessionData> sessions, string sceneFilter)
        {
        }
    }

    /// <summary>A button shown for the selected session in the Dashboard (e.g. Replay, PDF).</summary>
    public abstract class PlaySenseSessionAction
    {
        public abstract string Title { get; }

        public virtual int Order
        {
            get { return 100; }
        }

        public abstract void Run(SessionData session, string path);
    }

    /// <summary>Something drawn in the Scene view by the Replay window.</summary>
    public abstract class PlaySenseReplayLayer
    {
        public abstract string Title { get; }

        public virtual int Order
        {
            get { return 100; }
        }

        public virtual bool VisibleByDefault
        {
            get { return true; }
        }

        /// <summary>Called when sessions are (re)loaded or the scene changes.</summary>
        public virtual void OnLoad(PlaySenseReplayContext context)
        {
        }

        /// <summary>Extra options drawn under the layer's toggle in the Replay window.</summary>
        public virtual void DrawOptions(PlaySenseReplayContext context)
        {
        }

        /// <summary>Draws into the Scene view (called during Repaint).</summary>
        public abstract void DrawScene(PlaySenseReplayContext context);
    }

    /// <summary>One item in the Welcome window's setup checklist.</summary>
    public abstract class PlaySenseSetupStep
    {
        public abstract string Title { get; }

        public abstract string Description { get; }

        public virtual int Order
        {
            get { return 100; }
        }

        /// <summary>Optional steps are shown but "Set Up Scene" doesn't run them.</summary>
        public virtual bool IsOptional
        {
            get { return false; }
        }

        public virtual string ButtonLabel
        {
            get { return "Do it"; }
        }

        /// <summary>False for steps the user has to do themselves (play, write code). "Set Up Scene" skips those.</summary>
        public virtual bool IsAutomatic
        {
            get { return true; }
        }

        /// <summary>Checked every time the window repaints, so keep it cheap.</summary>
        public abstract bool IsDone();

        /// <summary>Performs the step. Return a message to show, or null.</summary>
        public abstract string Run();
    }

    /// <summary>A recorded session loaded for replay, with its frames split by agent.</summary>
    public class PlaySenseReplaySession
    {
        public string Path;
        public SessionData Data;
        public bool IsPrimary;

        /// <summary>Agent ids in first-seen order.</summary>
        public readonly List<string> Agents = new List<string>();

        /// <summary>Frames per agent, only those recorded in the scene being replayed.</summary>
        public readonly Dictionary<string, List<FrameData>> Frames = new Dictionary<string, List<FrameData>>();
    }

    /// <summary>What Replay layers draw from.</summary>
    public class PlaySenseReplayContext
    {
        public static readonly Color[] AgentColors =
        {
            new Color(0.2f, 0.75f, 1f),
            new Color(1f, 0.55f, 0.2f),
            new Color(0.6f, 1f, 0.3f),
            new Color(0.95f, 0.35f, 0.85f)
        };

        /// <summary>Sessions shown. One in single mode, many in "all sessions" mode.</summary>
        public readonly List<PlaySenseReplaySession> Sessions = new List<PlaySenseReplaySession>();

        /// <summary>True when showing every session recorded in this scene (no timeline).</summary>
        public bool Combined;

        /// <summary>Current timeline position (single mode).</summary>
        public float Time;

        /// <summary>The scene being replayed; data from other scenes is filtered out.</summary>
        public string SceneName = string.Empty;

        public PlaySenseSettings Settings
        {
            get { return PlaySenseSettings.Active; }
        }

        public PlaySenseReplaySession Primary
        {
            get { return Sessions.Count > 0 ? Sessions[0] : null; }
        }

        /// <summary>True if this record (frame, event, observation scene index) belongs to the scene being replayed.</summary>
        public bool InScene(SessionData session, int sceneIndex)
        {
            return session.GetSceneName(sceneIndex) == SceneName;
        }

        public static Color AgentColor(int index)
        {
            int count = AgentColors.Length;
            return AgentColors[((index % count) + count) % count];
        }
    }

    /// <summary>Finds and creates the extension classes above.</summary>
    public static class PlaySenseExtensionRegistry
    {
        private static readonly Dictionary<Type, object> s_Cache = new Dictionary<Type, object>();

        public static List<PlaySenseDashboardTab> Tabs
        {
            get { return Get<PlaySenseDashboardTab>(t => t.Order); }
        }

        public static List<PlaySenseSessionAction> Actions
        {
            get { return Get<PlaySenseSessionAction>(a => a.Order); }
        }

        public static List<PlaySenseReplayLayer> ReplayLayers
        {
            get { return Get<PlaySenseReplayLayer>(l => l.Order); }
        }

        public static List<PlaySenseSetupStep> SetupSteps
        {
            get { return Get<PlaySenseSetupStep>(s => s.Order); }
        }

        private static List<T> Get<T>(Func<T, int> order) where T : class
        {
            object cached;
            if (s_Cache.TryGetValue(typeof(T), out cached))
                return (List<T>)cached;

            List<T> items = new List<T>();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsGenericTypeDefinition || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                try
                {
                    items.Add((T)Activator.CreateInstance(type));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[PlaySense] Could not create " + type.Name + ": " + exception.Message);
                }
            }

            items.Sort((a, b) => order(a).CompareTo(order(b)));
            s_Cache[typeof(T)] = items;
            return items;
        }
    }
}
