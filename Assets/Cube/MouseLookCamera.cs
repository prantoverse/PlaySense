using UnityEngine;

public class MouseLookCamera : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField]
    private float sensitivity = 3f;

    [SerializeField]
    private float maxVerticalAngle = 80f;

    [Header("Cursor")]
    [SerializeField]
    private bool lockCursor = true;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 rotation =
            transform.eulerAngles;

        yaw = rotation.y;
        pitch = rotation.x;

        if (lockCursor)
        {
            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }
    }

    private void Update()
    {
        float mouseX =
            Input.GetAxis("Mouse X") *
            sensitivity;

        float mouseY =
            Input.GetAxis("Mouse Y") *
            sensitivity;

        yaw += mouseX;

        pitch -= mouseY;

        pitch =
            Mathf.Clamp(
                pitch,
                -maxVerticalAngle,
                maxVerticalAngle);

        transform.rotation =
            Quaternion.Euler(
                pitch,
                yaw,
                0f);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState =
                CursorLockMode.None;

            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0))
        {
            Cursor.lockState =
                CursorLockMode.Locked;

            Cursor.visible = false;
        }
    }
}