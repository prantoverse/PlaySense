using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlaySense.Analysis;
using PlaySense.Data.Models;
using PlaySense.Settings;

namespace PlaySense.Core
{
    /// <summary>
    /// The session being recorded. Modules receive it in their callbacks and
    /// use it to read the session clock and write data.
    ///
    /// Every timestamp (frames, events, attention, module data) comes from
    /// this one clock, so all timelines line up.
    /// </summary>
    public class PlaySenseSession
    {
        private readonly SessionData _data;
        private readonly PlaySenseSettings _settings;
        private double _elapsedTime;
        private bool _isRecording;
        private int _sceneIndex;

        internal PlaySenseSession(PlaySenseSettings settings)
        {
            _settings = settings;

            string scene = SceneManager.GetActiveScene().name;
            _data = new SessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                SceneName = scene,
                StartedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString()
            };

            _data.Scenes.Add(new SceneVisit { Name = scene, StartTime = 0f });
            _isRecording = true;
        }

        /// <summary>The raw session data. Prefer the Record methods for writing.</summary>
        public SessionData Data
        {
            get { return _data; }
        }

        public PlaySenseSettings Settings
        {
            get { return _settings; }
        }

        public bool IsRecording
        {
            get { return _isRecording; }
        }

        /// <summary>Seconds since recording started, on the PlaySense clock.</summary>
        public float Time
        {
            get { return (float)_elapsedTime; }
        }

        /// <summary>Index into Data.Scenes of the scene currently active.</summary>
        public int SceneIndex
        {
            get { return _sceneIndex; }
        }

        public string SceneName
        {
            get { return _data.GetSceneName(_sceneIndex); }
        }

        // =====================================================
        // CLOCK / SCENES (driven by PlaySenseManager)
        // =====================================================

        internal void Tick(float deltaTime)
        {
            if (_isRecording && deltaTime > 0f)
                _elapsedTime += deltaTime;
        }

        internal void EnterScene(string sceneName)
        {
            _data.Scenes.Add(new SceneVisit { Name = sceneName, StartTime = Time });
            _sceneIndex = _data.Scenes.Count - 1;
        }

        // =====================================================
        // WRITING DATA
        // =====================================================

        public void RecordFrame(string agentId, Vector3 position, Quaternion rotation, Vector3 viewDirection)
        {
            if (!_isRecording)
                return;

            _data.Frames.Add(new FrameData
            {
                ObjectName = agentId,
                Timestamp = Time,
                Position = position,
                Rotation = rotation,
                ViewDirection = viewDirection,
                Scene = _sceneIndex
            });
        }

        public GameEventData RecordEvent(
            string category,
            string action,
            string target,
            Vector3? position,
            IList<EventParameter> metadata)
        {
            if (!_isRecording)
                return null;

            GameEventData gameEvent = new GameEventData
            {
                Category = category ?? string.Empty,
                Action = action ?? string.Empty,
                Target = target ?? string.Empty,
                Timestamp = Time,
                Position = position.HasValue ? position.Value : Vector3.zero,
                HasPosition = position.HasValue,
                Scene = _sceneIndex
            };

            if (metadata != null)
            {
                for (int i = 0; i < metadata.Count; i++)
                {
                    if (metadata[i] != null)
                        gameEvent.Metadata.Add(metadata[i]);
                }
            }

            _data.Events.Add(gameEvent);

            if (_settings.verboseLogging)
                Debug.Log("[PlaySense] " + gameEvent.Timestamp.ToString("F2") + "s  " + gameEvent);

            return gameEvent;
        }

        public void RecordObservation(BehaviourObservation observation)
        {
            if (!_isRecording || observation == null)
                return;

            observation.Scene = _sceneIndex;
            _data.BehaviourObservations.Add(observation);
        }

        /// <summary>Stores a module's own data (any [Serializable] class) in the session.</summary>
        public void SetModuleData(string moduleId, object data)
        {
            _data.SetModuleData(moduleId, data);
        }

        // =====================================================
        // STOP
        // =====================================================

        /// <summary>Stops the clock, computes metrics and returns the finished data.</summary>
        internal SessionData Stop()
        {
            if (!_isRecording)
                return _data;

            _isRecording = false;
            _data.Duration = Mathf.Max(0f, Time);

            // Observations arrive when attention ends, which is not
            // chronological across agents. Sort for readable reports.
            _data.BehaviourObservations.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            _data.Metrics = new SessionAnalyzer().Analyze(_data);
            return _data;
        }
    }
}
