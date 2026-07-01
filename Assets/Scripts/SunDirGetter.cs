using UnityEngine;

public class SunDirGetter : MonoBehaviour
{
    public Material skyBoxMat;
    public Gradient sunColorGradient;
    void Update()
    {
        skyBoxMat.SetVector("_SunDirection", -transform.forward);
        skyBoxMat.SetVector("_SunColor", GetComponent<Light>().color);
    }
}
