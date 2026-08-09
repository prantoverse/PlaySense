using UnityEngine;

using PlaySense.Data.Models;
using PlaySense.Export;
using PlaySense.Perception;
using PlaySense.Recording;

namespace PlaySense.Core
{
    public class PlaySenseManager : MonoBehaviour
    {
        [SerializeField]
        private float sampleRate = 20f;

        private SessionRecorder _recorder;

        private PlaySenseAgent[] _agents;

        private PlaySensePerception[] _perceptionSystems;

        private float _timer;

        // Each perception system has its own observation cursor.
        private int[] _observationCursors;

        // =====================================================
        // INITIALIZATION
        // =====================================================

        private void Awake()
        {
            Debug.Log(
                "[PlaySense] Awake");

            _recorder =
                new SessionRecorder();

            _agents =
                FindObjectsByType<PlaySenseAgent>(
                    FindObjectsSortMode.None);

            _perceptionSystems =
                FindObjectsByType<PlaySensePerception>(
                    FindObjectsSortMode.None);

            _observationCursors =
                new int[
                    _perceptionSystems.Length];

            Debug.Log(
                $"[PlaySense] Found " +
                $"{_agents.Length} agents.");

            Debug.Log(
                $"[PlaySense] Found " +
                $"{_perceptionSystems.Length} " +
                $"perception systems.");
        }

        // =====================================================
        // START
        // =====================================================

        private void Start()
        {
            Debug.Log(
                "[PlaySense] Starting PlaySense...");

            _recorder.StartRecording();

            Debug.Log(
                "[PlaySense] Recording Started!");
        }

        // =====================================================
        // GAME EVENT
        // =====================================================

        public void RecordEvent(
            GameEventData gameEvent)
        {
            if (gameEvent == null)
                return;

            _recorder.RecordEvent(
                gameEvent.Category,
                gameEvent.Action,
                gameEvent.Target,
                gameEvent.Position);
        }

        // =====================================================
        // UPDATE
        // =====================================================

        private void Update()
        {
            if (_recorder == null ||
                !_recorder.IsRecording)
            {
                return;
            }

            // =================================================
            // AUTHORITATIVE PLAY SENSE CLOCK
            // =================================================

            _recorder.Tick(
                Time.unscaledDeltaTime);

            // =================================================
            // TELEMETRY SAMPLING
            // =================================================

            _timer +=
                Time.unscaledDeltaTime;

            float interval =
                1f /
                Mathf.Max(
                    sampleRate,
                    0.01f);

            if (_timer < interval)
                return;

            _timer = 0f;

            RecordAgents();

            RecordBehaviour();
        }

        // =====================================================
        // AGENTS
        // =====================================================

        private void RecordAgents()
        {
            if (_agents == null)
                return;

            foreach (
                PlaySenseAgent agent
                in _agents)
            {
                if (agent == null)
                    continue;

                _recorder.RecordTrackable(
                    agent);
            }
        }

        // =====================================================
        // BEHAVIOUR
        // =====================================================

        private void RecordBehaviour()
        {
            if (_perceptionSystems == null)
                return;

            for (
                int i = 0;
                i < _perceptionSystems.Length;
                i++)
            {
                PlaySensePerception perception =
                    _perceptionSystems[i];

                if (perception == null)
                    continue;

                var observations =
                    perception.Observations;

                if (observations == null)
                    continue;

                int cursor =
                    _observationCursors[i];

                while (
                    cursor <
                    observations.Count)
                {
                    BehaviourObservation observation =
                        observations[cursor];

                    if (observation != null)
                    {
                        _recorder.RecordBehaviour(
                            observation);

                        Debug.Log(
                            $"[PlaySense] " +
                            $"Recorded behaviour: " +
                            $"{observation.Type} → " +
                            $"{observation.Subject} " +
                            $"({observation.Duration:F2}s)");
                    }

                    cursor++;
                }

                _observationCursors[i] =
                    cursor;
            }
        }

        // =====================================================
        // STOP
        // =====================================================

        private void OnDestroy()
        {
            if (_recorder == null)
                return;

            // -------------------------------------------------
            // COLLECT FINAL BEHAVIOUR OBSERVATIONS
            // -------------------------------------------------

            RecordBehaviour();

            // -------------------------------------------------
            // STOP SESSION
            // -------------------------------------------------

            SessionData session =
                _recorder.StopRecording();

            if (session == null)
                return;

            // -------------------------------------------------
            // SAVE
            // -------------------------------------------------

            SessionStorage.Save(
                session);

            // -------------------------------------------------
            // DEBUG SUMMARY
            // -------------------------------------------------

            Debug.Log(
                "====== PLAYSENSE ======");

            Debug.Log(
                $"Frames: " +
                $"{session.Metrics?.FrameCount ?? 0}");

            Debug.Log(
                $"Duration: " +
                $"{session.Duration:F2}s");

            if (session.Metrics != null)
            {
                Debug.Log(
                    $"Distance: " +
                    $"{session.Metrics.TotalDistance:F2}m");

                Debug.Log(
                    $"Average Speed: " +
                    $"{session.Metrics.AverageSpeed:F2}m/s");
            }

            Debug.Log(
                $"Behaviour Observations: " +
                $"{session.BehaviourObservations?.Count ?? 0}");
        }
    }
}