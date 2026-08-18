using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;


public class SpiderFormation : MonoBehaviour
{
    public Transform playerLookat;

    [Header("Game Flow")]
    public float huntModeDelay = 30f;
    public float startFollowDistance;
    public float huntFollowDistance;
    public TMP_Text hideBigtxt;
    public TMP_Text huntTimer;
    public TMP_Text huntingBigtxt;
    public int numOfSpiders = 4;
    int spidersLeft;
    [SerializeField] SpiderDriverController spiderPlayer;

    [Header("Win Condition")]
    public float catchDistance = 2f;
    public TMP_Text winText;

    [Header("Win Effect")]
    public Material psxMaterial;
    public CinemachineCamera virtualCamera;
    public Camera mainCam;
    public SpiderDriverController controller;

    private bool _gameOver = false;
    public static SpiderFormation Instance { get; private set; }
    public bool huntMode { get; private set; }

    private List<SpiderAI> _pool = new();
    private List<SpiderAI> _active = new();
    private float _timer = 0f;
    private EndlessWorld _endlessWorld;

    private bool initialized = false;
    private AudioSource audioSrc;
    private bool isSnapping = false;

    void Awake()
    {
        initialized = false;
        _endlessWorld = Object.FindFirstObjectByType<EndlessWorld>();
        psxMaterial.SetFloat("_Saturation", 1f);
        Instance = this;
        if (GetComponent<AudioSource>() != null)
        {
            audioSrc = GetComponent<AudioSource>();
        }
        else { audioSrc = gameObject.AddComponent<AudioSource>(); }
    }
    private int _lastSecond;
    private void Start()
    {
        spidersLeft = numOfSpiders;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        UpdateUI();


        if (_timer < huntModeDelay)
        {
            int currentSecond = Mathf.FloorToInt(_timer);

            if (currentSecond > _lastSecond)
            {
                _lastSecond = currentSecond;
                audioSrc.PlayOneShot(AudioStore.Instance.HeartBeat);
            }
            huntMode = false;
            foreach (var spider in _active)
            {
                spider.followDistance = startFollowDistance;
                spider.DoNormalFollow();
                spider.SnapToGround();
            }
        }
        else
        {
            huntMode = true;
            foreach (var spider in _active)
            {
                spider.followDistance = huntFollowDistance;
                spider.DoHuntMode();
                spider.SnapToGround();
            }
            CheckWinCondition();
        }
    }
    public void SnapAllSpiders()
    {
        StartCoroutine(SnapSpiders());
    }
    IEnumerator SnapSpiders()
    {
        if (isSnapping) yield break;
        isSnapping = true;

        while (true)
        {
            Vector2 currentPlayerXZ = new Vector2(spiderPlayer.transform.position.x, spiderPlayer.transform.position.z);
            if (_endlessWorld.IsChunkReadyAt(currentPlayerXZ))
                break;
            yield return null;
        }

        if (spiderPlayer != null) spiderPlayer.SnapToGround();
        foreach (var spider in _active) spider.SnapToGround();

        isSnapping = false;
    }

    void CheckWinCondition()
    {
        if (_gameOver || !huntMode) return;

        SpiderAI winner = null;
        float closestDist = float.MaxValue;

        foreach (var spider in _active)
        {
            float dist = Vector3.Distance(spider.transform.position, spider.player.position);
            if (dist <= catchDistance && dist < closestDist)
            {
                closestDist = dist;
                winner = spider;
            }
        }

        if (winner != null)
            TriggerSpiderWin(winner);
    }

    void TriggerSpiderWin(SpiderAI winner)
    {
        winner.stopFollowing = true;
        _gameOver = true;

        if (virtualCamera != null)
        {
            virtualCamera.LookAt = winner.transform;
            virtualCamera.Follow = winner.transform;
            virtualCamera.transform.rotation = winner.transform.rotation;
            winner.GetComponent<AudioSource>().PlayOneShot(AudioStore.Instance.JumpScareAudio);
        }

        if (winText != null)
        {
            winText.text = "YOU LOSE";
            winText.gameObject.SetActive(true);
        }
        StartCoroutine(SpiderWinSequence(winner));
    }

