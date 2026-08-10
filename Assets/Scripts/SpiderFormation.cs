using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Threading;

public class SpiderFormation : MonoBehaviour
{

    [Header("Game Flow")]
    public float huntModeDelay = 30f;
    public float startFollowDistance;
    public float huntFollowDistance = 0f;
    public float followDistance;
    private float _timer = 0f;
    public bool huntMode = false;
    public TMP_Text hideBigtxt;
    public TMP_Text huntTimer;
    public TMP_Text huntingBigtxt;
    public static SpiderFormation Instance { get; private set; }

    private List<SpiderAI> _spiders = new();

    void Awake() => Instance = this;

    private void Update()
    {
        _timer += Time.deltaTime;
        if (huntTimer != null && hideBigtxt != null && huntingBigtxt != null)
        {
            if (huntModeDelay - _timer >= 28)
            {
                huntTimer.text = "";
                hideBigtxt.gameObject.SetActive(true);

            }
            else if (huntModeDelay - _timer >= 3)
            {
                hideBigtxt.gameObject.SetActive(false);
                huntTimer.text = "hunt begins in " + Mathf.CeilToInt(huntModeDelay - _timer);
            }
            else if (huntModeDelay - _timer >= 0)
            {
                huntTimer.text = "";
                huntingBigtxt.gameObject.SetActive(true);
                huntingBigtxt.text = "HUNT BEGINS\nIN " + Mathf.CeilToInt(huntModeDelay - _timer);
            }
            else
            {
                huntingBigtxt.gameObject.SetActive(false);
                hideBigtxt.gameObject.SetActive(false);
                huntTimer.text = "";
            }
        }
        if (_timer < huntModeDelay)
        {
            huntMode = false;
            foreach (var spider in _spiders)
            {
                spider.followDistance = startFollowDistance;
                spider.DoNormalFollow();
                spider.SnapToGround();
            }
            huntMode = false;
            return;
        }
        else
        {
            huntMode = true;
            foreach (var spider in _spiders)
            {
                spider.followDistance = huntFollowDistance;
                spider.DoHuntMode();
                spider.SnapToGround();
            }
        }
    }
    public void Register(SpiderAI spider)
    {
        if (!_spiders.Contains(spider))
        {
            _spiders.Add(spider);
            spider.followDistance = startFollowDistance;
        }
        AssignSlots();
    }

    public void Unregister(SpiderAI spider)
    {
        _spiders.Remove(spider);
        AssignSlots();
    }

    void AssignSlots()
    {
        int count = _spiders.Count;
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i;
            _spiders[i].AssignSlot(angle);
        }
    }
}