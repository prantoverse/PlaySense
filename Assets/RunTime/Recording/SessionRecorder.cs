using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlaySense.Core;
using PlaySense.Data.Models;
using PlaySense.Reporting;

namespace PlaySense.Recording
{
    public class SessionRecorder
    {
        private SessionData _session;

        // PlaySense-owned session clock.
        private double _elapsedTime;

        private bool _isRecording;

        // =====================================================
        // SESSION
        // =====================================================

        public SessionData CurrentSession =>
            _session;

        public bool IsRecording =>
            _isRecording;

        public float ElapsedTime =>
            (float)_elapsedTime;

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

            _elapsedTime =
                0.0;

            _isRecording =
                true;

            Debug.Log(
                "[PlaySense] Session clock started.");
        }

        // =====================================================
        // CLOCK
        // =====================================================

        /// <summary>
        /// Advances PlaySense's own session clock.
        /// Uses unscaled time so the telemetry clock is
        /// independent of Time.timeScale.
        /// </summary>
        public void Tick(float unscaledDeltaTime)
        {
            if (!_isRecording)
                return;

            if (unscaledDeltaTime < 0f)
                return;

            _elapsedTime +=
                unscaledDeltaTime;
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

            // Freeze the exact PlaySense-owned duration.
            _session.Duration =
                Mathf.Max(
                    0f,
                    ElapsedTime);

            _session.SceneName =
                SceneManager.GetActiveScene().name;

            _isRecording =
                false;

            // -------------------------------------------------
            // ANALYSE COMPLETED SESSION
            // -------------------------------------------------

            SessionAnalyzer analyzer =
                new SessionAnalyzer();

            _session.Metrics =
                analyzer.Analyze(
                    _session);

            // -------------------------------------------------
            // IMPORTANT:
            // Make sure metrics use the authoritative
            // PlaySense session duration.
            // -------------------------------------------------

            if (_session.Metrics != null)
            {
                _session.Metrics.Duration =
                    _session.Duration;
            }

            Debug.Log(
                $"[PlaySense] Session clock stopped: " +
                $"{_session.Duration:F2}s");

            return _session;
        }
    }
}