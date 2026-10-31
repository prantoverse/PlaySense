using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaySense.Core
{
    /// <summary>Where a module's component belongs. Used by the setup tools.</summary>
    public enum PlaySenseModulePlacement
    {
        /// <summary>On the PlaySenseManager object (e.g. Movement, Performance).</summary>
        Manager,

        /// <summary>On the player/agent object (e.g. Attention).</summary>
        Player
    }

    /// <summary>Describes a module in the editor's module list. Optional.</summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PlaySenseModuleInfoAttribute : Attribute
    {
        public readonly string DisplayName;
        public readonly string Description;
        public readonly PlaySenseModulePlacement Placement;

        public PlaySenseModuleInfoAttribute(string displayName, string description, PlaySenseModulePlacement placement = PlaySenseModulePlacement.Manager)
        {
            DisplayName = displayName;
            Description = description;
            Placement = placement;
        }
    }

    /// <summary>
    /// Base class for anything that records into a PlaySense session.
    ///
    /// Add the component anywhere (usually next to the PlaySenseManager or on
    /// the player). While it is enabled and a session is recording, the
    /// manager calls:
    /// <list type="bullet">
    /// <item>OnSessionStart once (also when the module is enabled mid-session)</item>
    /// <item>OnSessionUpdate every frame, after the session clock has advanced</item>
    /// <item>OnSceneChanged when the active scene changes (sessions can span scenes)</item>
    /// <item>OnSessionStop once (also when the module is disabled or destroyed mid-session)</item>
    /// </list>
    /// Write your own data with session.SetModuleData(ModuleId, yourSerializableObject)
    /// and read it back from SessionData.GetModuleData&lt;T&gt;(ModuleId).
    /// </summary>
    public abstract class PlaySenseModule : MonoBehaviour
    {
        private static readonly List<PlaySenseModule> s_Active = new List<PlaySenseModule>();

        /// <summary>All enabled modules. Do not modify.</summary>
        public static List<PlaySenseModule> ActiveModules
        {
            get { return s_Active; }
        }

        /// <summary>Key under which this module's data is stored. Defaults to the class name.</summary>
        public virtual string ModuleId
        {
            get { return GetType().Name; }
        }

        /// <summary>Lower runs first. Movement uses 0.</summary>
        public virtual int Order
        {
            get { return 100; }
        }

        /// <summary>If you override OnEnable, call base.OnEnable().</summary>
        protected virtual void OnEnable()
        {
            if (!s_Active.Contains(this))
                s_Active.Add(this);

            PlaySenseManager manager = PlaySenseManager.Instance;
            if (manager != null)
                manager.NotifyModuleEnabled(this);
        }

        /// <summary>If you override OnDisable, call base.OnDisable().</summary>
        protected virtual void OnDisable()
        {
            PlaySenseManager manager = PlaySenseManager.Instance;
            if (manager != null)
                manager.NotifyModuleDisabled(this);

            s_Active.Remove(this);
        }

        public virtual void OnSessionStart(PlaySenseSession session)
        {
        }

        public virtual void OnSessionUpdate(PlaySenseSession session, float deltaTime)
        {
        }

        public virtual void OnSceneChanged(PlaySenseSession session, string sceneName)
        {
        }

        public virtual void OnSessionStop(PlaySenseSession session)
        {
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Active.Clear();
        }
    }
}
