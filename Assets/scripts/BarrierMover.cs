using UnityEngine;

public class BarrierMover : MonoBehaviour
{
    [Header("Barrier References")]
    public GameObject leftBarrier;
    public GameObject rightBarrier;
    public GameObject lowerLeftBarrier;
    public GameObject lowerRightBarrier;

    [Header("Movement Settings")]
    public float moveSpeed = 5.0f;
    public float returnSpeed = 2.0f;
    public float epsilon = 0.01f;
    public Vector2 leftBarrierLimits = new Vector2(-10.0f, -9.5f);
    public Vector2 rightBarrierLimits = new Vector2(9.5f, 10.0f);
    public Vector2 lowerLeftBarrierLimits = new Vector2(-2, -1.5f);
    public Vector2 lowerRightBarrierLimits = new Vector2(-2, 1.5f);

    private Rigidbody leftRigidbody;
    private Rigidbody rightRigidbody;
    private Rigidbody lowerLeftRigidbody;
    private Rigidbody lowerRightRigidbody;

    private Vector3 leftInitialPosition;
    private Vector3 rightInitialPosition;
    private Vector3 lowerLeftInitialPosition;
    private Vector3 lowerRightInitialPosition;

    private bool isLeftMoving = false;
    private bool isRightMoving = false;
    private bool isLowerLeftMoving = false;
    private bool isLowerRightMoving = false;

    void Start()
    {
        leftRigidbody = leftBarrier.GetComponent<Rigidbody>();
        rightRigidbody = rightBarrier.GetComponent<Rigidbody>();
        lowerLeftRigidbody = lowerLeftBarrier.GetComponent<Rigidbody>();
        lowerRightRigidbody = lowerRightBarrier.GetComponent<Rigidbody>();

        if (leftRigidbody == null || rightRigidbody == null || lowerLeftRigidbody == null || lowerRightRigidbody == null)
        {
            Debug.LogError("All barriers must have a Rigidbody component.");
            return;
        }

        leftRigidbody.isKinematic = false;
        rightRigidbody.isKinematic = false;
        lowerLeftRigidbody.isKinematic = false;
        lowerRightRigidbody.isKinematic = false;

        leftRigidbody.useGravity = false;
        rightRigidbody.useGravity = false;
        lowerLeftRigidbody.useGravity = false;
        lowerRightRigidbody.useGravity = false;

        leftInitialPosition = leftRigidbody.position;
        rightInitialPosition = rightRigidbody.position;
        lowerLeftInitialPosition = lowerLeftRigidbody.position;
        lowerRightInitialPosition = lowerRightRigidbody.position;
    }

    void Update()
    {
        HandleBarrierMovement();

        ClampBarrierPosition(leftRigidbody, leftBarrierLimits, true);
        ClampBarrierPosition(rightRigidbody, rightBarrierLimits, true);
        ClampBarrierPosition(lowerLeftRigidbody, lowerLeftBarrierLimits, false);
        ClampBarrierPosition(lowerRightRigidbody, lowerRightBarrierLimits, false);

        ReturnToInitialPosition(leftRigidbody, leftInitialPosition, ref isLeftMoving, leftBarrierLimits, true, true);
        ReturnToInitialPosition(rightRigidbody, rightInitialPosition, ref isRightMoving, rightBarrierLimits, true, false);
        ReturnToInitialPosition(lowerLeftRigidbody, lowerLeftInitialPosition, ref isLowerLeftMoving, lowerLeftBarrierLimits, false, false);
        ReturnToInitialPosition(lowerRightRigidbody, lowerRightInitialPosition, ref isLowerRightMoving, lowerRightBarrierLimits, false, false);
    }

    private void HandleBarrierMovement()
    {
        float leftInput = Input.GetKey(KeyCode.A) ? -1 : (Input.GetKey(KeyCode.D) ? 1 : 0);
        if (leftInput != 0)
        {
            leftRigidbody.linearVelocity = new Vector3(leftInput * moveSpeed, 0, 0);
            isLeftMoving = true;
        }
        else
        {
            isLeftMoving = false;
        }

        float rightInput = Input.GetKey(KeyCode.J) ? -1 : (Input.GetKey(KeyCode.L) ? 1 : 0);
        if (rightInput != 0)
        {
            rightRigidbody.linearVelocity = new Vector3(rightInput * moveSpeed, 0, 0);
            isRightMoving = true;
        }
        else
        {
            isRightMoving = false;
        }

        float lowerLeftInput = Input.GetKey(KeyCode.W) ? 1 : (Input.GetKey(KeyCode.S) ? -1 : 0);
        if (lowerLeftInput != 0)
        {
            lowerLeftRigidbody.linearVelocity = new Vector3(0, lowerLeftInput * moveSpeed, 0);
            isLowerLeftMoving = true;
        }
        else
        {
            isLowerLeftMoving = false;
        }

        float lowerRightInput = Input.GetKey(KeyCode.I) ? 1 : (Input.GetKey(KeyCode.K) ? -1 : 0);
        if (lowerRightInput != 0)
        {
            lowerRightRigidbody.linearVelocity = new Vector3(0, lowerRightInput * moveSpeed, 0);
            isLowerRightMoving = true;
        }
        else
        {
            isLowerRightMoving = false;
        }
    }

    private void ClampBarrierPosition(Rigidbody rb, Vector2 limits, bool horizontal)
    {
        Vector3 pos = rb.position;
        Vector3 velocity = rb.linearVelocity;

        if (horizontal)
        {
            if (pos.x <= limits.x && velocity.x < 0)
            {
                velocity.x = 0;
            }
            else if (pos.x >= limits.y && velocity.x > 0)
            {
                velocity.x = 0;
            }
        }
        else
        {
            if (pos.y <= limits.x && velocity.y < 0)
            {
                velocity.y = 0;
            }
            else if (pos.y >= limits.y && velocity.y > 0)
            {
                velocity.y = 0;
            }
        }
        rb.linearVelocity = velocity;
    }

    private void ReturnToInitialPosition(Rigidbody rb, Vector3 initialPosition, ref bool isMoving, Vector2 limits, bool horizontal, bool checkMaxLimit)
    {
        if (isMoving) return;

        Vector3 pos = rb.position;

        if (horizontal)
        {
            if ((checkMaxLimit && pos.x < limits.y) || (!checkMaxLimit && pos.x > limits.x)) return;
        }
        else
        {
            if ((checkMaxLimit && pos.y > limits.x) || (!checkMaxLimit && pos.y < limits.y)) return;
        }

        float distance = Vector3.Distance(initialPosition, pos);
        if (distance > epsilon)
        {
            Vector3 direction = (initialPosition - pos).normalized;
            rb.linearVelocity = direction * returnSpeed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            rb.position = initialPosition;
        }
    }
}
