using System;
using System.Collections.Generic;
using UnityEngine;

using PlaySense.Core;
using PlaySense.Data.Models;

namespace PlaySense.Perception
{
    public class PlaySensePerception : MonoBehaviour
    {
        // =====================================================
        // DETECTION
        // =====================================================

        [Header("Detection")]

        [SerializeField]
        private float detectionRadius = 25f;

        [SerializeField]
        private LayerMask detectionLayers = ~0;

        // =====================================================
        // ATTENTION
        // =====================================================

        [Header("Attention")]

        [SerializeField]
        private float attentionAngle = 15f;

        [SerializeField]
        private float minimumAttentionTime = 0.5f;

        [SerializeField]
        private float sustainedAttentionTime = 3f;

        // =====================================================
        // ATTENTION SCORING
        // =====================================================

        [Header("Attention Scoring")]

        [SerializeField]
        private bool useDistanceInAttentionScore = true;

        [SerializeField]
        private float attentionDistanceWeight = 0.25f;

        [SerializeField]
        private float attentionAngleWeight = 0.75f;

        // =====================================================
        // SAMPLING
        // =====================================================

        [Header("Sampling")]

        [SerializeField]
        private float sampleRate = 10f;

        // =====================================================
        // VIEWS
        // =====================================================

        [Header("Views")]

        [SerializeField]
        private bool autoDiscoverViews = true;

        [SerializeField]
        private PlaySenseView[] views;

        // =====================================================
        // DIAGNOSTICS
        // =====================================================

        [Header("Diagnostics")]

        [SerializeField]
        private bool debugPerception = true;

        [SerializeField]
        private bool debugAttention = true;

        [SerializeField]
        private bool debugPatterns = true;

        // =====================================================
        // INTERNAL REFERENCES
        // =====================================================

        private PlaySenseAgent _agent;

        private float _timer;

        private float _perceptionStartTime;

        // =====================================================
        // TRACKED OBJECTS
        // =====================================================

        private readonly Dictionary<GameObject, PerceptionState>
            _trackedObjects =
                new Dictionary<GameObject, PerceptionState>();

        // =====================================================
        // OBSERVATIONS
        // =====================================================

        private readonly List<BehaviourObservation>
            _observations =
                new List<BehaviourObservation>();

        public IReadOnlyList<BehaviourObservation>
            Observations =>
            _observations;

        // =====================================================
        // ATTENTION SEQUENCE
        // =====================================================

        private readonly List<AttentionSequenceEntry>
            _attentionSequence =
                new List<AttentionSequenceEntry>();

        public IReadOnlyList<AttentionSequenceEntry>
            AttentionSequence =>
            _attentionSequence;

        // =====================================================
        // PATTERN STATE
        // =====================================================

        private int _attentionSwitchCount;

        private float _lastAttentionEndTime;

        private GameObject _lastAttendedObject;

        private GameObject _previousAttendedObject;

        // =====================================================
        // INITIALIZATION
        // =====================================================

        private void Awake()
        {
            _agent =
                GetComponent<PlaySenseAgent>();

            if (_agent == null)
            {
                Debug.LogError(
                    "[PlaySense] PlaySensePerception " +
                    "requires a PlaySenseAgent on the same GameObject.");

                enabled = false;
                return;
            }

            _perceptionStartTime =
                Time.realtimeSinceStartup;

            RefreshViews();

            Debug.Log(
                $"[PlaySense] Perception initialized on: {name}");

            Debug.Log(
                $"[PlaySense] Views found: {views.Length}");

            foreach (PlaySenseView view in views)
            {
                if (view == null)
                    continue;

                Debug.Log(
                    $"[PlaySense] View: {view.name} | " +
                    $"Active: {view.IsActive} | " +
                    $"Position: {view.Position}");
            }
        }

        // =====================================================
        // SESSION-RELATIVE PERCEPTION TIME
        // =====================================================

        private float PerceptionTime()
        {
            return
                Time.realtimeSinceStartup -
                _perceptionStartTime;
        }

        // =====================================================
        // VIEW DISCOVERY
        // =====================================================

