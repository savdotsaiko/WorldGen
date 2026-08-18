using UnityEngine;

public class WalkGroupController : MonoBehaviour
{
    private int[] steppingCount = new int[2];

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