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

        private float _startTime;

        // =====================================================
        // SESSION
        // =====================================================

        public SessionData CurrentSession
        {
            get
            {
                return _session;
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

            _startTime =
                Time.time;

            Debug.Log(
                "PlaySense SessionRecorder started.");
        }

        // =====================================================
        // TRACKABLE
        // =====================================================

        public void RecordTrackable(
            PlaySenseAgent agent)
        {
            if (_session == null ||
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
                        Time.time,

                    Position =
                        agent.transform.position,

                    Rotation =
                        agent.transform.rotation
                });
        }

        // =====================================================
        // GAME EVENT
        // =====================================================

        public void RecordEvent(
            string category,
            string action,
            string target,
            Vector3 position)
        {
            if (_session == null)
                return;

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
                        Time.time -
                        _startTime,

                    Position =
                        position
                });
        }

        // =====================================================
        // BEHAVIOUR OBSERVATION
        // =====================================================

        public void RecordBehaviour(
            BehaviourObservation observation)
        {
            if (_session == null ||
                observation == null)
            {
                return;
            }

            _session.BehaviourObservations.Add(
                observation);
        }

        // =====================================================
        // STOP
        // =====================================================

        public SessionData StopRecording()
        {
            if (_session == null)
                return null;

            _session.Duration =
                Time.time -
                _startTime;

            _session.SceneName =
                SceneManager.GetActiveScene().name;

            SessionAnalyzer analyzer =
                new SessionAnalyzer();

            _session.Metrics =
                analyzer.Analyze(_session);

            Debug.Log(
                $"PlaySense recording stopped. " +
                $"Behaviour observations: " +
                $"{_session.BehaviourObservations.Count}");

            return _session;
        }
    }
}