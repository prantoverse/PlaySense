using PlaySense;
using PlaySense.Perception;
using UnityEngine;

/// <summary>
/// Demo only: press E while looking at something to "inspect" it.
/// Shows how a game logs events with PlaySenseEvents.Log.
/// </summary>
public class DemoInteraction : MonoBehaviour
{
    [SerializeField]
    private float reach = 6f;

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.E))
            return;

        Camera view = Camera.main;
        if (view == null)
            return;

        RaycastHit hit;
        if (Physics.Raycast(view.transform.position, view.transform.forward, out hit, reach))
        {
            PlaySenseTarget target = hit.collider.GetComponentInParent<PlaySenseTarget>();
            string targetName = target != null ? target.DisplayName : hit.collider.name;

            PlaySenseEvents.Log("Interaction", "Inspect", targetName, hit.point);
        }
        else
        {
            PlaySenseEvents.Log("Interaction", "InspectNothing", null, transform.position);
        }
    }
}
