using UnityEngine;
using UnityEngine.Serialization;

namespace PlaySense.Settings
{
    /// <summary>
    /// Project-wide PlaySense configuration. Every threshold PlaySense uses
    /// (recording, perception and analysis) lives here so the runtime, the
    /// dashboard and the PDF report always agree.
    ///
    /// Put an asset named "PlaySenseSettings" in any Resources folder, or assign
    /// one on the PlaySenseManager. Without either, defaults are used.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaySenseSettings", menuName = "PlaySense/Settings", order = 0)]
    public class PlaySenseSettings : ScriptableObject
    {
        public const string ResourcesName = "PlaySenseSettings";

        [Header("Recording")]
        [Tooltip("How many times per second agent positions are sampled.")]
        [Range(1, 120)]
        public int samplingRate = 20;

        [Tooltip("Recording stops (and is saved) after this many minutes. 0 = no limit.")]
        [Min(0)]
        public int maxRecordingMinutes = 30;

        [Tooltip("Largest frame delta the session clock accepts, in seconds. " +
                 "Stops loading hitches and breakpoints from inflating session time.")]
        [Range(0.02f, 2f)]
        public float maxClockStep = 0.25f;

        [Tooltip("Save the session to disk automatically when recording stops.")]
        [FormerlySerializedAs("autoExport")]
        public bool autoSave = true;

        [Tooltip("Record in player builds as well as in the editor.")]
        public bool recordInBuilds = true;

        [Tooltip("Keep one session running across scene loads (one playthrough = one session). " +
                 "When off, each scene is its own session.")]
        public bool keepSessionAcrossScenes = true;

        [Header("Perception")]
        [Tooltip("How many times per second perception scans for targets.")]
        [Range(1, 60)]
        public int perceptionRate = 10;

        [Tooltip("Radius around the agent that is scanned for targets (metres).")]
        [Min(0.1f)]
        public float detectionRadius = 25f;

        [Tooltip("A target counts as attended when it is within this many degrees of the view centre.")]
        [Range(1f, 90f)]
        public float attentionAngle = 15f;

        [Tooltip("Attention shorter than this is ignored (seconds).")]
        [Min(0f)]
        public float minimumAttentionTime = 0.5f;

        [Tooltip("Attention survives brief interruptions (a blink, a passing occluder) shorter than this (seconds).")]
        [Min(0f)]
        public float attentionGraceTime = 0.25f;

        [Tooltip("Focus switches to a new target only when its attention score beats the current one by this much.")]
        [Range(0f, 1f)]
        public float attentionSwitchMargin = 0.15f;

        [Tooltip("Include distance in the attention score (closer = stronger).")]
        public bool useDistanceInAttentionScore = true;

        [Range(0f, 1f)] public float attentionAngleWeight = 0.75f;
        [Range(0f, 1f)] public float attentionDistanceWeight = 0.25f;

        [Header("Analysis")]
        [Tooltip("Attention shorter than this is reported as a brief glance (seconds).")]
        [Min(0f)]
        public float briefAttentionThreshold = 1f;

        [Tooltip("Attention at least this long is reported as sustained (seconds).")]
        [Min(0f)]
        public float sustainedAttentionThreshold = 3f;

        [Tooltip("Agents slower than this are considered idle (m/s).")]
        [Min(0f)]
        public float idleSpeedThreshold = 0.08f;

        [Tooltip("Movement faster than this between two samples is treated as a teleport " +
                 "and excluded from distance and speed (m/s).")]
        [Min(1f)]
        public float maxPlausibleSpeed = 50f;

        [Header("Replay")]
        [Tooltip("Default playback speed in the Replay window.")]
        [Min(0.1f)]
        public float replaySpeed = 1f;

        [Tooltip("Heatmap cell size in metres.")]
        [Min(0.1f)]
        public float heatmapCellSize = 1f;

        [Header("Diagnostics")]
        [Tooltip("Log every attention start/end and recorded event to the Console.")]
        public bool verboseLogging = false;

        // =====================================================
        // ACTIVE SETTINGS
        // =====================================================

        private static PlaySenseSettings s_Active;
        private static PlaySenseSettings s_Defaults;

        /// <summary>
        /// The settings currently in use: the one assigned by the manager, else
        /// the Resources asset, else built-in defaults. Never null.
        /// </summary>
        public static PlaySenseSettings Active
        {
            get
            {
                if (s_Active != null)
                    return s_Active;

                s_Active = Resources.Load<PlaySenseSettings>(ResourcesName);
                if (s_Active != null)
                    return s_Active;

                if (s_Defaults == null)
                {
                    s_Defaults = CreateInstance<PlaySenseSettings>();
                    s_Defaults.name = "PlaySenseSettings (Defaults)";
                    s_Defaults.hideFlags = HideFlags.HideAndDontSave;
                }

                return s_Defaults;
            }
        }

        /// <summary>Overrides <see cref="Active"/>. Pass null to fall back to Resources/defaults.</summary>
        public static void SetActive(PlaySenseSettings settings)
        {
            s_Active = settings;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Active = null;
        }
    }
}