        private void RefreshViews()
        {
            if (!autoDiscoverViews)
            {
                if (views == null)
                    views =
                        new PlaySenseView[0];

                return;
            }

            views =
                FindObjectsByType<PlaySenseView>(
                    FindObjectsSortMode.None);

            if (views == null)
            {
                views =
                    new PlaySenseView[0];
            }
        }

        // =====================================================
        // UPDATE
        // =====================================================

        private void Update()
        {
            if (_agent == null)
                return;

            _timer += Time.deltaTime;

            float interval =
                1f /
                Mathf.Max(
                    sampleRate,
                    0.01f);

            if (_timer < interval)
                return;

            _timer = 0f;

            ScanEnvironment();

            UpdateAttention();

            UpdateAttentionPatterns();
        }

        // =====================================================
        // ENVIRONMENT SCAN
        // =====================================================

        private void ScanEnvironment()
        {
            Collider[] colliders =
                Physics.OverlapSphere(
                    _agent.Position,
                    detectionRadius,
                    detectionLayers,
                    QueryTriggerInteraction.Collide);

            if (debugPerception)
            {
                Debug.Log(
                    $"[PlaySense] Scan: " +
                    $"{colliders.Length} colliders found.");
            }

            HashSet<GameObject> currentObjects =
                new HashSet<GameObject>();

            foreach (Collider collider in colliders)
            {
                if (collider == null)
                    continue;

                GameObject target =
                    collider.gameObject;

                if (target == gameObject)
                    continue;

                currentObjects.Add(
                    target);

                if (debugPerception)
                {
                    Debug.Log(
                        $"[PlaySense] Candidate: " +
                        $"{target.name}");
                }

                EvaluateObject(target);
            }

            RemoveObjectsNoLongerNearby(
                currentObjects);
        }

        // =====================================================
        // OBJECT EVALUATION
        // =====================================================

