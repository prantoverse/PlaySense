using System.Collections.Generic;
using UnityEngine;

namespace PlaySense.Core
{
    /// <summary>
    /// Samples the position and view direction of every enabled PlaySenseAgent
    /// at the Settings sampling rate. Added to the manager automatically.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlaySense/Modules/Movement")]
    [PlaySenseModuleInfo("Movement", "Player paths, speed, idle time and the Replay heatmap. Samples every PlaySenseAgent.")]
    public class PlaySenseMovementModule : PlaySenseModule
    {
        public const string Id = "Movement";

        private float _timer;
        private bool _warnedNoAgents;

        public override string ModuleId
        {
            get { return Id; }
        }

        public override int Order
        {
            get { return 0; }
        }

        public override void OnSessionStart(PlaySenseSession session)
        {
            _timer = 0f;
            _warnedNoAgents = false;

            // First sample at t = 0 so paths start where the player spawned.
            Sample(session);
        }

        public override void OnSessionUpdate(PlaySenseSession session, float deltaTime)
        {
            // Keep the remainder so the real sample rate matches the setting.
            float interval = 1f / Mathf.Max(1, session.Settings.samplingRate);
            _timer += deltaTime;

            if (_timer < interval)
                return;

            _timer -= interval;

            // After a long hitch don't fire a burst of catch-up samples.
            if (_timer > interval)
                _timer = 0f;

            Sample(session);
        }

        public override void OnSceneChanged(PlaySenseSession session, string sceneName)
        {
            Sample(session);
        }

        public override void OnSessionStop(PlaySenseSession session)
        {
            Sample(session);
        }

        private void Sample(PlaySenseSession session)
        {
            List<PlaySenseAgent> agents = PlaySenseAgent.ActiveAgents;

            if (agents.Count == 0)
            {
                if (!_warnedNoAgents)
                {
                    Debug.LogWarning("[PlaySense] Movement module is recording, but no PlaySenseAgent is enabled. Add one to your player.");
                    _warnedNoAgents = true;
                }

                return;
            }

            for (int i = 0; i < agents.Count; i++)
            {
                PlaySenseAgent agent = agents[i];
                if (agent != null)
                    session.RecordFrame(agent.Id, agent.transform.position, agent.transform.rotation, agent.ViewDirection);
            }
        }
    }
}
