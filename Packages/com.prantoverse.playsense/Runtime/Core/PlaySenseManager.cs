using System;
using System.Collections.Generic;
using UnityEngine;
using PlaySense.Data.Models;
using PlaySense.Perception;
using PlaySense.Recording;
using PlaySense.Settings;
using PlaySense.Storage;

namespace PlaySense.Core
{
    /// <summary>
    /// Runs a PlaySense recording session. Add one to the scene.
    ///
    /// It drives the single session clock, samples every enabled
    /// PlaySenseAgent, receives attention observations from every
    /// PlaySensePerception and saves the session when it stops
    /// (scene unload, application quit, max duration, or StopRecording()).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("PlaySense/PlaySense Manager")]
    public class PlaySenseManager : MonoBehaviour
    {
        [Tooltip("Optional. If empty, the PlaySenseSettings asset in a Resources folder is used, else defaults.")]
        [SerializeField]
        private PlaySenseSettings settings;

        [Tooltip("Start recording automatically when the scene starts.")]
        [SerializeField]
        private bool recordOnStart = true;

        private readonly SessionRecorder _recorder = new SessionRecorder();
        private float _sampleTimer;
        private bool _warnedNoAgents;

        /// <summary>The manager currently running, if any.</summary>
        public static PlaySenseManager Instance { get; private set; }

        /// <summary>Raised after a session is saved, with the session and its file path.</summary>
        public static event Action<SessionData, string> SessionSaved;

        public PlaySenseSettings Settings
        {
            get { return settings != null ? settings : PlaySenseSettings.Active; }
        }

        public bool IsRecording
        {
            get { return _recorder.IsRecording; }
        }

        /// <summary>Seconds since recording started on the session clock. 0 when not recording.</summary>
        public float SessionTime
        {
            get { return _recorder.IsRecording ? _recorder.ElapsedTime : 0f; }
        }

        /// <summary>The session being recorded (or the last one stopped).</summary>
        public SessionData CurrentSession
        {
            get { return _recorder.CurrentSession; }
        }

        /// <summary>Path of the last saved session file.</summary>
        public string LastSavedPath { get; private set; }

        // =====================================================
        // LIFECYCLE
        // =====================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlaySense] More than one PlaySenseManager in the scene. Disabling '" + name + "'.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (settings != null)
                PlaySenseSettings.SetActive(settings);
        }

        private void Start()
        {
            if (!recordOnStart)
                return;

            if (!Application.isEditor && !Settings.recordInBuilds)
                return;

            StartRecording();
        }

        private void Update()
        {
            if (!_recorder.IsRecording)
                return;

            PlaySenseSettings s = Settings;

            float step = Mathf.Min(Time.unscaledDeltaTime, s.maxClockStep);
            _recorder.Tick(step);

            // Keep the remainder so the real sample rate matches the setting.
            float interval = 1f / Mathf.Max(1, s.samplingRate);
            _sampleTimer += step;
            if (_sampleTimer >= interval)
            {
                _sampleTimer -= interval;

                // After a long hitch don't fire a burst of catch-up samples.
                if (_sampleTimer > interval)
                    _sampleTimer = 0f;

                SampleAgents();
            }

            if (s.maxRecordingMinutes > 0 && _recorder.ElapsedTime >= s.maxRecordingMinutes * 60f)
            {
                Debug.Log("[PlaySense] Max recording time reached (" + s.maxRecordingMinutes + " min). Stopping.");
                StopRecording();
            }
        }

        // Unity calls OnApplicationQuit on every object before any
        // OnDisable/OnDestroy, so perceptions are still alive here.
        private void OnApplicationQuit()
        {
            StopRecording();
        }

        // Scene unload or the manager being disabled. Perceptions that
        // were disabled first have already flushed their attention into
        // the session; the rest are finalized inside StopRecording.
        private void OnDisable()
        {
            StopRecording();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // =====================================================
        // PUBLIC CONTROL
        // =====================================================

        /// <summary>Starts a new session. Does nothing if one is already recording.</summary>
        public void StartRecording()
        {
            if (_recorder.IsRecording)
                return;

            _sampleTimer = 0f;
            _warnedNoAgents = false;
            LastSavedPath = null;
            _recorder.StartRecording();

            for (int i = 0; i < PlaySensePerception.ActivePerceptions.Count; i++)
                PlaySensePerception.ActivePerceptions[i].ResetForNewSession();

            // Take the first sample now so the path starts at t = 0.
            SampleAgents();

            Debug.Log("[PlaySense] Recording started (" + _recorder.CurrentSession.SceneName + ").");
        }

        /// <summary>
        /// Stops the session, closes any attention still in progress and
        /// saves it (if autoSave is on). Returns the finished session.
        /// </summary>
        public SessionData StopRecording()
        {
            if (!_recorder.IsRecording)
                return null;

            SampleAgents();

            List<PlaySensePerception> perceptions = PlaySensePerception.ActivePerceptions;
            for (int i = perceptions.Count - 1; i >= 0; i--)
                perceptions[i].FinalizeAttention();

            SessionData session = _recorder.StopRecording();
            if (session == null)
                return null;

            if (Settings.autoSave)
                SaveSession(session);

            SessionMetrics m = session.Metrics;
            Debug.Log(
                "[PlaySense] Recording stopped: " + session.Duration.ToString("F1") + "s, " +
                m.FrameCount + " frames, " + m.EventCount + " events, " +
                m.AttentionCount + " attention observations, " +
                m.TotalDistance.ToString("F1") + " m travelled." +
                (LastSavedPath != null ? "\nSaved: " + LastSavedPath : string.Empty));

            return session;
        }

        /// <summary>Saves a session (defaults to the current one). Returns the file path or null.</summary>
        public string SaveSession(SessionData session = null)
        {
            if (session == null)
                session = _recorder.CurrentSession;

            if (session == null)
                return null;

            string path = SessionStorage.Save(session);
            if (path == null)
                return null;

            LastSavedPath = path;

            if (SessionSaved != null)
                SessionSaved(session, path);

            return path;
        }

        // =====================================================
        // DATA INPUT (called by PlaySenseEvents / PlaySensePerception)
        // =====================================================

        internal GameEventData RecordEvent(
            string category,
            string action,
            string target,
            Vector3? position,
            IList<EventParameter> metadata)
        {
            GameEventData gameEvent = _recorder.RecordEvent(category, action, target, position, metadata);

            if (gameEvent != null && Settings.verboseLogging)
                Debug.Log("[PlaySense] " + gameEvent.Timestamp.ToString("F2") + "s  " + gameEvent);

            return gameEvent;
        }

        internal void RecordBehaviour(BehaviourObservation observation)
        {
            _recorder.RecordBehaviour(observation);
        }

        // =====================================================
        // SAMPLING
        // =====================================================

        private void SampleAgents()
        {
            List<PlaySenseAgent> agents = PlaySenseAgent.ActiveAgents;

            if (agents.Count == 0)
            {
                if (!_warnedNoAgents)
                {
                    Debug.LogWarning("[PlaySense] Recording, but no PlaySenseAgent is enabled. Add one to your player.");
                    _warnedNoAgents = true;
                }

                return;
            }

            for (int i = 0; i < agents.Count; i++)
                _recorder.RecordAgent(agents[i]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Needed when "Enter Play Mode Options" skips domain reload.
            Instance = null;
            SessionSaved = null;
        }
    }
}