        private void EvaluateObject(
            GameObject target)
        {
            if (target == null)
                return;

            if (!_trackedObjects.ContainsKey(target))
            {
                _trackedObjects.Add(
                    target,
                    new PerceptionState());
            }

            PerceptionState state =
                _trackedObjects[target];

            // -------------------------------------------------
            // RESET CURRENT VIEW STATE
            // -------------------------------------------------

            bool visible = false;

            float bestDistance =
                float.MaxValue;

            float bestAngle =
                180f;

            bool bestUnobstructed =
                false;

            PlaySenseView bestView =
                null;

            // -------------------------------------------------
            // TEST EVERY AVAILABLE VIEW
            // -------------------------------------------------

            foreach (PlaySenseView view in views)
            {
                if (view == null)
                    continue;

                if (!view.IsActive)
                {
                    if (debugPerception)
                    {
                        Debug.Log(
                            $"[PlaySense] View inactive: " +
                            $"{view.name}");
                    }

                    continue;
                }

                Vector3 targetPosition =
                    target.transform.position;

                Vector3 offset =
                    targetPosition -
                    view.Position;

                float distance =
                    offset.magnitude;

                if (distance <= 0.01f)
                    continue;

                // -------------------------------------------------
                // FAR CLIP
                // -------------------------------------------------

                if (distance >
                    view.FarClip)
                {
                    if (debugPerception)
                    {
                        Debug.Log(
                            $"[PlaySense] Rejected " +
                            $"{target.name}: beyond FarClip.");
                    }

                    continue;
                }

                // -------------------------------------------------
                // FOV
                // -------------------------------------------------

                float angle =
                    Vector3.Angle(
                        view.Forward,
                        offset);

                float effectiveFov =
                    view.FieldOfView *
                    0.5f;

                if (debugPerception)
                {
                    Debug.Log(
                        $"[PlaySense] {target.name} | " +
                        $"View: {view.name} | " +
                        $"Angle: {angle:F2} | " +
                        $"FOV half: {effectiveFov:F2}");
                }

                if (angle >
                    effectiveFov)
                {
                    if (debugPerception)
                    {
                        Debug.Log(
                            $"[PlaySense] Rejected " +
                            $"{target.name}: outside FOV.");
                    }

                    continue;
                }

                // -------------------------------------------------
                // OCCLUSION
                // -------------------------------------------------

                bool unobstructed =
                    IsUnobstructed(
                        view,
                        target,
                        distance,
                        offset);

                if (!unobstructed)
                {
                    if (debugPerception)
                    {
                        Debug.Log(
                            $"[PlaySense] Rejected " +
                            $"{target.name}: obstructed.");
                    }

                    continue;
                }

                visible = true;

                // -------------------------------------------------
                // BEST VIEW
                // -------------------------------------------------

                if (angle < bestAngle)
                {
                    bestAngle =
                        angle;

                    bestDistance =
                        distance;

                    bestUnobstructed =
                        true;

                    bestView =
                        view;
                }
            }

            // =====================================================
            // UPDATE PERCEPTION STATE
            // =====================================================

            float now =
                PerceptionTime();

            bool wasVisible =
                state.Visible;

            state.Position =
                target.transform.position;

            state.Direction =
                (
                    target.transform.position -
                    transform.position
                ).normalized;

            state.Distance =
                visible
                    ? bestDistance
                    : 0f;

            state.Angle =
                visible
                    ? bestAngle
                    : 180f;

            state.Visible =
                visible;

            state.Unobstructed =
                bestUnobstructed;

            state.BestView =
                bestView;

            state.ViewName =
                bestView != null
                    ? bestView.name
                    : string.Empty;

            // =====================================================
            // VISIBILITY HISTORY
            // =====================================================

            if (visible)
            {
                if (!wasVisible)
                {
                    state.FirstSeenTime =
                        now;

                    if (debugPerception)
                    {
                        Debug.Log(
                            $"[PlaySense] FIRST SEEN: " +
                            $"{target.name} at {now:F2}s");
                    }
                }

                state.LastSeenTime =
                    now;

                state.VisibleDuration +=
                    1f /
                    Mathf.Max(
                        sampleRate,
                        0.01f);
            }

            state.WasVisibleLastFrame =
                visible;

            // =====================================================
            // ATTENTION CANDIDATE
            // =====================================================

            state.AttentionCandidate =
                visible &&
                bestUnobstructed &&
                bestAngle <= attentionAngle;

            // =====================================================
            // ATTENTION SCORE
            // =====================================================

            state.AttentionScore =
                CalculateAttentionScore(
                    state,
                    bestView);

            if (debugPerception && visible)
            {
                Debug.Log(
                    $"[PlaySense] VISIBLE: " +
                    $"{target.name} | " +
                    $"View: {state.ViewName} | " +
                    $"Distance: {state.Distance:F2}m | " +
                    $"Angle: {state.Angle:F1}° | " +
                    $"Score: {state.AttentionScore:F2} | " +
                    $"Attention Candidate: " +
                    $"{state.AttentionCandidate}");
            }
        }

        // =====================================================
        // ATTENTION SCORE
        // =====================================================

        private float CalculateAttentionScore(
            PerceptionState state,
            PlaySenseView view)
        {
            if (!state.Visible ||
                !state.Unobstructed ||
                view == null)
            {
                return 0f;
            }

            // -------------------------------------------------
            // ANGLE SCORE
            // -------------------------------------------------

            float angleScore =
                Mathf.InverseLerp(
                    attentionAngle,
                    0f,
                    state.Angle);

            // -------------------------------------------------
            // DISTANCE SCORE
            // -------------------------------------------------

            float distanceScore =
                1f;

            if (useDistanceInAttentionScore)
            {
                float maxDistance =
                    Mathf.Max(
                        view.FarClip,
                        0.01f);

                distanceScore =
                    Mathf.Clamp01(
                        1f -
                        (
                            state.Distance /
                            maxDistance
                        ));
            }

            // -------------------------------------------------
            // WEIGHTED SCORE
            // -------------------------------------------------

            float totalWeight =
                Mathf.Max(
                    attentionAngleWeight +
                    attentionDistanceWeight,
                    0.01f);

            float score =
                (
                    angleScore *
                    attentionAngleWeight
                )
                +
                (
                    distanceScore *
                    attentionDistanceWeight
                );

            return
                Mathf.Clamp01(
                    score /
                    totalWeight);
        }

        // =====================================================
        // ATTENTION UPDATE
        // =====================================================

