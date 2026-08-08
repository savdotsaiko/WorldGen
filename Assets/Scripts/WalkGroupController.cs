using UnityEngine;

public class WalkGroupController : MonoBehaviour
{
    public static WalkGroupController Instance;

    private int[] steppingCount = new int[2]; 

    void Awake()
    {
        Instance = this;
    }

    public bool CanStep(int group)
    {
        int otherGroup = 1 - group;
        return steppingCount[otherGroup] == 0; 
    }

    public void RegisterStepStart(int group)
    {
        steppingCount[group]++;
    }

    public void RegisterStepEnd(int group)
    {
        steppingCount[group]--;
    }
}