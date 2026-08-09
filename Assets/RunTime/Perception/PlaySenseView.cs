using UnityEngine;

namespace PlaySense.Perception
{
    public class PlaySenseView : MonoBehaviour
    {
        [Header("View")]
        [SerializeField]
        private bool activeView = true;

        [SerializeField]
        private float fieldOfView = 90f;

        [SerializeField]
        private float nearClip = 0.05f;

        [SerializeField]
        private float farClip = 100f;

        [Header("View Type")]
        [SerializeField]
        private ViewType viewType =
            ViewType.Gameplay;

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

        public float FieldOfView =>
            fieldOfView;

        public float NearClip =>
            nearClip;

        public float FarClip =>
            farClip;

        public ViewType Type =>
            viewType;

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