    IEnumerator SpiderWinSequence(SpiderAI winner)
    {
        controller.enabled = false;

        float blinkDuration = 3f;
        float blinkInterval = 0.15f;
        float blinkElapsed = 0f;
        bool redOn = true;

        while (blinkElapsed < blinkDuration)
        {
            blinkElapsed += blinkInterval;
            redOn = !redOn;
            winner.SetWinColor(redOn);
            yield return new WaitForSecondsRealtime(blinkInterval);
        }

        winner.SetWinColor(true);

        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (psxMaterial != null)
                psxMaterial.SetFloat("_Saturation", Mathf.Lerp(0f, -1f, elapsed / duration));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);
        RestartRound();
    }
    void TriggerPlayerWin()
    {
        _gameOver = true;


        if (winText != null)
        {
            winText.text = "YOU WIN";
            winText.gameObject.SetActive(true);
        }
        StartCoroutine(PlayerWinSequence());
    }

    IEnumerator PlayerWinSequence()
    {
        yield return new WaitForSecondsRealtime(1f);

        controller.enabled = true;
        float elapsed = 0f;
        float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (psxMaterial != null)
                psxMaterial.SetFloat("_Saturation", Mathf.Lerp(0f, -1f, elapsed / duration));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1f);
        RestartRound();
    }

    public void RestartRound()
    {
        spidersLeft = numOfSpiders;
        _lastSecond = 0;
        controller.enabled = true;
        _gameOver = false;
        _timer = 0f;
        huntMode = false;

        if (psxMaterial != null)
            psxMaterial.SetFloat("_Saturation", 1f);

        if (virtualCamera != null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                virtualCamera.LookAt = player.transform;
                virtualCamera.Follow = player.transform;
                mainCam.transform.rotation = player.transform.rotation;
            }
        }

        if (winText != null)
            winText.gameObject.SetActive(false);

        foreach (var spider in _pool)
        {
            spider.SetWinColor(false);
            spider.stopFollowing = false;
            spider.Revive();
        }

        AssignSlots();
    }
    public void Register(SpiderAI spider)
    {
        if (!_pool.Contains(spider))
            _pool.Add(spider);

        if (!_active.Contains(spider))
        {
            _active.Add(spider);
            spider.followDistance = startFollowDistance;
        }

        AssignSlots();
    }

    public void Unregister(SpiderAI spider)
    {
        if (_active.Contains(spider))
        {
            spidersLeft--;
            _active.Remove(spider);
        }
        if (spidersLeft <= 0)
        {
            TriggerPlayerWin();
        }
        AssignSlots();
    }

    public SpiderAI GetFromPool()
    {
        foreach (var spider in _pool)
        {
            if (!_active.Contains(spider))
            {
                spider.gameObject.SetActive(true);
                spider.Revive();
                return spider;
            }
        }
        return null;
    }

    void AssignSlots()
    {
        int count = _active.Count;
        for (int i = 0; i < count; i++)
        {
            _active[i].AssignSlot((360f / count) * i);
        }
    }

    void UpdateUI()
    {
        if (huntTimer == null || hideBigtxt == null || huntingBigtxt == null) return;

        float remaining = huntModeDelay - _timer;

        if (remaining >= 28f)
        {
            huntTimer.text = "";
            hideBigtxt.gameObject.SetActive(true);
        }
        else if (remaining >= 3f)
        {
            hideBigtxt.gameObject.SetActive(false);
            huntTimer.text = "hunt begins in " + Mathf.CeilToInt(remaining);
        }
        else if (remaining >= 0f)
        {
            huntTimer.text = "";
            huntingBigtxt.gameObject.SetActive(true);
            huntingBigtxt.text = "HUNT BEGINS\nIN " + Mathf.CeilToInt(remaining);
        }
        else
        {
            huntingBigtxt.gameObject.SetActive(false);
            hideBigtxt.gameObject.SetActive(false);
            huntTimer.text = "";
        }
    }
}