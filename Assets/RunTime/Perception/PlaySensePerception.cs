using System.Collections.Generic;
using UnityEngine;

using PlaySense.Core;
using PlaySense.Data.Models;

namespace PlaySense.Perception
{
    public class PlaySensePerception : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField]
        private float detectionRadius = 25f;

        [SerializeField]
        private LayerMask detectionLayers = ~0;

        [Header("Attention")]
        [SerializeField]
        private float attentionAngle = 15f;

        [SerializeField]
        private float minimumAttentionTime = 0.5f;

        [Header("Sampling")]
        [SerializeField]
        private float sampleRate = 10f;

        [Header("Views")]
        [SerializeField]
        private bool autoDiscoverViews = true;

        [SerializeField]
        private PlaySenseView[] views;

        [Header("Diagnostics")]
        [SerializeField]
        private bool debugPerception = true;

        private PlaySenseAgent _agent;

        private float _timer;

        private readonly Dictionary<GameObject, PerceptionState>
            _trackedObjects =
                new Dictionary<GameObject, PerceptionState>();

        private readonly List<BehaviourObservation>
            _observations =
                new List<BehaviourObservation>();

        public IReadOnlyList<BehaviourObservation>
            Observations =>
            _observations;

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
        // VIEW DISCOVERY
        // =====================================================

        private void RefreshViews()
        {
            if (!autoDiscoverViews)
                return;

            views =
                FindObjectsByType<PlaySenseView>(
                    FindObjectsSortMode.None);

            if (views == null)
                views =
                    new PlaySenseView[0];
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
                1f / Mathf.Max(
                    sampleRate,
                    0.01f);

            if (_timer < interval)
                return;

            _timer = 0f;

            ScanEnvironment();
            UpdateAttention();
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

                currentObjects.Add(target);

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

            bool visible =
                false;

            float bestDistance =
                float.MaxValue;

            float bestAngle =
                180f;

            bool bestUnobstructed =
                false;

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

                if (debugPerception)
                {
                    Debug.Log(
                        $"[PlaySense] Checking " +
                        $"{target.name} from {view.name} | " +
                        $"Distance: {distance:F2}");
                }

                if (distance <= 0.01f)
                    continue;

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

                float angle =
                    Vector3.Angle(
                        view.Forward,
                        offset);

                float effectiveFov =
                    view.FieldOfView * 0.5f;

                if (debugPerception)
                {
                    Debug.Log(
                        $"[PlaySense] {target.name} | " +
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

                if (angle < bestAngle)
                {
                    bestAngle =
                        angle;

                    bestDistance =
                        distance;

                    bestUnobstructed =
                        true;
                }
            }

            if (!_trackedObjects.ContainsKey(target))
            {
                _trackedObjects.Add(
                    target,
                    new PerceptionState());
            }

            PerceptionState state =
                _trackedObjects[target];

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

            state.AttentionCandidate =
                visible &&
                bestAngle <= attentionAngle;

            if (visible)
            {
                Debug.Log(
                    $"[PlaySense] VISIBLE: " +
                    $"{target.name} | " +
                    $"Angle: {bestAngle:F1}° | " +
                    $"Distance: {bestDistance:F1}m | " +
                    $"Attention: {state.AttentionCandidate}");
            }
        }

        // =====================================================
        // ATTENTION
        // =====================================================

        private void UpdateAttention()
        {
            float deltaTime =
                1f / Mathf.Max(
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

                state.AttentionTime = 0f;
            }
        }

        // =====================================================
        // BEGIN ATTENTION
        // =====================================================

        private void BeginAttention(
            GameObject target,
            PerceptionState state)
        {
            state.IsAttending = true;

            state.AttentionStartTime =
                Time.time -
                state.AttentionTime;

            state.AttentionStartDistance =
                state.Distance;

            state.AttentionStartAngle =
                state.Angle;

            Debug.Log(
                $"[PlaySense] ATTENTION STARTED: " +
                $"{target.name}");
        }

        // =====================================================
        // END ATTENTION
        // =====================================================

        private void EndAttention(
            GameObject target,
            PerceptionState state)
        {
            float duration =
                Time.time -
                state.AttentionStartTime;

            if (duration >=
                minimumAttentionTime)
            {
                CreateObservation(
                    target,
                    state,
                    duration);
            }

            Debug.Log(
                $"[PlaySense] ATTENTION ENDED: " +
                $"{target.name} | " +
                $"Duration: {duration:F2}s");

            state.IsAttending = false;

            state.AttentionStartTime = 0f;

            state.AttentionTime = 0f;
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
                        state.Distance,

                    Angle =
                        state.Angle,

                    Position =
                        target.transform.position,

                    Visible =
                        state.Visible,

                    Unobstructed =
                        state.Unobstructed,

                    Attended =
                        true
                };

            _observations.Add(
                observation);

            Debug.Log(
                $"[PlaySense] OBSERVATION CREATED: " +
                $"{target.name} | " +
                $"{duration:F2}s | " +
                $"Total observations: " +
                $"{_observations.Count}");
        }

        // =====================================================
        // RAYCAST
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
                if (current.gameObject == target)
                    return true;

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
                if (!currentObjects.Contains(
                        pair.Key))
                {
                    if (pair.Value.IsAttending)
                    {
                        EndAttention(
                            pair.Key,
                            pair.Value);
                    }

                    toRemove.Add(
                        pair.Key);
                }
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
        // INTERNAL STATE
        // =====================================================

        private class PerceptionState
        {
            public float Distance;

            public float Angle;

            public bool Visible;

            public bool Unobstructed;

            public bool AttentionCandidate;

            public float AttentionTime;

            public bool IsAttending;

            public float AttentionStartTime;

            public float AttentionStartDistance;

            public float AttentionStartAngle;
        }
    }
}