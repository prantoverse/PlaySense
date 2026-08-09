using UnityEngine;

namespace PlaySense.Perception
{
    public class PlaySenseView : MonoBehaviour
    {
        [Header("View")]
        [SerializeField]
        private bool activeView = true;

        [Header("Fallback Settings")]
        [SerializeField]
        private float fallbackFieldOfView = 90f;

        [SerializeField]
        private float fallbackNearClip = 0.05f;

        [SerializeField]
        private float fallbackFarClip = 100f;

        [Header("View Type")]
        [SerializeField]
        private ViewType viewType =
            ViewType.Gameplay;

        private Camera _camera;

        // =====================================================
        // PUBLIC PROPERTIES
        // =====================================================

        public bool IsActive =>
            activeView &&
            enabled &&
            gameObject.activeInHierarchy;

        public Vector3 Position =>
            transform.position;

        public Vector3 Forward =>
            transform.forward;

        public float FieldOfView
        {
            get
            {
                if (_camera != null)
                    return _camera.fieldOfView;

                return fallbackFieldOfView;
            }
        }

        public float NearClip
        {
            get
            {
                if (_camera != null)
                    return _camera.nearClipPlane;

                return fallbackNearClip;
            }
        }

        public float FarClip
        {
            get
            {
                if (_camera != null)
                    return _camera.farClipPlane;

                return fallbackFarClip;
            }
        }

        public ViewType Type =>
            viewType;

        public Camera Camera =>
            _camera;

        // =====================================================
        // INITIALIZATION
        // =====================================================

        private void Awake()
        {
            _camera =
                GetComponent<Camera>();

            if (_camera != null)
            {
                Debug.Log(
                    $"[PlaySense] View '{name}' " +
                    $"connected to Camera | " +
                    $"FOV: {_camera.fieldOfView:F1}°");
            }
            else
            {
                Debug.Log(
                    $"[PlaySense] View '{name}' " +
                    $"has no Camera component. " +
                    $"Using fallback view settings.");
            }
        }

        // =====================================================
        // VIEW TYPES
        // =====================================================

        public enum ViewType
        {
            Gameplay,
            ThirdPerson,
            FirstPerson,
            SideScroller,
            TopDown,
            VR,
            Cinematic
        }

        // =====================================================
        // DEBUG
        // =====================================================

        private void OnDrawGizmosSelected()
        {
            if (!activeView)
                return;

            Gizmos.color =
                Color.cyan;

            Gizmos.DrawRay(
                transform.position,
                transform.forward * 2f);
        }
    }
}