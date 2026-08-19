using UnityEngine;
public class LegCaster : MonoBehaviour
{
    public Transform movementRoot;
    public Transform body;
    public Vector3 localOffset;
    public LayerMask groundMask;
    public float rayStartHeight = 2f;
    public float rayDistance = 5f;

    public SpiderDriverController driverController;
    public float obstacleCheckDistance = 1.5f;
    public float obstacleCheckHeight = 0.5f;
    public LayerMask climbableObstacleMask;

    public bool grounded;
    [Header("Fallback fan search")]
    public int fanRayCount = 6;
    public float fanAngle = 45f; 

    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    private LegStepper _stepper;

    void Awake()
    {
        _stepper = GetComponent<LegStepper>();
    }
    void Update()
    {
        if (_stepper != null && _stepper.isStepping) return;
        Vector3 desiredPos = body.position + (body.rotation * localOffset);
        Vector3 origin = desiredPos + body.up * rayStartHeight;
        Vector3 castDir = -body.up;

        Debug.DrawLine(desiredPos, origin, Color.purple);
        Debug.DrawRay(origin, castDir * rayDistance, Color.red);


        Vector3 forwardOrigin = body.position + body.up * obstacleCheckHeight;
        RaycastHit obstacleHit;

        Vector3 moveDir = movementRoot.forward;

        if (driverController != null && driverController.enabled)
        {
            if (driverController.FB < 0)
                moveDir = -movementRoot.forward;
            else if (driverController.LR < 0)
                moveDir = -movementRoot.right;
            else if (driverController.LR > 0)
                moveDir = movementRoot.right;
        }


        bool climbableObstacleAhead =
    Physics.Raycast(forwardOrigin, moveDir, out obstacleHit,
                    obstacleCheckDistance, climbableObstacleMask);
        

        Debug.DrawRay(forwardOrigin, movementRoot.forward * obstacleCheckDistance, Color.white);
        Debug.DrawRay(forwardOrigin, -movementRoot.forward * obstacleCheckDistance, Color.white);

        RaycastHit bestHit = default;
        float bestDist = float.MaxValue;
        bool found = false;
        // 1. try the normal straight-down (relative to body) ray first, exactly as before
        if (Physics.Raycast(origin, castDir, out RaycastHit hit, rayDistance, groundMask))
        {
            transform.position = hit.point;
            GroundNormal = hit.normal;
            Debug.DrawRay(hit.point, hit.normal * 1f, Color.green);
            if (!climbableObstacleAhead)
                return;
        }

        // 2. main ray missed entirely (e.g. at a ledge/cliff edge) — fan out around it as a fallback
        for (int i = 0; i < fanRayCount; i++)
        {
            float t = fanRayCount > 1 ? (float)i / (fanRayCount - 1) : 0f;
            float angle = Mathf.Lerp(-fanAngle, fanAngle, t);
            Vector3 dir = Quaternion.AngleAxis(angle, body.right) * castDir;

            Debug.DrawRay(origin, dir * rayDistance, Color.cyan);

            if (Physics.Raycast(origin, dir, out RaycastHit fanHit, rayDistance, groundMask))
            {
                float d;

                if (climbableObstacleAhead)
                {
                    d = Vector3.Distance(fanHit.point, obstacleHit.point);
                }
                else
                {
                    // normal fallback behaviour
                    d = Vector3.Distance(fanHit.point, transform.position);
                }
                if (d < bestDist)
                {
                    bestDist = d;
                    bestHit = fanHit;
                    found = true;
                }
            }
        }

        if (found)
        {
            transform.position = bestHit.point;
            GroundNormal = bestHit.normal;
            Debug.DrawRay(bestHit.point, bestHit.normal * 1f, Color.purple);
        }
        
        // if nothing found at all, foot just stays exactly where it was last frame — no snapping, no falling through
    }

    
}