using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlaySense.Analysis;
using PlaySense.Core;
using PlaySense.Data.Models;

namespace PlaySense.Recording
{
    /// <summary>
    /// Owns the session being recorded and the PlaySense session clock.
    /// Every timestamp in a session (frames, events, attention) comes from
    /// this one clock, so all timelines line up.
    /// </summary>
    public class SessionRecorder
    {
        private SessionData _session;
        private double _elapsedTime;
        private bool _isRecording;

        public SessionData CurrentSession
        {
            get { return _session; }
        }

        public bool IsRecording
        {
            get { return _isRecording; }
        }

        /// <summary>Seconds since recording started, on the PlaySense clock.</summary>
        public float ElapsedTime
        {
            get { return (float)_elapsedTime; }
        }

        public void StartRecording()
        {
            _session = new SessionData
            {
                SessionId = Guid.NewGuid().ToString(),
                SceneName = SceneManager.GetActiveScene().name,
                StartedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                UnityVersion = Application.unityVersion,
                Platform = Application.platform.ToString()
            };

            _elapsedTime = 0.0;
            _isRecording = true;
        }

        /// <summary>
        /// Advances the session clock. The caller clamps the step
        /// (see PlaySenseSettings.maxClockStep) so a load hitch or a
        /// paused debugger does not add seconds of fake play time.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_isRecording || deltaTime <= 0f)
                return;

            _elapsedTime += deltaTime;
        }

        public void RecordAgent(PlaySenseAgent agent)
        {
            if (!_isRecording || agent == null)
                return;

            _session.Frames.Add(new FrameData
            {
                ObjectName = agent.Id,
                Timestamp = ElapsedTime,
                Position = agent.transform.position,
                Rotation = agent.transform.rotation,
                ViewDirection = agent.ViewDirection
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
                Timestamp = ElapsedTime,
                Position = position.HasValue ? position.Value : Vector3.zero,
                HasPosition = position.HasValue
            };

            if (metadata != null)
            {
                for (int i = 0; i < metadata.Count; i++)
                {
                    if (metadata[i] != null)
                        gameEvent.Metadata.Add(metadata[i]);
                }
            }

            _session.Events.Add(gameEvent);
            return gameEvent;
        }

        public void RecordBehaviour(BehaviourObservation observation)
        {
            if (!_isRecording || observation == null)
                return;

            _session.BehaviourObservations.Add(observation);
        }

        /// <summary>Stops the clock, computes metrics and returns the finished session.</summary>
        public SessionData StopRecording()
        {
            if (_session == null)
                return null;

            if (!_isRecording)
                return _session;

            _isRecording = false;
            _session.Duration = Mathf.Max(0f, ElapsedTime);

            // Observations arrive when attention ends, which is not
            // chronological across agents. Sort for readable reports.
            _session.BehaviourObservations.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            _session.Metrics = new SessionAnalyzer().Analyze(_session);
            return _session;
        }
    }
}
