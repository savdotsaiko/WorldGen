using UnityEngine;

public class AudioStore : MonoBehaviour
{
    public static AudioStore Instance { get; private set; }

    [Header("Footsteps")]
    [SerializeField] private AudioClip[] grassFootStepAudios;
    [SerializeField] private AudioClip[] metalStepAudios;

    [Header("Other Audio")]
    [SerializeField] private AudioClip jumpScareAudio;
    [SerializeField] private AudioClip gunShootAudio;
    [SerializeField] private AudioClip heartBeat;

    public AudioClip[] GrassFootStepAudios => grassFootStepAudios;
    public AudioClip[] MetalStepAudios => metalStepAudios;
    public AudioClip JumpScareAudio => jumpScareAudio;
    public AudioClip GunShootAudio => gunShootAudio;
    public AudioClip HeartBeat => heartBeat;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}