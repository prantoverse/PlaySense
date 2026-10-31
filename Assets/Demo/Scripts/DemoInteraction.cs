using PlaySense;
using PlaySense.Attention;
using UnityEngine;

/// <summary>
/// Demo only: press E (or click) while looking at something to "inspect" it.
/// Shows how a game logs events with PlaySenseEvents.Log, including metadata
/// that becomes a breakdown in the Dashboard's All Sessions view.
/// </summary>
public class DemoInteraction : MonoBehaviour
{
    [SerializeField]
    private float reach = 6f;

    private void Update()
    {
        bool key = Input.GetKeyDown(KeyCode.E);
        bool click = Input.GetMouseButtonDown(1);
        if (!key && !click)
            return;

        Camera view = Camera.main;
        if (view == null)
            return;

        string input = key ? "keyboard" : "mouse";

        RaycastHit hit;
        if (Physics.Raycast(view.transform.position, view.transform.forward, out hit, reach))
        {
            PlaySenseTarget target = hit.collider.GetComponentInParent<PlaySenseTarget>();
            string category = target != null && !string.IsNullOrEmpty(target.Category) ? target.Category : "Other";

            GameObject inspected = target != null ? target.gameObject : hit.collider.gameObject;
            PlaySenseEvents.Log("Interaction", "Inspect", inspected, "input", input, "kind", category);
        }
        else
        {
            PlaySenseEvents.LogWithData("Interaction", "InspectNothing", null, "input", input);
        }
    }
}
