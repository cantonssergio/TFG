using UnityEngine;

public class Ship : MonoBehaviour
{
    public float speed = 5.0f;
    public float initialHoverTime = 5.0f;

    private Rigidbody rb;
    private float elapsedTime = 0.0f;
    private bool isMoving = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing on the Ship.");
            return;
        }

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        if (!isMoving && elapsedTime >= initialHoverTime)
        {
            isMoving = true;
            rb.useGravity = true;
        }
        if (isMoving)
        {
            rb.linearVelocity = new Vector3(speed, 0, 0);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<FluidInteractable>() != null)
        {
            Destroy(gameObject);
        }
    }
}
