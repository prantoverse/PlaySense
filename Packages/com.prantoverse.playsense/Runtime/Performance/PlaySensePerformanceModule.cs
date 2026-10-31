using System;
using System.Collections.Generic;
using UnityEngine;
using PlaySense.Core;

namespace PlaySense.Performance
{
    /// <summary>Frame-rate data saved by the Performance module.</summary>
    [Serializable]
    public class PerformanceData
    {
        /// <summary>Seconds between samples.</summary>
        public float SampleInterval;

        /// <summary>Session time of each sample.</summary>
        public List<float> Times = new List<float>();

        /// <summary>Average frames per second over each sample window.</summary>
        public List<float> Fps = new List<float>();

        /// <summary>Scene index (see SessionData.Scenes) of each sample.</summary>
        public List<int> Scenes = new List<int>();

        public float AverageFps;
        public float MinFps;

        /// <summary>Average of the slowest 1% of individual frames, as FPS.</summary>
        public float OnePercentLowFps;

        /// <summary>Frames that took longer than HitchThresholdMs.</summary>
        public int HitchCount;

        public float HitchThresholdMs;
    }

    /// <summary>
    /// Records frame rate during a session so you can line up "players struggled here"
    /// with "the game dropped to 20 FPS here". Add it to the PlaySenseManager object.
    ///
    /// This module is also the reference example for writing your own:
    /// sample in OnSessionUpdate, then store a [Serializable] class with
    /// session.SetModuleData(ModuleId, data) in OnSessionStop.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlaySense/Modules/Performance")]
    [PlaySenseModuleInfo("Performance", "Frame rate, 1% lows and hitches over the session, per scene.")]
    public class PlaySensePerformanceModule : PlaySenseModule
    {
        public const string Id = "Performance";

        [Tooltip("Seconds per FPS sample.")]
        [Range(0.1f, 5f)]
        [SerializeField]
        private float sampleInterval = 0.5f;

        [Tooltip("Frames slower than this count as a hitch (milliseconds).")]
        [Min(1f)]
        [SerializeField]
        private float hitchThresholdMs = 100f;

        private const int MaxStoredFrameTimes = 200000;

        private PerformanceData _data;
        private readonly List<float> _frameTimes = new List<float>();
        private float _windowTime;
        private int _windowFrames;

        public override string ModuleId
        {
            get { return Id; }
        }

        public override void OnSessionStart(PlaySenseSession session)
        {
            _data = new PerformanceData
            {
                SampleInterval = sampleInterval,
                HitchThresholdMs = hitchThresholdMs
            };

            _frameTimes.Clear();
            _windowTime = 0f;
            _windowFrames = 0;
        }

        public override void OnSessionUpdate(PlaySenseSession session, float deltaTime)
        {
            // Use the real, unclamped frame time: hitches are what we want to see.
            float frameTime = Time.unscaledDeltaTime;
            if (frameTime <= 0f)
                return;

            _windowTime += frameTime;
            _windowFrames++;

            if (_frameTimes.Count < MaxStoredFrameTimes)
                _frameTimes.Add(frameTime);

            if (frameTime * 1000f >= hitchThresholdMs)
                _data.HitchCount++;

            if (_windowTime >= sampleInterval)
            {
                _data.Times.Add(session.Time);
                _data.Fps.Add(_windowFrames / _windowTime);
                _data.Scenes.Add(session.SceneIndex);
                _windowTime = 0f;
                _windowFrames = 0;
            }
        }

        public override void OnSessionStop(PlaySenseSession session)
        {
            if (_data == null)
                return;

            if (_data.Fps.Count > 0)
            {
                float sum = 0f;
                float min = float.MaxValue;
                for (int i = 0; i < _data.Fps.Count; i++)
                {
                    sum += _data.Fps[i];
                    min = Mathf.Min(min, _data.Fps[i]);
                }

                _data.AverageFps = sum / _data.Fps.Count;
                _data.MinFps = min;
            }

            if (_frameTimes.Count > 0)
            {
                _frameTimes.Sort();
                int slowest = Mathf.Max(1, _frameTimes.Count / 100);
                float slowSum = 0f;
                for (int i = _frameTimes.Count - slowest; i < _frameTimes.Count; i++)
                    slowSum += _frameTimes[i];

                _data.OnePercentLowFps = slowest / slowSum;
            }

            session.SetModuleData(ModuleId, _data);
            _data = null;
            _frameTimes.Clear();
        }
    }
}
