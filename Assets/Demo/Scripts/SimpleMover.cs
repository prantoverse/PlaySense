using UnityEngine;

public class SimpleMover : MonoBehaviour
{
    public float speed = 5f;

    void Update()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        transform.Translate(
            new Vector3(h, 0f, v) * speed * Time.deltaTime);
    }
}