        private void UpdateAttention()
        {
            float deltaTime =
                1f /
                Mathf.Max(
                    sampleRate,
                    0.01f);

            foreach (var pair in _trackedObjects)
            {
                GameObject target =
                    pair.Key;

                PerceptionState state =
                    pair.Value;

                if (state.AttentionCandidate)
                {
                    state.AttentionTime +=
                        deltaTime;

                    if (!state.IsAttending &&
                        state.AttentionTime >=
                        minimumAttentionTime)
                    {
                        BeginAttention(
                            target,
                            state);
                    }

                    continue;
                }

                if (state.IsAttending)
                {
                    EndAttention(
                        target,
                        state);
                }

                state.AttentionTime =
                    0f;
            }
        }

        // =====================================================
        // BEGIN ATTENTION
        // =====================================================

        private void BeginAttention(
            GameObject target,
            PerceptionState state)
        {
            state.IsAttending =
                true;

            state.AttentionStartTime =
                PerceptionTime() -
                state.AttentionTime;

            state.AttentionStartDistance =
                state.Distance;

            state.AttentionStartAngle =
                state.Angle;

            state.AttentionStartScore =
                state.AttentionScore;

            state.AttentionCount++;

            // -------------------------------------------------
            // ATTENTION SEQUENCE
            // -------------------------------------------------

            _previousAttendedObject =
                _lastAttendedObject;

            _lastAttendedObject =
                target;

            AttentionSequenceEntry entry =
                new AttentionSequenceEntry
                {
                    ObjectName =
                        target.name,

                    StartTime =
                        state.AttentionStartTime,

                    EndTime =
                        -1f,

                    Duration =
                        0f,

                    Distance =
                        state.AttentionStartDistance,

                    Angle =
                        state.AttentionStartAngle,

                    AttentionScore =
                        state.AttentionStartScore,

                    ViewName =
                        state.ViewName,

                    IsReinspection =
                        HasPreviouslyAttended(target)
                };

            _attentionSequence.Add(
                entry);

            if (_previousAttendedObject != null &&
                _previousAttendedObject != target)
            {
                _attentionSwitchCount++;

                float gap =
                    PerceptionTime() -
                    _lastAttentionEndTime;

                if (debugPatterns)
                {
                    Debug.Log(
                        $"[PlaySense] ATTENTION SWITCH: " +
                        $"{_previousAttendedObject.name} → " +
                        $"{target.name} | " +
                        $"Gap: {gap:F2}s");
                }
            }

            if (debugAttention)
            {
                Debug.Log(
                    $"[PlaySense] ATTENTION STARTED: " +
                    $"{target.name} | " +
                    $"Score: {state.AttentionScore:F2} | " +
                    $"Distance: {state.Distance:F2}m | " +
                    $"Angle: {state.Angle:F1}°");
            }
        }

        // =====================================================
        // END ATTENTION
        // =====================================================

        private void EndAttention(
            GameObject target,
            PerceptionState state)
        {
            float now =
                PerceptionTime();

            float duration =
                now -
                state.AttentionStartTime;

            if (duration >=
                minimumAttentionTime)
            {
                CreateObservation(
                    target,
                    state,
                    duration);
            }

            state.TotalAttentionDuration +=
                Mathf.Max(
                    duration,
                    0f);

            state.LastAttentionEndTime =
                now;

            _lastAttentionEndTime =
                now;

            // -------------------------------------------------
            // CLOSE SEQUENCE ENTRY
            // -------------------------------------------------

            if (_attentionSequence.Count > 0)
            {
                AttentionSequenceEntry entry =
                    _attentionSequence[
                        _attentionSequence.Count - 1];

                if (entry.ObjectName ==
                    target.name &&
                    entry.EndTime < 0f)
                {
                    entry.EndTime =
                        now;

                    entry.Duration =
                        duration;
                }
            }

            // -------------------------------------------------
            // PATTERN LOGGING
            // -------------------------------------------------

            if (duration >=
                sustainedAttentionTime)
            {
                if (debugPatterns)
                {
                    Debug.Log(
                        $"[PlaySense] SUSTAINED ATTENTION: " +
                        $"{target.name} | " +
                        $"{duration:F2}s");
                }
            }

            if (debugAttention)
            {
                Debug.Log(
                    $"[PlaySense] ATTENTION ENDED: " +
                    $"{target.name} | " +
                    $"Duration: {duration:F2}s");
            }

            state.IsAttending =
                false;

            state.AttentionStartTime =
                0f;

            state.AttentionTime =
                0f;

            state.AttentionStartDistance =
                0f;

            state.AttentionStartAngle =
                0f;

            state.AttentionStartScore =
                0f;
        }

