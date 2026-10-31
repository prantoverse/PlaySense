using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaySense.Data.Models
{
    /// <summary>Everything recorded in one play session. Saved as JSON in a .pss file.</summary>
    [Serializable]
    public class SessionData
    {
        /// <summary>File format version. 1 = original, 2 = single-scene package format, 3 = scenes + modules.</summary>
        public const int CurrentVersion = 3;

        public int Version = CurrentVersion;

        public string SessionId = string.Empty;

        /// <summary>Scene the session started in.</summary>
        public string SceneName = string.Empty;

        /// <summary>Local wall-clock start time, ISO 8601.</summary>
        public string StartedAt = string.Empty;

        public string UnityVersion = string.Empty;

        public string Platform = string.Empty;

        /// <summary>Length of the session on the PlaySense session clock (seconds).</summary>
        public float Duration;

        /// <summary>
        /// Every scene the session passed through, in order. Frames, events and
        /// observations store an index into this list. Empty in v1/v2 files
        /// (everything happened in SceneName).
        /// </summary>
        public List<SceneVisit> Scenes = new List<SceneVisit>();

        /// <summary>Ids of the modules that recorded this session.</summary>
        public List<string> Modules = new List<string>();

        public List<FrameData> Frames = new List<FrameData>();

        public List<GameEventData> Events = new List<GameEventData>();

        public List<BehaviourObservation> BehaviourObservations = new List<BehaviourObservation>();

        /// <summary>Data written by modules (built-in or your own), one JSON blob per module.</summary>
        public List<ModuleDataEntry> ModuleData = new List<ModuleDataEntry>();

        public SessionMetrics Metrics = new SessionMetrics();

        /// <summary>Makes a loaded session safe to read (no null lists or metrics).</summary>
        public void EnsureValid()
        {
            if (Frames == null) Frames = new List<FrameData>();
            if (Events == null) Events = new List<GameEventData>();
            if (BehaviourObservations == null) BehaviourObservations = new List<BehaviourObservation>();
            if (ModuleData == null) ModuleData = new List<ModuleDataEntry>();
            if (Modules == null) Modules = new List<string>();
            if (Scenes == null) Scenes = new List<SceneVisit>();
            if (Metrics == null) Metrics = new SessionMetrics();
            if (SessionId == null) SessionId = string.Empty;
            if (SceneName == null) SceneName = string.Empty;

            // Older files: everything happened in the start scene.
            if (Scenes.Count == 0)
                Scenes.Add(new SceneVisit { Name = SceneName, StartTime = 0f });
        }

        /// <summary>Name of the scene at a scene index (see Scenes).</summary>
        public string GetSceneName(int sceneIndex)
        {
            if (Scenes != null && sceneIndex >= 0 && sceneIndex < Scenes.Count)
                return Scenes[sceneIndex].Name;

            return SceneName;
        }

        /// <summary>Distinct scene names visited, in first-visit order.</summary>
        public List<string> GetSceneNames()
        {
            List<string> names = new List<string>();
            if (Scenes == null || Scenes.Count == 0)
            {
                names.Add(SceneName);
                return names;
            }

            for (int i = 0; i < Scenes.Count; i++)
            {
                if (!names.Contains(Scenes[i].Name))
                    names.Add(Scenes[i].Name);
            }

            return names;
        }

        // =====================================================
        // MODULE DATA
        // =====================================================

        public bool HasModuleData(string moduleId)
        {
            return FindModuleData(moduleId) != null;
        }

        /// <summary>Reads a module's data, or returns null if the module didn't record any.</summary>
        public T GetModuleData<T>(string moduleId) where T : class
        {
            ModuleDataEntry entry = FindModuleData(moduleId);
            if (entry == null || string.IsNullOrEmpty(entry.Json))
                return null;

            try
            {
                return JsonUtility.FromJson<T>(entry.Json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[PlaySense] Could not read data of module '" + moduleId + "': " + exception.Message);
                return null;
            }
        }

        /// <summary>Stores a module's data (a [Serializable] class) in the session, replacing earlier data.</summary>
        public void SetModuleData(string moduleId, object data)
        {
            if (string.IsNullOrEmpty(moduleId))
                throw new ArgumentException("moduleId is required", "moduleId");

            if (ModuleData == null)
                ModuleData = new List<ModuleDataEntry>();

            ModuleDataEntry entry = FindModuleData(moduleId);
            if (entry == null)
            {
                entry = new ModuleDataEntry { ModuleId = moduleId };
                ModuleData.Add(entry);
            }

            entry.Json = data != null ? JsonUtility.ToJson(data) : string.Empty;
        }

        private ModuleDataEntry FindModuleData(string moduleId)
        {
            if (ModuleData == null)
                return null;

            for (int i = 0; i < ModuleData.Count; i++)
            {
                if (ModuleData[i] != null && ModuleData[i].ModuleId == moduleId)
                    return ModuleData[i];
            }

            return null;
        }
    }

    [Serializable]
    public class SceneVisit
    {
        public string Name = string.Empty;

        /// <summary>Session time when this scene became active (seconds).</summary>
        public float StartTime;
    }

    [Serializable]
    public class ModuleDataEntry
    {
        public string ModuleId = string.Empty;

        public string Json = string.Empty;
    }
}
