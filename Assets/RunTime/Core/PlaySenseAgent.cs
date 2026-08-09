using UnityEngine;

namespace PlaySense.Core
{
    public class PlaySenseAgent : MonoBehaviour
    {
        private CharacterController _characterController;
        private Rigidbody _rigidbody;
        private Camera _camera;

        private Vector3 _previousPosition;
        private Vector3 _velocity;
        private Vector3 _previousVelocity;

        private bool _initialized;

        // =====================================================
        // BASIC STATE
        // =====================================================

        public Vector3 Position =>
            transform.position;

        public Quaternion Rotation =>
            transform.rotation;

        public Vector3 Velocity =>
            _velocity;

        public float Speed =>
            _velocity.magnitude;

        public Vector3 MovementDirection
        {
            get
            {
                if (Speed <= 0.05f)
                    return Vector3.zero;

                return _velocity.normalized;
            }
        }

        public bool IsMoving =>
            Speed > 0.05f;

        public bool IsGrounded
        {
            get
            {
                if (_characterController != null)
                    return _characterController.isGrounded;

                return true;
            }
        }

        // =====================================================
        // MOVEMENT CHANGE
        // =====================================================

        public Vector3 Acceleration =>
            CalculateAcceleration();

        // =====================================================
        // COMPONENT AVAILABILITY
        // =====================================================

        public bool HasCharacterController =>
            _characterController != null;

        public bool HasRigidbody =>
            _rigidbody != null;

        // =====================================================
        // CAMERA / PERCEPTION
        // =====================================================

        public Camera PlayerCamera =>
            _camera;

        public bool HasCamera =>
            _camera != null;

        public Vector3 ViewDirection
        {
            get
            {
                if (_camera == null)
                    return transform.forward;

                return _camera.transform.forward;
            }
        }

        public Vector3 CameraPosition
        {
            get
            {
                if (_camera == null)
                    return transform.position;

                return _camera.transform.position;
            }
        }

        public float FieldOfView
        {
            get
            {
                if (_camera == null)
                    return 0f;

                return _camera.fieldOfView;
            }
        }

        // =====================================================
        // INITIALIZATION
        // =====================================================

        private void Awake()
        {
            _characterController =
                GetComponent<CharacterController>();

            _rigidbody =
                GetComponent<Rigidbody>();

            FindPlayerCamera();

            _previousPosition =
                transform.position;

            _velocity =
                Vector3.zero;

            _previousVelocity =
                Vector3.zero;

            _initialized = true;

            Debug.Log(
                $"PlaySenseAgent initialized: {name}");

            LogDetectedComponents();
        }

        // =====================================================
        // UPDATE
        // =====================================================

        private void Update()
        {
            if (!_initialized)
                return;

            CalculateVelocity();

            // Camera can sometimes be created
            // after the player during runtime.
            if (_camera == null)
                FindPlayerCamera();
        }

        // =====================================================
        // CAMERA DISCOVERY
        // =====================================================

        private void FindPlayerCamera()
        {
            // -------------------------------------------------
            // First: look for a camera on the player
            // or anywhere underneath the player hierarchy.
            // -------------------------------------------------

            Camera childCamera =
                GetComponentInChildren<Camera>();

            if (childCamera != null)
            {
                _camera = childCamera;
                return;
            }

            // -------------------------------------------------
            // Second: fall back to Unity's active camera.
            // -------------------------------------------------

            if (Camera.main != null)
            {
                _camera = Camera.main;
            }
        }

        // =====================================================
        // VELOCITY
        // =====================================================

        private void CalculateVelocity()
        {
            float deltaTime =
                Time.deltaTime;

            if (deltaTime <= 0f)
                return;

            _previousVelocity =
                _velocity;

            // -------------------------------------------------
            // CharacterController
            // -------------------------------------------------

            if (_characterController != null)
            {
                _velocity =
                    _characterController.velocity;

                _previousPosition =
                    transform.position;

                return;
            }

            // -------------------------------------------------
            // Rigidbody
            // -------------------------------------------------

            if (_rigidbody != null)
            {
                _velocity =
                    _rigidbody.linearVelocity;

                _previousPosition =
                    transform.position;

                return;
            }

            // -------------------------------------------------
            // Transform fallback
            // -------------------------------------------------

            Vector3 currentPosition =
                transform.position;

            _velocity =
                (currentPosition - _previousPosition)
                / deltaTime;

            _previousPosition =
                currentPosition;
        }

        // =====================================================
        // ACCELERATION
        // =====================================================

        private Vector3 CalculateAcceleration()
        {
            float deltaTime =
                Time.deltaTime;

            if (deltaTime <= 0f)
                return Vector3.zero;

            return
                (_velocity - _previousVelocity)
                / deltaTime;
        }

        // =====================================================
        // PERCEPTION
        // =====================================================

        public bool IsInView(
            Vector3 worldPosition,
            float maxDistance = 100f)
        {
            if (_camera == null)
                return false;

            Vector3 offset =
                worldPosition -
                _camera.transform.position;

            float distance =
                offset.magnitude;

            if (distance > maxDistance)
                return false;

            Vector3 viewportPoint =
                _camera.WorldToViewportPoint(
                    worldPosition);

            if (viewportPoint.z <= 0f)
                return false;

            return
                viewportPoint.x >= 0f &&
                viewportPoint.x <= 1f &&
                viewportPoint.y >= 0f &&
                viewportPoint.y <= 1f;
        }

        // =====================================================
        // DISTANCE FROM CAMERA
        // =====================================================

        public float DistanceFromCamera(
            Vector3 worldPosition)
        {
            if (_camera == null)
                return Vector3.Distance(
                    transform.position,
                    worldPosition);

            return Vector3.Distance(
                _camera.transform.position,
                worldPosition);
        }

        // =====================================================
        // VIEW DIRECTION
        // =====================================================

        public float AngleTo(
            Vector3 worldPosition)
        {
            Vector3 direction =
                worldPosition -
                CameraPosition;

            if (direction.sqrMagnitude <= 0.0001f)
                return 0f;

            return Vector3.Angle(
                ViewDirection,
                direction.normalized);
        }

        // =====================================================
        // MOVEMENT STATE
        // =====================================================

        public string GetMovementState()
        {
            if (!IsMoving)
                return "Idle";

            float acceleration =
                Acceleration.magnitude;

            if (acceleration > 3f)
                return "Accelerating";

            if (acceleration < 0.5f &&
                Speed > 0.05f)
                return "Moving";

            return "Changing Movement";
        }

        // =====================================================
        // DEBUG
        // =====================================================

        private void LogDetectedComponents()
        {
            if (_characterController != null)
            {
                Debug.Log(
                    $"{name}: CharacterController detected.");
            }
            else if (_rigidbody != null)
            {
                Debug.Log(
                    $"{name}: Rigidbody detected.");
            }
            else
            {
                Debug.Log(
                    $"{name}: No movement controller detected. " +
                    "Using Transform-based sensing.");
            }

            if (_camera != null)
            {
                Debug.Log(
                    $"{name}: Camera detected -> " +
                    $"{_camera.name}");
            }
            else
            {
                Debug.LogWarning(
                    $"{name}: No camera detected.");
            }
        }
    }
}