        // =====================================================
        // ATTENTION PATTERNS
        // =====================================================

        private void UpdateAttentionPatterns()
        {
            if (_attentionSequence.Count < 2)
                return;

            AttentionSequenceEntry latest =
                _attentionSequence[
                    _attentionSequence.Count - 1];

            if (latest.EndTime < 0f)
                return;

            // -------------------------------------------------
            // RAPID SWITCHING
            // -------------------------------------------------

            if (_attentionSwitchCount >= 3)
            {
                if (debugPatterns)
                {
                    Debug.Log(
                        $"[PlaySense] ATTENTION PATTERN: " +
                        $"rapid environmental scanning " +
                        $"detected.");
                }

                _attentionSwitchCount = 0;
            }
        }

        // =====================================================
        // REINSPECTION
        // =====================================================

        private bool HasPreviouslyAttended(
            GameObject target)
        {
            if (target == null)
                return false;

            for (int i = 0;
                 i < _attentionSequence.Count;
                 i++)
            {
                if (_attentionSequence[i].ObjectName ==
                    target.name)
                {
                    return true;
                }
            }

            return false;
        }

        // =====================================================
        // OBSERVATION
        // =====================================================

        private void CreateObservation(
            GameObject target,
            PerceptionState state,
            float duration)
        {
            if (target == null)
                return;

            BehaviourObservation observation =
                new BehaviourObservation
                {
                    Timestamp =
                        state.AttentionStartTime,

                    Duration =
                        duration,

                    Type =
                        "ObjectAttended",

                    Subject =
                        target.name,

                    Distance =
                        state.AttentionStartDistance,

                    Angle =
                        state.AttentionStartAngle,

                    Position =
                        state.Position,

                    Visible =
                        state.Visible,

                    Unobstructed =
                        state.Unobstructed,

                    Attended =
                        true
                };

            _observations.Add(
                observation);

            if (debugAttention)
            {
                Debug.Log(
                    $"[PlaySense] OBSERVATION CREATED: " +
                    $"{target.name} | " +
                    $"Start: " +
                    $"{observation.Timestamp:F2}s | " +
                    $"Duration: " +
                    $"{duration:F2}s | " +
                    $"Score: " +
                    $"{state.AttentionStartScore:F2} | " +
                    $"Total observations: " +
                    $"{_observations.Count}");
            }
        }

        // =====================================================
        // RAYCAST / OCCLUSION
        // =====================================================

        private bool IsUnobstructed(
            PlaySenseView view,
            GameObject target,
            float distance,
            Vector3 offset)
        {
            Vector3 direction =
                offset.normalized;

            Ray ray =
                new Ray(
                    view.Position,
                    direction);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    distance,
                    detectionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                if (debugPerception)
                {
                    Debug.Log(
                        $"[PlaySense] Ray clear: " +
                        $"{target.name}");
                }

                return true;
            }

            bool targetHit =
                IsTargetOrChild(
                    hit.collider.gameObject,
                    target);

            if (debugPerception)
            {
                Debug.Log(
                    $"[PlaySense] Ray hit: " +
                    $"{hit.collider.gameObject.name} | " +
                    $"Target: {target.name} | " +
                    $"Valid: {targetHit}");
            }

            return targetHit;
        }

        // =====================================================
        // TARGET HIERARCHY
        // =====================================================

        private bool IsTargetOrChild(
            GameObject hitObject,
            GameObject target)
        {
            if (hitObject == target)
                return true;

            Transform current =
                hitObject.transform;

            while (current != null)
            {
                if (current.gameObject ==
                    target)
                {
                    return true;
                }

                current =
                    current.parent;
            }

            return false;
        }

        // =====================================================
        // REMOVE OLD OBJECTS
        // =====================================================

