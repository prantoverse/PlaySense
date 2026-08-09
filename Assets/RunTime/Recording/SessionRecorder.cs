using System;
using UnityEngine;
using UnityEngine.SceneManagement;

using PlaySense.Data.Models;
using PlaySense.Reporting;
using PlaySense.Core;

namespace PlaySense.Recording
{
    public class SessionRecorder
    {
        private SessionData _session;

        // PlaySense's own session clock.
        private float _sessionStartTime;

        private bool _isRecording;

        // =====================================================
        // SESSION
        // =====================================================

        public SessionData CurrentSession =>
            _session;

        public bool IsRecording =>
            _isRecording;

        public float ElapsedTime
        {
            get
            {
                if (!_isRecording)
                {
                    if (_session != null)
                        return _session.Duration;

                    return 0f;
                }

                return Time.realtimeSinceStartup -
                       _sessionStartTime;
            }
        }

        // =====================================================
        // START
        // =====================================================

        public void StartRecording()
        {
            _session =
                new SessionData();

            _session.SessionId =
                Guid.NewGuid().ToString();

            _session.SceneName =
                SceneManager.GetActiveScene().name;

            _session.Duration =
                0f;

            _sessionStartTime =
                Time.realtimeSinceStartup;

            _isRecording =
                true;

            Debug.Log(
                "[PlaySense] Session clock started.");
        }

        // =====================================================
        // TRACKABLE
        // =====================================================

        public void RecordTrackable(
            PlaySenseAgent agent)
        {
            if (!_isRecording ||
                _session == null ||
                agent == null)
            {
                return;
            }

            _session.Frames.Add(
                new FrameData
                {
                    ObjectName =
                        agent.name,

                    Timestamp =
                        ElapsedTime,

                    Position =
                        agent.transform.position,

                    Rotation =
                        agent.transform.rotation
                });
        }

        // =====================================================
        // GAMEPLAY EVENT
        // =====================================================

        public void RecordEvent(
            string category,
            string action,
            string target,
            Vector3 position)
        {
            if (!_isRecording ||
                _session == null)
            {
                return;
            }

            _session.Events.Add(
                new GameEventData
                {
                    Category =
                        category,

                    Action =
                        action,

                    Target =
                        target,

                    Timestamp =
                        ElapsedTime,

                    Position =
                        position
                });

            Debug.Log(
                $"[PlaySense] Event recorded: " +
                $"{action} → {target} " +
                $"at {ElapsedTime:F2}s");
        }

        // =====================================================
        // BEHAVIOUR
        // =====================================================

        public void RecordBehaviour(
            BehaviourObservation observation)
        {
            if (!_isRecording ||
                _session == null ||
                observation == null)
            {
                return;
            }

            // Make sure the observation uses
            // PlaySense's session timeline.
            observation.Timestamp =
                observation.Timestamp;

            _session.BehaviourObservations.Add(
                observation);

            Debug.Log(
                $"[PlaySense] Behaviour recorded: " +
                $"{observation.Type} → " +
                $"{observation.Subject} " +
                $"at {observation.Timestamp:F2}s " +
                $"for {observation.Duration:F2}s");
        }

        // =====================================================
        // STOP
        // =====================================================

        public SessionData StopRecording()
        {
            if (_session == null)
                return null;

            if (!_isRecording)
                return _session;

            // Capture the final session time BEFORE
            // disabling the recording state.
            float finalDuration =
                Time.realtimeSinceStartup -
                _sessionStartTime;

            _session.Duration =
                Mathf.Max(
                    0f,
                    finalDuration);

            _session.SceneName =
                SceneManager.GetActiveScene().name;

            _isRecording =
                false;

            // Analyse the completed session.
            SessionAnalyzer analyzer =
                new SessionAnalyzer();

            _session.Metrics =
                analyzer.Analyze(
                    _session);

            Debug.Log(
                $"[PlaySense] Session clock stopped: " +
                $"{_session.Duration:F2}s");

            return _session;
        }
    }
}