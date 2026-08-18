using UnityEngine;
using System.Collections;

public class LegStepper : MonoBehaviour
{
    public float moveThreshold = 5f;
    public Transform moveDistanceMarker;
    public float stepDuration = 0.2f;
    public float stepHeight = 0.5f;
    public int groupID = 0;
    public bool isStepping = false;
    private bool pendingSettle = false;
    public WalkGroupController _walkGroup;

    void Awake()
    {
    }

    void Update()
    {
        if (_walkGroup == null) return;
        if (isStepping) return;

        float distanceToMarker = Vector3.Distance(transform.position, moveDistanceMarker.position);
        bool needsNormalStep = distanceToMarker > moveThreshold;

        if (MovementTracker.Instance.JustStopped && distanceToMarker > 0.0001f)
            pendingSettle = true;

        if (distanceToMarker > moveThreshold * 2.5f)
        {
            StartCoroutine(StepTo(moveDistanceMarker.position));
            pendingSettle = false;
            return;
        }

        if ((needsNormalStep || pendingSettle) && _walkGroup.CanStep(groupID))
        {
            StartCoroutine(StepTo(moveDistanceMarker.position));
            pendingSettle = false;
        }
    }

    IEnumerator StepTo(Vector3 targetPos)
    {
        isStepping = true;
        _walkGroup.RegisterStepStart(groupID);
        //GetComponent<AudioSource>().PlayOneShot(AudioStore.Instance.MetalStepAudios[Random.Range(0, AudioStore.Instance.MetalStepAudios.Length)]);
        GetComponent<AudioSource>().PlayOneShot(AudioStore.Instance.GrassFootStepAudios[Random.Range(0, AudioStore.Instance.GrassFootStepAudios.Length)]);

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < stepDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / stepDuration;

            Vector3 flatPos = Vector3.Lerp(startPos, targetPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * stepHeight;
            flatPos.y += arc;

            transform.position = flatPos;
            yield return null;
        }

        transform.position = targetPos;
        isStepping = false;
        _walkGroup.RegisterStepEnd(groupID);
    }
}