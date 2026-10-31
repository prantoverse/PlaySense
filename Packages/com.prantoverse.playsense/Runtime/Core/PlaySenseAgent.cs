using System.Collections.Generic;
using UnityEngine;

namespace PlaySense.Core
{
    /// <summary>
    /// Marks an object (usually the player) whose movement PlaySense records.
    /// Add PlaySensePerception next to it to also record what it looks at.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlaySense/PlaySense Agent")]
    public class PlaySenseAgent : MonoBehaviour
    {
        [Tooltip("Name used in session files. Leave empty to use the GameObject name. " +
                 "Give agents different ids when you record more than one.")]
        [SerializeField]
        private string agentId = string.Empty;

        [Tooltip("Camera this agent looks through. Leave empty to use a camera under this object, else Camera.main.")]
        [SerializeField]
        private Camera viewCamera;

        private static readonly List<PlaySenseAgent> s_Active = new List<PlaySenseAgent>();

        private CharacterController _characterController;
        private Rigidbody _rigidbody;
        private Vector3 _previousPosition;
        private Vector3 _velocity;

        /// <summary>All enabled agents. Do not modify.</summary>
        public static List<PlaySenseAgent> ActiveAgents
        {
            get { return s_Active; }
        }

        public string Id
        {
            get { return string.IsNullOrEmpty(agentId) ? name : agentId; }
        }

        public Vector3 Position
        {
            get { return transform.position; }
        }

        public Vector3 Velocity
        {
            get { return _velocity; }
        }

        public float Speed
        {
            get { return _velocity.magnitude; }
        }

        public Camera ViewCamera
        {
            get
            {
                if (viewCamera == null)
                    FindCamera();

                return viewCamera;
            }
        }

        /// <summary>Where the agent is looking: its camera's forward, else its own forward.</summary>
        public Vector3 ViewDirection
        {
            get
            {
                Camera cam = ViewCamera;
                return cam != null ? cam.transform.forward : transform.forward;
            }
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _rigidbody = GetComponent<Rigidbody>();
            FindCamera();
        }

        private void OnEnable()
        {
            _previousPosition = transform.position;
            _velocity = Vector3.zero;

            if (!s_Active.Contains(this))
                s_Active.Add(this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
                return;

            Vector3 position = transform.position;

            if (_characterController != null)
                _velocity = _characterController.velocity;
            else if (_rigidbody != null && !_rigidbody.isKinematic)
                _velocity = _rigidbody.linearVelocity;
            else
                _velocity = (position - _previousPosition) / deltaTime;

            _previousPosition = position;
        }

        private void FindCamera()
        {
            if (viewCamera != null)
                return;

            viewCamera = GetComponentInChildren<Camera>();

            if (viewCamera == null)
                viewCamera = Camera.main;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Active.Clear();
        }
    }
}
