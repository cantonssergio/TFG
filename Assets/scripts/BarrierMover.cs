using UnityEngine;

public class BarrierMover : MonoBehaviour
{
    [Header("Barrier References")]
    public GameObject leftBarrier;  // Referencia al prisma izquierdo
    public GameObject rightBarrier; // Referencia al prisma derecho

    [Header("Movement Settings")]
    public float moveSpeed = 5.0f; // Velocidad de movimiento de las barreras
    public Vector2 leftBarrierLimits = new Vector2(-5.0f, 0.0f); // Límites del prisma izquierdo
    public Vector2 rightBarrierLimits = new Vector2(0.0f, 5.0f); // Límites del prisma derecho

    private Rigidbody leftRigidbody;
    private Rigidbody rightRigidbody;

    void Start()
    {
        // Obtén los Rigidbody de las barreras
        leftRigidbody = leftBarrier.GetComponent<Rigidbody>();
        rightRigidbody = rightBarrier.GetComponent<Rigidbody>();

        // Verifica que las barreras tengan Rigidbody
        if (leftRigidbody == null || rightRigidbody == null)
        {
            Debug.LogError("Both barriers must have a Rigidbody component.");
            return;
        }

        // Configura los Rigidbody en modo dinámico
        leftRigidbody.isKinematic = false;
        rightRigidbody.isKinematic = false;

        // Asegúrate de que no tengan gravedad si no es necesaria
        leftRigidbody.useGravity = false;
        rightRigidbody.useGravity = false;
    }

    void Update()
    {
        // Mueve las barreras usando las entradas del teclado
        HandleBarrierMovement();

        // Limita las posiciones dentro de los límites
        ClampBarrierPosition(leftRigidbody, leftBarrierLimits);
        ClampBarrierPosition(rightRigidbody, rightBarrierLimits);
    }

    private void HandleBarrierMovement()
    {
        // Movimiento de la barrera izquierda (teclas A y D)
        float leftInput = Input.GetKey(KeyCode.A) ? -1 : (Input.GetKey(KeyCode.D) ? 1 : 0);
        leftRigidbody.linearVelocity = new Vector3(leftInput * moveSpeed, 0, 0);

        // Movimiento de la barrera derecha (teclas J y L)
        float rightInput = Input.GetKey(KeyCode.J) ? -1 : (Input.GetKey(KeyCode.L) ? 1 : 0);
        rightRigidbody.linearVelocity = new Vector3(rightInput * moveSpeed, 0, 0);
    }

    private void ClampBarrierPosition(Rigidbody rb, Vector2 limits)
    {
        Vector3 pos = rb.position;

        // Limita la posición dentro de los límites
        if (pos.x < limits.x)
        {
            pos.x = limits.x;
            rb.position = pos;
            rb.linearVelocity = Vector3.zero; // Detén el movimiento si alcanza el límite
        }
        else if (pos.x > limits.y)
        {
            pos.x = limits.y;
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
        }
    }
}
