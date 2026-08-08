using UnityEngine;

public class MovementTracker : MonoBehaviour
{
    public static MovementTracker Instance;

    public float stopThreshold = 0.01f;   
    public float stopDelay = 0.5f;        

    private Vector3 lastPos;
    private float stillTimer = 0f;

    public bool IsMoving { get; private set; }
    public bool JustStopped { get; private set; } 

    void Awake()
    {
        Instance = this;
        lastPos = transform.position;
    }

    void Update()
    {
        float delta = Vector3.Distance(transform.position, lastPos);
        bool movingNow = delta > stopThreshold;

        JustStopped = false; 

        if (movingNow)
        {
            stillTimer = 0f;
            IsMoving = true;
        }
        else
        {
            if (IsMoving) 
            {
                stillTimer += Time.deltaTime;
                if (stillTimer >= stopDelay)
                {
                    JustStopped = true;
                    IsMoving = false;
                }
            }
        }

        lastPos = transform.position;
    }
}