        private void RemoveObjectsNoLongerNearby(
            HashSet<GameObject> currentObjects)
        {
            List<GameObject> toRemove =
                new List<GameObject>();

            foreach (var pair in _trackedObjects)
            {
                if (currentObjects.Contains(
                        pair.Key))
                {
                    continue;
                }

                if (pair.Value.IsAttending)
                {
                    EndAttention(
                        pair.Key,
                        pair.Value);
                }

                toRemove.Add(
                    pair.Key);
            }

            foreach (GameObject target in toRemove)
            {
                _trackedObjects.Remove(
                    target);
            }
        }

        // =====================================================
        // PUBLIC QUERIES
        // =====================================================

        public bool IsObjectAttended(
            GameObject target)
        {
            if (target == null)
                return false;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return false;
            }

            return state.IsAttending;
        }

        // -----------------------------------------------------

        public float GetAttentionTime(
            GameObject target)
        {
            if (target == null)
                return 0f;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return 0f;
            }

            return state.AttentionTime;
        }

        // -----------------------------------------------------

        public float GetAttentionScore(
            GameObject target)
        {
            if (target == null)
                return 0f;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return 0f;
            }

            return state.AttentionScore;
        }

        // -----------------------------------------------------

        public float GetVisibleDuration(
            GameObject target)
        {
            if (target == null)
                return 0f;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return 0f;
            }

            return state.VisibleDuration;
        }

        // -----------------------------------------------------

        public float GetTotalAttentionDuration(
            GameObject target)
        {
            if (target == null)
                return 0f;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return 0f;
            }

            return
                state.TotalAttentionDuration +
                (
                    state.IsAttending
                        ? PerceptionTime() -
                          state.AttentionStartTime
                        : 0f
                );
        }

        // -----------------------------------------------------

        public int GetAttentionCount(
            GameObject target)
        {
            if (target == null)
                return 0;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return 0;
            }

            return state.AttentionCount;
        }

        // -----------------------------------------------------

        public bool IsObjectVisible(
            GameObject target)
        {
            if (target == null)
                return false;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return false;
            }

            return state.Visible;
        }

        // -----------------------------------------------------

        public string GetBestViewName(
            GameObject target)
        {
            if (target == null)
                return string.Empty;

            if (!_trackedObjects.TryGetValue(
                    target,
                    out PerceptionState state))
            {
                return string.Empty;
            }

            return state.ViewName;
        }

        // =====================================================
        // DEBUG GIZMOS
        // =====================================================

        private void OnDrawGizmosSelected()
        {
            Gizmos.DrawWireSphere(
                transform.position,
                detectionRadius);
        }

        // =====================================================
        // PERCEPTION STATE
        // =====================================================

        private class PerceptionState
        {
            // -------------------------------------------------
            // CURRENT PERCEPTION
            // -------------------------------------------------

            public float Distance;

            public float Angle;

            public bool Visible;

            public bool Unobstructed;

            public Vector3 Position;

            public Vector3 Direction;

            // -------------------------------------------------
            // VIEW
            // -------------------------------------------------

            public PlaySenseView BestView;

            public string ViewName;

            // -------------------------------------------------
            // VISIBILITY HISTORY
            // -------------------------------------------------

            public float FirstSeenTime;

            public float LastSeenTime;

            public float VisibleDuration;

            public bool WasVisibleLastFrame;

            // -------------------------------------------------
            // ATTENTION
            // -------------------------------------------------

            public bool AttentionCandidate;

            public bool IsAttending;

            public float AttentionTime;

            public float AttentionScore;

            public float AttentionStartTime;

            public float AttentionStartDistance;

            public float AttentionStartAngle;

            public float AttentionStartScore;

            // -------------------------------------------------
            // ATTENTION HISTORY
            // -------------------------------------------------

            public int AttentionCount;

            public float TotalAttentionDuration;

            public float LastAttentionEndTime;
        }

        // =====================================================
        // ATTENTION SEQUENCE ENTRY
        // =====================================================

        [Serializable]
        public class AttentionSequenceEntry
        {
            public string ObjectName;

            public float StartTime;

            public float EndTime;

            public float Duration;

            public float Distance;

            public float Angle;

            public float AttentionScore;

            public string ViewName;

            public bool IsReinspection;
        }
    }
}