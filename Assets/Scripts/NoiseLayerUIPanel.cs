using UnityEngine;

public class NoiseLayerUIPanel : MonoBehaviour
{
    public NoiseVisualizerDemo controller;

    [Tooltip("0 = Continentalness, 1 = Erosion, 2 = PeaksValleys - must match controller.layers order")]
    public int layerIndex;

    public void OnScaleChanged(float v) => controller.SetLayerScale(layerIndex, v);
    public void OnOctavesChanged(float v) => controller.SetLayerOctaves(layerIndex, v);
    public void OnPersistenceChanged(float v) => controller.SetLayerPersistence(layerIndex, v);
    public void OnLacunarityChanged(float v) => controller.SetLayerLacunarity(layerIndex, v);
    public void OnEnabledChanged(bool v) => controller.SetLayerEnabled(layerIndex, v);
}