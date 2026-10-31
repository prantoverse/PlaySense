using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlaySense.Data.Models;
using PlaySense.Settings;
using PlaySense.Storage;

namespace PlaySense.Core
{
    /// <summary>
    /// Runs PlaySense recording sessions. Add one to your first scene.
    ///
    /// It drives the session clock and tells every enabled PlaySenseModule
    /// when a session starts, updates, changes scene and stops. With
    /// "Keep Session Across Scenes" on (default) it survives scene loads so
    /// one playthrough is one session; managers placed in later scenes step
    /// aside automatically.
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

        [Tooltip("Add the Movement module automatically if this object doesn't have one.")]
        [SerializeField]
        private bool autoAddMovementModule = true;

        private readonly List<PlaySenseModule> _started = new List<PlaySenseModule>();
        private readonly List<PlaySenseModule> _iteration = new List<PlaySenseModule>();
        private PlaySenseSession _session;
        private SessionData _lastSession;
        private bool _isPersistent;

        /// <summary>The manager currently running, if any.</summary>
        public static PlaySenseManager Instance { get; private set; }

        /// <summary>Raised when a session starts.</summary>
        public static event Action<PlaySenseSession> SessionStarted;

        /// <summary>Raised after a session is saved, with the session and its file path.</summary>
        public static event Action<SessionData, string> SessionSaved;

        public PlaySenseSettings Settings
        {
            get { return settings != null ? settings : PlaySenseSettings.Active; }
        }

        public bool IsRecording
        {
            get { return _session != null && _session.IsRecording; }
        }

        /// <summary>The session being recorded, or null.</summary>
        public PlaySenseSession Session
        {
            get { return IsRecording ? _session : null; }
        }

        /// <summary>Seconds since recording started on the session clock. 0 when not recording.</summary>
        public float SessionTime
        {
            get { return IsRecording ? _session.Time : 0f; }
        }

        /// <summary>The session being recorded, or the last one that stopped.</summary>
        public SessionData CurrentSession
        {
            get { return _session != null ? _session.Data : _lastSession; }
        }

        /// <summary>Path of the last saved session file.</summary>
        public string LastSavedPath { get; private set; }

        // =====================================================
        // LIFECYCLE
        // =====================================================

        private void Reset()
        {
            if (GetComponent<PlaySenseMovementModule>() == null)
                gameObject.AddComponent<PlaySenseMovementModule>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Instance._isPersistent)
                {
                    // A later level has its own manager; the persistent one keeps recording.
                    RemoveDuplicate();
                    return;
                }

