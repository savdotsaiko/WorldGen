using UnityEngine;

public class BodyOrienter : MonoBehaviour
{
    public Transform[] feetTargets = new Transform[4];
    public LegCaster[] legCasters = new LegCaster[4];
    public Transform movementRoot;
    public float bodyHeightOffset = 1.0f;
    public float heightCatchSpeed = 10f;
    public float rotationSmoothSpeed = 5f;
    [Header("Normal smoothing (damps corner/edge instability)")]
    public float normalSmoothSpeed = 6f;

    private Vector3 smoothedNormal = Vector3.up;
    public Vector3 CurrentSurfaceNormal => smoothedNormal; 

    void Update()
    {
        Vector3 avgPos = Vector3.zero;
        Vector3 rawAvgNormal = Vector3.zero;
        int count = 0;
        foreach (var legCast in legCasters)
        {
            //if (!legCast.grounded) return; // if any leg i
            rawAvgNormal += legCast.GroundNormal;
            count++;
        }
        if (count == 0) return;
        rawAvgNormal = (rawAvgNormal / count).normalized;

        smoothedNormal = Vector3.Slerp(smoothedNormal, rawAvgNormal, Time.deltaTime * normalSmoothSpeed).normalized;
        Vector3 avgNormal = smoothedNormal;

        foreach (Transform foot in feetTargets)
        {
            avgPos += foot.position;
        }
        avgPos /= feetTargets.Length;

        Vector3 desiredCenter = avgPos + avgNormal * bodyHeightOffset;
        Vector3 delta = desiredCenter - transform.position;
        Vector3 normalDelta = Vector3.Project(delta, avgNormal);
        transform.position = Vector3.Lerp(
            transform.position,
            transform.position + normalDelta,
            Time.deltaTime * heightCatchSpeed);

        Vector3 refForward = movementRoot.forward;
        Vector3 forwardFlat = Vector3.ProjectOnPlane(refForward, avgNormal);

        if (forwardFlat.sqrMagnitude < 0.001f)
        {
            float facingSign = Vector3.Dot(refForward, avgNormal);
            Vector3 verticalFallback = -Mathf.Sign(facingSign) * movementRoot.up;
            forwardFlat = Vector3.ProjectOnPlane(verticalFallback, avgNormal);
        }
        forwardFlat.Normalize();

        Quaternion desiredWorldRot = Quaternion.LookRotation(forwardFlat, avgNormal);
        Quaternion desiredLocalRot = Quaternion.Inverse(transform.parent.rotation) * desiredWorldRot;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, desiredLocalRot, Time.deltaTime * rotationSmoothSpeed);
        
    }
    
}