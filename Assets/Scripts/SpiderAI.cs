
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SpiderAI : MonoBehaviour
{

    [Header("Follow Settings")]
    public Transform player;
    public float followDistance = 8f;
    public float moveSpeed = 3f;
    public float turnSpeed = 5f;
    public float positionSmoothTime = 0.4f;

    public Transform bodyTransform;

    [Header("Delay")]
    public int historyLength = 45;
    public float historyInterval = 0.05f;

    private float _slotAngle = 0f;
    private Queue<Vector3> _posHistory = new();
    private float _historyTimer;
    private Vector3 _velocity;

    [Header("Hunt Mode")]
    public Transform playerCamera;
    public float detectionAngle = 60f;
    public float creepSpeed = 1.5f;
    public GameObject[] disguisePrefabs;
    public float disguiseScaleMultiplier = 1f;

    [Header("Health")]
    public int hitPoints = 1;

    public Renderer mainRenderer;


    private enum HuntState { Moving, Stopping, Disguised }
    private HuntState _huntState = HuntState.Moving;
    private float _settleTimer = 0f;
    public float settleTime = 0.3f;

    private Vector3 _lastPos;
    private float _stopCheckTimer = 0f;

    public bool _isDetected = false, stopFollowing = false;
    private GameObject _activeDisguise;
    private Renderer[] _ownRenderers;
    private Dictionary<GameObject, Queue<GameObject>> _pool = new();
    private GameObject _activeDisguisePrefab;

    private void Awake()
    {
        if (mainRenderer != null)
            mainRenderer.material = new Material(mainRenderer.material);
    }

    void Start()
    {
        _ownRenderers = GetComponentsInChildren<Renderer>();

        var controller = GetComponent<SpiderDriverController>();
        if (controller != null) controller.enabled = false;

        if (SpiderFormation.Instance == null)
        {
            GameObject go = new GameObject("SpiderFormation");
            go.AddComponent<SpiderFormation>();
        }

        SpiderFormation.Instance.Register(this);
        player = GameObject.FindWithTag("Player").transform;
        SnapToGround();
    }

    void Update()
    {
        if (player == null) return;
    }
    GameObject GetFromPool(GameObject prefab)
    {
        if (!_pool.ContainsKey(prefab))
            _pool[prefab] = new Queue<GameObject>();

        GameObject obj;
        if (_pool[prefab].Count > 0)
        {
            obj = _pool[prefab].Dequeue();
            obj.SetActive(true);
        }
        else
        {
            obj = Instantiate(prefab, transform.position,
                Quaternion.Euler(0, Random.Range(0f, 360f), 0), transform);
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        Collider col = obj.GetComponent<Collider>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (col != null)
        {
            col.enabled = false;
        }

        return obj;
    }

    void ReturnToPool(GameObject prefab, GameObject obj)
    {
        obj.SetActive(false);
        if (!_pool.ContainsKey(prefab))
            _pool[prefab] = new Queue<GameObject>();
        _pool[prefab].Enqueue(obj);
    }
    public void AssignSlot(float angle) => _slotAngle = angle;


    public void StartHunting()
    {
        DoHuntMode();
        SnapToGround();
    }
    public void DoNormalFollow()
    {
        if (stopFollowing) return;

        ShowSelf();
        Disguise(true);

        Quaternion slotRot = Quaternion.AngleAxis(_slotAngle, Vector3.up);
        Vector3 slotOffset = slotRot * (Vector3.back * followDistance);
        Vector3 targetPos = player.position + slotOffset;
        targetPos.y = transform.position.y;

        Vector3 currentXZ = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 targetXZ = new Vector3(targetPos.x, 0, targetPos.z);
        float dist = Vector3.Distance(currentXZ, targetXZ);

        if (dist > 1f)
            SnapToGround();
        float speed = moveSpeed;

        Vector3 newXZ = Vector3.MoveTowards(currentXZ, targetXZ, speed * Time.deltaTime);
        transform.position = new Vector3(newXZ.x, transform.position.y, newXZ.z);

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toPlayer), turnSpeed * Time.deltaTime);
    }

    public void DoHuntMode()
    {
        if (stopFollowing) return;
        _isDetected = IsPlayerLookingAtMe();

        switch (_huntState)
        {
            case HuntState.Moving:
                ShowSelf();
                if (_isDetected)
                {
                    _huntState = HuntState.Stopping;
                    _settleTimer = 0f;
                    _lastPos = transform.position;
                }
                else
                {
                    CreepTowardPlayer();
                }
                break;

            case HuntState.Stopping:
                _settleTimer += Time.deltaTime;

                if (!_isDetected)
                {
                    _huntState = HuntState.Moving;
                    _settleTimer = 0f;
                    break;
                }

                if (_settleTimer >= settleTime)
                {
                    _huntState = HuntState.Disguised;
                    Disguise(false);
                }
                break;

            case HuntState.Disguised:
                if (!_isDetected)
                {
                    _huntState = HuntState.Moving;
                    ShowSelf();
                }
                break;
        }
    }

    public void CreepTowardPlayer()
    {
        if (stopFollowing) return;
        Quaternion slotRot = Quaternion.AngleAxis(_slotAngle, Vector3.up);
        Vector3 slotOffset = slotRot * (Vector3.back * followDistance);
        Vector3 targetPos = player.position + slotOffset;
        targetPos.y = transform.position.y;

        transform.position = Vector3.MoveTowards(
            transform.position, targetPos, creepSpeed * Time.deltaTime);

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toPlayer), turnSpeed * Time.deltaTime);
    }

    void Disguise(bool half)
    {
        foreach (var r in _ownRenderers) r.enabled = false;
        if (half) return;
        if (_activeDisguise == null && disguisePrefabs != null && disguisePrefabs.Length > 0)
        {
            // Use instance ID as part of seed so each spider picks differently
            int idx = (GetInstanceID() + (int)(_settleTimer * 100)) % disguisePrefabs.Length;
            idx = Mathf.Abs(idx);
            _activeDisguisePrefab = disguisePrefabs[idx];
            _activeDisguise = GetFromPool(_activeDisguisePrefab);
            _activeDisguise.transform.position = transform.position;
            _activeDisguise.transform.rotation = _activeDisguisePrefab.transform.rotation;
            _activeDisguise.transform.localScale = _activeDisguisePrefab.transform.localScale;
            _activeDisguise.transform.SetParent(transform);
        }
        else if (_activeDisguise != null)
        {
            _activeDisguise.SetActive(true);
        }
        SnapToGround();
    }

    void ShowSelf()
    {
        foreach (var r in _ownRenderers) r.enabled = true;

        if (_activeDisguise != null && _activeDisguisePrefab != null)
        {
            ReturnToPool(_activeDisguisePrefab, _activeDisguise);
            _activeDisguise = null;
            _activeDisguisePrefab = null;
        }
        SnapToGround();

    }
    public void SetWinColor(bool red)
    {
        if (mainRenderer != null)
            mainRenderer.material.color = red ? Color.red : Color.white;
    }
    public void Revive()
    {
        hitPoints = 3;
        _huntState = HuntState.Moving;
        _activeDisguise = null;
        _activeDisguisePrefab = null;

        if (mainRenderer != null)
            mainRenderer.enabled = true;

        foreach (var r in _ownRenderers)
            r.enabled = true;

        enabled = true;
        SpiderFormation.Instance?.Register(this);
    }
    public void TakeHit()
    {
        hitPoints--;

        if (hitPoints <= 0)
            Die();
    }

    void Die()
    {
        SpiderFormation.Instance?.Unregister(this);

        ShowSelf();
        if (mainRenderer != null)
            mainRenderer.enabled = false;


        enabled = false;
    }

    bool IsPlayerLookingAtMe()
    {
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : player.forward;

        Vector3 flatForward = new Vector3(forward.x, 0, forward.z).normalized;
        Vector3 flatToSpider = new Vector3(
            transform.position.x - player.position.x,
            0,
            transform.position.z - player.position.z).normalized;

        float dot = Vector3.Dot(flatForward, flatToSpider);
        float threshold = Mathf.Cos(detectionAngle * 0.5f * Mathf.Deg2Rad);

        if (dot <= threshold) return false;

        // In angle — now check if terrain blocks line of sight
        Vector3 eyePos = player.position + Vector3.up * 0.5f;
        Vector3 spiderPos = bodyTransform.position + Vector3.up * 0.1f;
        Vector3 dir = (spiderPos - eyePos).normalized;
        float dist = Vector3.Distance(eyePos, spiderPos);

        if (Physics.Raycast(eyePos, dir, out RaycastHit hit, dist, LayerMask.GetMask("Ground")))
        {
            return false;
        }
        Debug.DrawLine(eyePos, spiderPos, Color.red);

        return true;
    }
    public void SnapToGround()
    {
        GetComponentInChildren<BodyOrienter>().enabled = false;
        Vector3 rayOrigin = transform.position + Vector3.up * 5000f;
        var rayDown = Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hitDown, Mathf.Infinity,
            LayerMask.GetMask("Ground"));
        var rayUp = Physics.Raycast(rayOrigin, Vector3.up, out RaycastHit hitUp, Mathf.Infinity,
            LayerMask.GetMask("Ground"));
        Vector3 pos = transform.position;
        if (rayDown)
        {
            pos.y = hitDown.point.y;
        }
        else if (rayUp)
        {

            pos.y = hitUp.point.y;
        }
        transform.position = pos;
        GetComponentInChildren<BodyOrienter>().enabled = true;

    }

}