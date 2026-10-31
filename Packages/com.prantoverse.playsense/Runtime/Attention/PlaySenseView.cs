using System.Collections.Generic;
using UnityEngine;

namespace PlaySense.Attention
{
    /// <summary>
    /// A point of view PlaySense uses to decide what the player can see.
    /// Put it on the gameplay camera. Without a Camera component the
    /// fallback field of view and clip distance are used.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlaySense/PlaySense View")]
    public class PlaySenseView : MonoBehaviour
    {
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

        [SerializeField]
        private bool activeView = true;

        [SerializeField]
        private ViewType viewType = ViewType.Gameplay;

        [Header("Fallback (used when there is no Camera on this object)")]
        [SerializeField]
        private float fallbackFieldOfView = 90f;

        [SerializeField]
        private float fallbackFarClip = 100f;

        private static readonly List<PlaySenseView> s_Active = new List<PlaySenseView>();

        private Camera _camera;

        /// <summary>All enabled views. Do not modify.</summary>
        public static List<PlaySenseView> ActiveViews
        {
            get { return s_Active; }
        }

        public bool IsActive
        {
            get { return activeView && isActiveAndEnabled; }
        }

        public ViewType Type
        {
            get { return viewType; }
        }

        public Camera Camera
        {
            get { return _camera; }
        }

        public Vector3 Position
        {
            get { return transform.position; }
        }

        public Vector3 Forward
        {
            get { return transform.forward; }
        }

        public float FarClip
        {
            get { return _camera != null ? _camera.farClipPlane : fallbackFarClip; }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void OnEnable()
        {
            if (!s_Active.Contains(this))
                s_Active.Add(this);
        }

        private void OnDisable()
        {
            s_Active.Remove(this);
        }

        /// <summary>
        /// True when the world point is inside this view's frustum
        /// (camera viewport, or the fallback cone without a camera).
        /// </summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            Vector3 offset = worldPoint - Position;
            float distance = offset.magnitude;

            if (distance <= 0.01f || distance > FarClip)
                return false;

            if (_camera != null)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(worldPoint);
                return viewport.z > 0f &&
                       viewport.x >= 0f && viewport.x <= 1f &&
                       viewport.y >= 0f && viewport.y <= 1f;
            }

            return Vector3.Angle(Forward, offset) <= fallbackFieldOfView * 0.5f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_Active.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            if (!activeView)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
