using System.Text;
using UnityEngine;

namespace PlaySense.Perception
{
    /// <summary>
    /// Marks an object PlaySense should track attention on (a door, a clue,
    /// an enemy). It needs a collider on itself or a child.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("PlaySense/PlaySense Target")]
    public class PlaySenseTarget : MonoBehaviour
    {
        [Tooltip("Name shown in reports. Leave empty to use the GameObject name.")]
        [SerializeField]
        private string displayName = string.Empty;

        [Tooltip("Optional grouping for reports, e.g. Clue, Door, Enemy.")]
        [SerializeField]
        private string category = string.Empty;

        [Tooltip("Point the player has to see. Leave empty to use the centre of the colliders.")]
        [SerializeField]
        private Transform aimPoint;

        private string _id;

        public string DisplayName
        {
            get { return string.IsNullOrEmpty(displayName) ? name : displayName; }
        }

        public string Category
        {
            get { return category; }
        }

        public Transform AimPoint
        {
            get { return aimPoint; }
        }

        /// <summary>Stable id: the hierarchy path, so two objects named "Crate" stay distinct.</summary>
        public string Id
        {
            get
            {
                if (string.IsNullOrEmpty(_id))
                    _id = GetHierarchyPath(transform);

                return _id;
            }
        }

        private void OnTransformParentChanged()
        {
            _id = null;
        }

        public static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
                return string.Empty;

            StringBuilder builder = new StringBuilder(transform.name);
            Transform parent = transform.parent;

            while (parent != null)
            {
                builder.Insert(0, '/');
                builder.Insert(0, parent.name);
                parent = parent.parent;
            }

            return builder.ToString();
        }
    }
}
