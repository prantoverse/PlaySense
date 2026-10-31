using System.Collections.Generic;
using UnityEngine;
using PlaySense.Core;
using PlaySense.Data.Models;
using PlaySense.Settings;

namespace PlaySense.Perception
{
    /// <summary>
    /// Records what an agent looks at.
    ///
    /// Several times a second it finds targets near the agent, checks which
    /// ones a PlaySenseView can actually see (in frustum, not occluded), and
    /// tracks the one closest to the view centre as the agent's focus.
    /// When focus ends it becomes a BehaviourObservation in the session.
    /// All thresholds come from PlaySenseSettings.
    /// </summary>
    [RequireComponent(typeof(PlaySenseAgent))]
    [DisallowMultipleComponent]
    [AddComponentMenu("PlaySense/PlaySense Perception")]
    public class PlaySensePerception : MonoBehaviour
    {
        public enum TargetMode
        {
            /// <summary>Only objects with a PlaySenseTarget component (recommended).</summary>
            PlaySenseTargetsOnly,

            /// <summary>Every collider in range. Useful for a quick look, noisy in real levels.</summary>
            AnyCollider
        }

        [Tooltip("Which objects can receive attention.")]
        [SerializeField]
        private TargetMode targetMode = TargetMode.PlaySenseTargetsOnly;

        [Tooltip("Layers searched for targets.")]
        [SerializeField]
        private LayerMask detectionLayers = ~0;

        [Tooltip("Layers that block line of sight.")]
        [SerializeField]
        private LayerMask occlusionLayers = ~0;

        [Tooltip("Use every enabled PlaySenseView in the scene. Turn off to use only the views listed below.")]
        [SerializeField]
        private bool autoDiscoverViews = true;

        [SerializeField]
        private PlaySenseView[] views = new PlaySenseView[0];

        private const int MaxColliders = 512;
        private const int MaxOcclusionHits = 16;

        private static readonly List<PlaySensePerception> s_Active = new List<PlaySensePerception>();

        private readonly Collider[] _colliderBuffer = new Collider[MaxColliders];
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[MaxOcclusionHits];
        private readonly Dictionary<int, TrackedTarget> _tracked = new Dictionary<int, TrackedTarget>();
        private readonly List<int> _removeBuffer = new List<int>();
        private readonly HashSet<string> _attendedIds = new HashSet<string>();
        private readonly List<BehaviourObservation> _observations = new List<BehaviourObservation>();

        private PlaySenseAgent _agent;
        private int _scanId;
        private float _lastSampleTime = -1f;
        private bool _warnedBufferFull;
        private bool _warnedNoViews;

        // Current focus.
        private TrackedTarget _focus;
        private float _focusStartTime;
        private float _focusLastSeenTime;
        private bool _focusConfirmed;
        private float _focusStartDistance;
        private float _focusStartAngle;
        private float _focusStartScore;
        private string _focusStartView;

        /// <summary>All enabled perception components. Do not modify.</summary>
        public static List<PlaySensePerception> ActivePerceptions
        {
            get { return s_Active; }
        }

        /// <summary>Attention observations this component produced in the current session.</summary>
        public IReadOnlyList<BehaviourObservation> Observations
        {
            get { return _observations; }
        }

        /// <summary>The object currently being attended (after the minimum attention time), or null.</summary>
        public GameObject CurrentFocus
        {
            get { return _focus != null && _focusConfirmed ? _focus.GameObject : null; }
        }

        private static PlaySenseSettings Settings
        {
            get
            {
                PlaySenseManager manager = PlaySenseManager.Instance;
                return manager != null ? manager.Settings : PlaySenseSettings.Active;
            }
        }

        // =====================================================
        // LIFECYCLE
        // =====================================================

        private void Awake()
        {
            _agent = GetComponent<PlaySenseAgent>();
        }

        private void OnEnable()
        {
            if (!s_Active.Contains(this))
                s_Active.Add(this);
        }

        private void OnDisable()
        {
            // Flush attention in progress while the session is still recording.
            FinalizeAttention();
            s_Active.Remove(this);
        }

