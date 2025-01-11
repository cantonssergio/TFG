using UnityEngine;

public class Ship : MonoBehaviour
{
    public float speed = 5.0f;
    public float initialHoverTime = 5.0f;

    private Rigidbody rb;
    private float elapsedTime = 0.0f;
    private Vector3 initialPosition;
    private Quaternion initialRotation;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing on the Ship.");
            return;
        }
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.freezeRotation = true;

    }

    void Update()
    {
        elapsedTime += Time.deltaTime;

        if (elapsedTime >= initialHoverTime)
        {
            rb.useGravity = true;
            rb.freezeRotation = false;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezePositionZ;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetShip();
        }
    }
    private void ResetShip()
    {
        // Restaura la posición inicial
        transform.position = initialPosition;

        // Restaura la rotación inicial
        transform.rotation = initialRotation;

        // Reinicia el tiempo transcurrido
        elapsedTime = 0.0f;

        // Desactiva la gravedad y velocidades
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero; // Velocidad lineal a 0
        rb.angularVelocity = Vector3.zero; // Velocidad angular a 0

        // Bloquea la rotación mientras está en espera
        rb.freezeRotation = true;
    }

}