                Debug.LogWarning("[PlaySense] More than one PlaySenseManager in the scene. Disabling '" + name + "'.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (settings != null)
                PlaySenseSettings.SetActive(settings);

            if (autoAddMovementModule && GetComponent<PlaySenseMovementModule>() == null)
                gameObject.AddComponent<PlaySenseMovementModule>();

            if (Settings.keepSessionAcrossScenes)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
                _isPersistent = true;
                SceneManager.activeSceneChanged += OnActiveSceneChanged;
            }
        }

        private void Start()
        {
            if (Instance != this || !recordOnStart)
                return;

            if (!Application.isEditor && !Settings.recordInBuilds)
                return;

            StartRecording();
        }

        private void Update()
        {
            if (!IsRecording)
                return;

            PlaySenseSettings s = Settings;
            float step = Mathf.Min(Time.unscaledDeltaTime, s.maxClockStep);
            _session.Tick(step);

            // Modules enabled before the manager existed, or by late registration.
            List<PlaySenseModule> active = PlaySenseModule.ActiveModules;
            for (int i = 0; i < active.Count; i++)
            {
                if (!_started.Contains(active[i]))
                    StartModule(active[i]);
            }

            _iteration.Clear();
            _iteration.AddRange(_started);

            for (int i = 0; i < _iteration.Count; i++)
            {
                PlaySenseModule module = _iteration[i];
                if (module == null)
                    continue;

                try
                {
                    module.OnSessionUpdate(_session, step);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, module);
                }
            }

            if (s.maxRecordingMinutes > 0 && _session.Time >= s.maxRecordingMinutes * 60f)
            {
                Debug.Log("[PlaySense] Max recording time reached (" + s.maxRecordingMinutes + " min). Stopping.");
                StopRecording();
            }
        }

        // Unity calls OnApplicationQuit on every object before any
        // OnDisable/OnDestroy, so modules are still alive here.
        private void OnApplicationQuit()
        {
            StopRecording();
        }

        private void OnDisable()
        {
            if (Instance == this)
                StopRecording();
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Instance = null;
        }

        private void RemoveDuplicate()
        {
            // Remove this manager and the modules on its object (the persistent
            // manager already has its own). Other components are left alone.
            PlaySenseModule[] modules = GetComponents<PlaySenseModule>();
            for (int i = 0; i < modules.Length; i++)
                Destroy(modules[i]);

            enabled = false;
            Destroy(this);
        }

        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            if (!IsRecording)
                return;

            _session.EnterScene(next.name);
            _session.RecordEvent("Scene", "Entered", next.name, null, null);

            for (int i = 0; i < _started.Count; i++)
            {
                try
                {
                    _started[i].OnSceneChanged(_session, next.name);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, _started[i]);
                }
            }
        }

        // =====================================================
        // PUBLIC CONTROL
        // =====================================================

        /// <summary>Starts a new session. Does nothing if one is already recording.</summary>
        public void StartRecording()
        {
            if (IsRecording || Instance != this)
                return;

            _started.Clear();
            LastSavedPath = null;
            _session = new PlaySenseSession(Settings);

            List<PlaySenseModule> active = new List<PlaySenseModule>(PlaySenseModule.ActiveModules);
            active.Sort((a, b) => a.Order.CompareTo(b.Order));

            for (int i = 0; i < active.Count; i++)
                StartModule(active[i]);

            if (_started.Count == 0)
                Debug.LogWarning("[PlaySense] Recording, but no PlaySense modules are enabled, so nothing will be recorded.");

            if (SessionStarted != null)
                SessionStarted(_session);

            Debug.Log("[PlaySense] Recording started in " + _session.SceneName + " (" + _started.Count + " module(s)).");
        }

        /// <summary>
        /// Stops the session, lets every module finish (attention in progress,
        /// module data) and saves it if autoSave is on. Returns the session.
        /// </summary>
        public SessionData StopRecording()
        {
            if (!IsRecording)
                return null;

            for (int i = _started.Count - 1; i >= 0; i--)
                StopModule(_started[i]);

            _started.Clear();

            SessionData data = _session.Stop();
            _lastSession = data;
            _session = null;

            if (Settings.autoSave)
                SaveSession(data);

            SessionMetrics m = data.Metrics;
            Debug.Log(
                "[PlaySense] Recording stopped: " + data.Duration.ToString("F1") + "s, " +
                data.Scenes.Count + " scene(s), " + m.FrameCount + " frames, " + m.EventCount + " events, " +
                m.AttentionCount + " attention observations." +
                (LastSavedPath != null ? "\nSaved: " + LastSavedPath : string.Empty));

            return data;
        }

        /// <summary>Saves a session (defaults to the last one). Returns the file path or null.</summary>
        public string SaveSession(SessionData data = null)
        {
            if (data == null)
                data = CurrentSession;

            if (data == null)
                return null;

            string path = SessionStorage.Save(data);
            if (path == null)
                return null;

            LastSavedPath = path;

            if (SessionSaved != null)
                SessionSaved(data, path);

            return path;
        }

        // =====================================================
        // MODULES
        // =====================================================

        internal void NotifyModuleEnabled(PlaySenseModule module)
        {
            if (IsRecording && !_started.Contains(module))
                StartModule(module);
        }

        internal void NotifyModuleDisabled(PlaySenseModule module)
        {
            if (IsRecording && _started.Contains(module))
            {
                StopModule(module);
                _started.Remove(module);
            }
        }

        private void StartModule(PlaySenseModule module)
        {
            if (module == null || BelongsToOtherManager(module))
                return;

            _started.Add(module);

            if (!_session.Data.Modules.Contains(module.ModuleId))
                _session.Data.Modules.Add(module.ModuleId);

            try
            {
                module.OnSessionStart(_session);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, module);
            }
        }

        private void StopModule(PlaySenseModule module)
        {
            if (module == null)
                return;

            try
            {
                module.OnSessionStop(_session);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, module);
            }
        }

        private bool BelongsToOtherManager(PlaySenseModule module)
        {
            PlaySenseManager owner = module.GetComponent<PlaySenseManager>();
            return owner != null && owner != this;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Needed when "Enter Play Mode Options" skips domain reload.
            Instance = null;
            SessionStarted = null;
            SessionSaved = null;
        }
    }
}