        private void Update()
        {
            PlaySenseManager manager = PlaySenseManager.Instance;
            if (manager == null || !manager.IsRecording)
                return;

            float now = manager.SessionTime;
            float interval = 1f / Mathf.Max(1, Settings.perceptionRate);

            if (_lastSampleTime >= 0f && now - _lastSampleTime < interval)
                return;

            _lastSampleTime = now;
            Scan(now);
            UpdateFocus(now);
        }

        // =====================================================
        // SESSION CONTROL (called by PlaySenseManager)
        // =====================================================

        internal void ResetForNewSession()
        {
            _tracked.Clear();
            _attendedIds.Clear();
            _observations.Clear();
            _focus = null;
            _focusConfirmed = false;
            _lastSampleTime = -1f;
        }

        /// <summary>Ends any attention in progress and records it. Called when recording stops.</summary>
        public void FinalizeAttention()
        {
            if (_focus != null)
                EndFocus(_focusLastSeenTime);
        }

        // =====================================================
        // SCAN
        // =====================================================

        private void Scan(float now)
        {
            PlaySenseSettings s = Settings;
            _scanId++;

            int count = Physics.OverlapSphereNonAlloc(
                _agent.Position,
                s.detectionRadius,
                _colliderBuffer,
                detectionLayers,
                QueryTriggerInteraction.Collide);

            if (count >= MaxColliders && !_warnedBufferFull)
            {
                Debug.LogWarning("[PlaySense] More than " + MaxColliders + " colliders in perception range. " +
                                 "Lower the detection radius or narrow the detection layers.", this);
                _warnedBufferFull = true;
            }

            // Gather colliders per target so a target made of several
            // colliders is one target, aimed at the centre of all of them.
            for (int i = 0; i < count; i++)
            {
                Collider collider = _colliderBuffer[i];
                _colliderBuffer[i] = null;

                if (collider == null || IsPartOfAgent(collider.transform))
                    continue;

                GameObject targetObject;
                PlaySenseTarget marker;

                if (!ResolveTarget(collider, out targetObject, out marker))
                    continue;

                int key = targetObject.GetInstanceID();
                TrackedTarget tracked;

                if (!_tracked.TryGetValue(key, out tracked))
                {
                    tracked = new TrackedTarget(targetObject, marker);
                    _tracked.Add(key, tracked);
                }

                if (tracked.ScanId != _scanId)
                {
                    tracked.ScanId = _scanId;
                    tracked.Bounds = collider.bounds;
                }
                else
                {
                    tracked.Bounds.Encapsulate(collider.bounds);
                }
            }

            // Drop targets that left the radius or were destroyed.
            _removeBuffer.Clear();
            foreach (KeyValuePair<int, TrackedTarget> pair in _tracked)
            {
                if (pair.Value.ScanId != _scanId || pair.Value.GameObject == null)
                    _removeBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
            {
                TrackedTarget removed = _tracked[_removeBuffer[i]];
                removed.IsCandidate = false;
                _tracked.Remove(_removeBuffer[i]);
            }

            foreach (TrackedTarget tracked in _tracked.Values)
                Evaluate(tracked, s);
        }

        private bool ResolveTarget(Collider collider, out GameObject targetObject, out PlaySenseTarget marker)
        {
            marker = collider.GetComponentInParent<PlaySenseTarget>();

            if (marker != null)
            {
                targetObject = marker.gameObject;
                return true;
            }

            if (targetMode == TargetMode.AnyCollider)
            {
                Rigidbody body = collider.attachedRigidbody;
                targetObject = body != null ? body.gameObject : collider.gameObject;
                return true;
            }

            targetObject = null;
            return false;
        }

        private bool IsPartOfAgent(Transform other)
        {
            return other == transform || other.IsChildOf(transform);
        }

        // =====================================================
        // VISIBILITY
        // =====================================================

        private void Evaluate(TrackedTarget tracked, PlaySenseSettings s)
        {
            Vector3 aim = tracked.Marker != null && tracked.Marker.AimPoint != null
                ? tracked.Marker.AimPoint.position
                : tracked.Bounds.center;

            tracked.Position = aim;
            tracked.IsVisible = false;
            tracked.IsCandidate = false;
            tracked.Angle = 180f;
            tracked.Distance = 0f;
            tracked.Score = 0f;
            tracked.ViewName = string.Empty;

            List<PlaySenseView> viewList = autoDiscoverViews ? PlaySenseView.ActiveViews : null;
            int viewCount = viewList != null ? viewList.Count : (views != null ? views.Length : 0);

            if (viewCount == 0 && !_warnedNoViews)
            {
                Debug.LogWarning("[PlaySense] No PlaySenseView found, so no attention can be recorded. " +
                                 "Add PlaySenseView to your gameplay camera.", this);
                _warnedNoViews = true;
            }

            for (int v = 0; v < viewCount; v++)
            {
                PlaySenseView view = viewList != null ? viewList[v] : views[v];
                if (view == null || !view.IsActive || !view.ContainsPoint(aim))
                    continue;

                Vector3 offset = aim - view.Position;
                float distance = offset.magnitude;
                float angle = Vector3.Angle(view.Forward, offset);

                if (angle >= tracked.Angle)
                    continue;

                if (!HasLineOfSight(view, tracked, offset, distance))
                    continue;

                tracked.IsVisible = true;
                tracked.Angle = angle;
                tracked.Distance = distance;
                tracked.ViewName = view.name;
                tracked.ViewFarClip = view.FarClip;
            }

            if (!tracked.IsVisible)
                return;

            tracked.IsCandidate = tracked.Angle <= s.attentionAngle;
            tracked.Score = tracked.IsCandidate ? CalculateScore(tracked, s) : 0f;
        }

        private bool HasLineOfSight(PlaySenseView view, TrackedTarget tracked, Vector3 offset, float distance)
        {
            int hitCount = Physics.RaycastNonAlloc(
                view.Position,
                offset / distance,
                _hitBuffer,
                distance,
                occlusionLayers,
                QueryTriggerInteraction.Ignore);

            // RaycastNonAlloc results are unordered: find the nearest hit
            // that isn't the agent's own body (third-person cameras look
            // past the player's capsule) or the view's own object.
            float nearest = float.MaxValue;
            Transform nearestHit = null;

            for (int i = 0; i < hitCount; i++)
            {
                Transform hit = _hitBuffer[i].transform;
                if (hit == null || IsPartOfAgent(hit) || hit.IsChildOf(view.transform))
                    continue;

                if (_hitBuffer[i].distance < nearest)
                {
                    nearest = _hitBuffer[i].distance;
                    nearestHit = hit;
                }
            }

            // Nothing in the way (the aim point can sit inside the target's
            // own collider, which a ray starting outside may not report).
            if (nearestHit == null)
                return true;

            return nearestHit.IsChildOf(tracked.Transform);
        }

        private static float CalculateScore(TrackedTarget tracked, PlaySenseSettings s)
        {
            float angleScore = Mathf.InverseLerp(s.attentionAngle, 0f, tracked.Angle);

            if (!s.useDistanceInAttentionScore)
                return angleScore;

            float distanceScore = Mathf.Clamp01(1f - tracked.Distance / Mathf.Max(tracked.ViewFarClip, 0.01f));
            float totalWeight = Mathf.Max(s.attentionAngleWeight + s.attentionDistanceWeight, 0.0001f);

            return Mathf.Clamp01(
                (angleScore * s.attentionAngleWeight + distanceScore * s.attentionDistanceWeight) / totalWeight);
        }

        // =====================================================
        // FOCUS
        // =====================================================

        private void UpdateFocus(float now)
        {
            PlaySenseSettings s = Settings;

            TrackedTarget best = null;
            foreach (TrackedTarget tracked in _tracked.Values)
            {
                if (tracked.IsCandidate && (best == null || tracked.Score > best.Score))
                    best = tracked;
            }

            if (_focus != null)
            {
                if (_focus.IsCandidate)
                {
                    _focusLastSeenTime = now;

                    // Stay on the current focus unless something is clearly
                    // more central, so two nearby objects don't flicker.
                    if (best != null && best != _focus && best.Score > _focus.Score + s.attentionSwitchMargin)
                    {
                        EndFocus(now);
                        StartFocus(best, now);
                    }
                }
                else if (now - _focusLastSeenTime > s.attentionGraceTime)
                {
                    EndFocus(_focusLastSeenTime);

                    if (best != null)
                        StartFocus(best, now);
                }
            }
            else if (best != null)
            {
                StartFocus(best, now);
            }

            if (_focus != null && !_focusConfirmed && _focusLastSeenTime - _focusStartTime >= s.minimumAttentionTime)
            {
                _focusConfirmed = true;

                if (s.verboseLogging)
                {
                    Debug.Log("[PlaySense] " + _focusStartTime.ToString("F2") + "s  " + _agent.Id +
                              " attending " + _focus.DisplayName, this);
                }
            }
        }

        private void StartFocus(TrackedTarget target, float now)
        {
            _focus = target;
            _focusStartTime = now;
            _focusLastSeenTime = now;
            _focusConfirmed = false;
            _focusStartDistance = target.Distance;
            _focusStartAngle = target.Angle;
            _focusStartScore = target.Score;
            _focusStartView = target.ViewName;
        }

        private void EndFocus(float endTime)
        {
            TrackedTarget target = _focus;
            bool confirmed = _focusConfirmed;

            _focus = null;
            _focusConfirmed = false;

            if (target == null || !confirmed)
                return;

            float duration = Mathf.Max(0f, endTime - _focusStartTime);
            if (duration < Settings.minimumAttentionTime)
                return;

            BehaviourObservation observation = new BehaviourObservation
            {
                Timestamp = _focusStartTime,
                Duration = duration,
                Type = BehaviourObservation.AttentionType,
                Subject = target.DisplayName,
                TargetId = target.Id,
                Category = target.Category,
                AgentId = _agent != null ? _agent.Id : name,
                ViewName = _focusStartView,
                Distance = _focusStartDistance,
                Angle = _focusStartAngle,
                AttentionScore = _focusStartScore,
                Position = target.Position,
                IsReinspection = !_attendedIds.Add(target.Id)
            };

            _observations.Add(observation);

            PlaySenseManager manager = PlaySenseManager.Instance;
            if (manager != null)
                manager.RecordBehaviour(observation);

            if (Settings.verboseLogging)
            {
                Debug.Log("[PlaySense] " + endTime.ToString("F2") + "s  " + observation.AgentId +
                          " stopped attending " + observation.Subject + " (" + duration.ToString("F2") + "s" +
                          (observation.IsReinspection ? ", reinspection" : string.Empty) + ")", this);
            }
        }

        // =====================================================
        // GIZMOS
        // =====================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, PlaySenseSettings.Active.detectionRadius);

            if (!Application.isPlaying)
                return;

            foreach (TrackedTarget tracked in _tracked.Values)
            {
                if (!tracked.IsVisible)
                    continue;

                Gizmos.color = tracked == _focus
                    ? (_focusConfirmed ? Color.green : Color.yellow)
                    : new Color(1f, 1f, 1f, 0.25f);

                Gizmos.DrawLine(transform.position, tracked.Position);
                Gizmos.DrawWireCube(tracked.Bounds.center, tracked.Bounds.size);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Active.Clear();
        }

        // =====================================================
        // TRACKED TARGET
        // =====================================================

        private class TrackedTarget
        {
            public readonly GameObject GameObject;
            public readonly Transform Transform;
            public readonly PlaySenseTarget Marker;
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Category;

            public int ScanId;
            public Bounds Bounds;
            public Vector3 Position;
            public bool IsVisible;
            public bool IsCandidate;
            public float Angle;
            public float Distance;
            public float Score;
            public float ViewFarClip;
            public string ViewName = string.Empty;

            public TrackedTarget(GameObject gameObject, PlaySenseTarget marker)
            {
                GameObject = gameObject;
                Transform = gameObject.transform;
                Marker = marker;
                Id = marker != null ? marker.Id : PlaySenseTarget.GetHierarchyPath(gameObject.transform);
                DisplayName = marker != null ? marker.DisplayName : gameObject.name;
                Category = marker != null ? marker.Category : string.Empty;
            }
        }
    }
}
