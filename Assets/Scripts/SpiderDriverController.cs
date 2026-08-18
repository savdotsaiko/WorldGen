using UnityEngine;
using Unity.Cinemachine;

public class SpiderDriverController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float halfMoveSpeed;
    public float turnSpeed = 100f;
    public float mouseSensitivity = 3f;
    public LayerMask groundMask;
    public float rayDistance = 5f;
    public Transform movementRoot;
    public BodyOrienter bodyOrienter;
    public float pitchSensitivity = 3f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public bool invertY = false;
    public CinemachineCamera virtualCamera;

    [Header("Gizmo settings")]
    public float gizmoLength = 2f;

    private float pitchAngle = 0f;
    private float yawAngle = 0f;
    private Vector3 lastNormal = Vector3.up;
    private Vector3 lastSlopeForward = Vector3.forward;
    public float FB, LR;
    private float turnAngle = 0f;
    private Vector3 trackedTangent;
    private Vector3 lastTurnAxis;
    private bool hasLastTurnAxis = false;

    [Header("Obstacle blocking")]
    public LayerMask obstacleMask;
    public float obstacleCheckDistance = 1f;
    public float obstacleCheckHeight = 0.5f;

    private void Start()
    {
        halfMoveSpeed = moveSpeed * 0.7071f; 
        
    }
    bool IsBlocked(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.0001f) return false;
        Vector3 origin = transform.position + movementRoot.up * obstacleCheckHeight;
        return Physics.Raycast(origin, moveDir, obstacleCheckDistance, obstacleMask);
    }

    void Update()
    {
        LR = Input.GetAxis("Horizontal");
        FB = Input.GetAxis("Vertical");

        Vector3 desiredDir = (movementRoot.forward * FB + movementRoot.right * LR);
        if (desiredDir.sqrMagnitude > 0.0001f && IsBlocked(desiredDir.normalized))
        {
            FB = 0;
            LR = 0;
        }

        Vector3 turnAxis = bodyOrienter != null ? bodyOrienter.CurrentSurfaceNormal : Vector3.up;

        if (!hasLastTurnAxis)
        {
            trackedTangent = Vector3.ProjectOnPlane(movementRoot.forward, turnAxis);
            if (trackedTangent.sqrMagnitude < 0.001f)
                trackedTangent = Vector3.ProjectOnPlane(Vector3.right, turnAxis);
            trackedTangent.Normalize();
            lastTurnAxis = turnAxis;
            hasLastTurnAxis = true;
        }
        else if (turnAxis != lastTurnAxis)
        {
            Quaternion transport = Quaternion.FromToRotation(lastTurnAxis, turnAxis);
            trackedTangent = transport * trackedTangent;
            trackedTangent = Vector3.ProjectOnPlane(trackedTangent, turnAxis).normalized;
            lastTurnAxis = turnAxis;
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        turnAngle += mouseX * mouseSensitivity;

        float pitchInput = invertY ? mouseY : -mouseY;
        pitchAngle = Mathf.Clamp(pitchAngle + pitchInput * pitchSensitivity, minPitch, maxPitch);

        Vector3 newForward = Quaternion.AngleAxis(turnAngle, turnAxis) * trackedTangent;
        movementRoot.rotation = Quaternion.LookRotation(newForward, turnAxis);

        Vector3 normal = Vector3.up;
        Vector3 origin = transform.position + transform.up * 2f;
        if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, rayDistance, groundMask))
        {
            normal = hit.normal;
        }

        Vector3 forward = movementRoot.transform.forward;
        Vector3 right = movementRoot.transform.right;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;

        float moveSpeedtoApply = moveSpeed;
        if (LR != 0 && FB != 0) moveSpeedtoApply = halfMoveSpeed; else moveSpeedtoApply = moveSpeed;
        transform.position += forward * FB * moveSpeedtoApply * Time.deltaTime;
        transform.position += right * LR * moveSpeedtoApply * Time.deltaTime;

        lastNormal = normal;
        lastSlopeForward = forward;
    }
    public void SnapToGround()
    {
        Debug.Log("SNAPPING");
        GetComponentInChildren<BodyOrienter>().enabled = false;
        Vector3 rayOrigin = transform.position + Vector3.up * 50000f;
        var rayDown = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitDown, Mathf.Infinity,
            LayerMask.GetMask("Ground"));
        var rayUp = Physics.Raycast(rayOrigin, Vector3.up, out RaycastHit hitUp, Mathf.Infinity,
            LayerMask.GetMask("Ground"));

        Vector3 pos = transform.position;
        if (rayDown)
        {
            Debug.Log(hitDown.collider.gameObject.name);
            pos.y = hitDown.point.y;
        }
        else if (rayUp)
        {
            Debug.Log("Hit up at: " + hitUp.point);
            pos.y = hitUp.point.y;
        }
        transform.position = pos;
        GetComponentInChildren<BodyOrienter>().enabled = true;

    }
    void LateUpdate()
    {
        Quaternion yaw = Quaternion.LookRotation(movementRoot.forward, movementRoot.up);
        Quaternion pitch = Quaternion.AngleAxis(pitchAngle, movementRoot.right);
        if (virtualCamera == null) return;
        virtualCamera.transform.rotation = pitch * yaw;
    }

    void OnDrawGizmos()
    {
        Vector3 pos = transform.position;
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(pos, pos + transform.forward * gizmoLength);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(pos, pos + lastNormal * gizmoLength);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(pos, pos + lastSlopeForward * gizmoLength);
        Gizmos.DrawSphere(pos + lastSlopeForward * gizmoLength, 0.1f);
    }
}