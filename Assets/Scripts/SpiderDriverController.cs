using UnityEngine;
public class SpiderDriverController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float turnSpeed = 100f;
    public float mouseSensitivity = 3f;
    public LayerMask groundMask;
    public float rayDistance = 5f;
    public Transform movementRoot;
    public BodyOrienter bodyOrienter;

    [Header("Look pitch (camera only, doesn't affect movementRoot)")]
    public Transform cameraPitchTransform; // child of movementRoot - put your actual Camera under this
    public float pitchSensitivity = 3f;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public bool invertY = false;
    private float pitchAngle = 0f;

    [Header("Gizmo settings")]
    public float gizmoLength = 2f;
    private Vector3 lastNormal = Vector3.up;
    private Vector3 lastSlopeForward = Vector3.forward;
    public float FB, LR;

    private float turnAngle = 0f;
    private Vector3 trackedTangent;
    private Vector3 lastTurnAxis;
    private bool hasLastTurnAxis = false;

    void Update()
    {
        LR = Input.GetAxis("Horizontal");
        FB = Input.GetAxis("Vertical");

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

        Vector3 newForward = Quaternion.AngleAxis(turnAngle, turnAxis) * trackedTangent;
        movementRoot.rotation = Quaternion.LookRotation(newForward, turnAxis);

        // --- pitch: local to movementRoot, never touches its forward/up ---
        float pitchInput = invertY ? mouseY : -mouseY;
        pitchAngle = Mathf.Clamp(pitchAngle + pitchInput * pitchSensitivity, minPitch, maxPitch);
        if (cameraPitchTransform != null)
        {
            cameraPitchTransform.localRotation = Quaternion.AngleAxis(pitchAngle, Vector3.right);
        }

        Vector3 normal = Vector3.up;
        Vector3 origin = transform.position + transform.up * 2f;
        if (Physics.Raycast(origin, -transform.up, out RaycastHit hit, rayDistance, groundMask))
        {
            normal = hit.normal;
        }
        Vector3 forward = movementRoot.transform.forward;
        Vector3 right = movementRoot.transform.right;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        transform.position += forward * FB * moveSpeed * Time.deltaTime;
        transform.position += right * LR * moveSpeed * Time.deltaTime;
        lastNormal = normal;
        lastSlopeForward = forward;
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