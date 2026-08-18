using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class ShootingController : MonoBehaviour
{
    public Transform cameraTransform;
    public float shootRange = 200f;
    public float cooldown = 1f;
    public LayerMask hitMask;
    public Transform _playerTransform;

    [Header("PSX Feel")]
    public float screenShakeDuration = 0.1f;
    public float screenShakeMagnitude = 0.08f;
    public Image flashImage;
    public float flashOffSpeed = 0.1f;
    public CinemachineImpulseSource impulseSource;

    private float _cooldownTimer = 0f;
    private float _shakeTimer = 0f;
    private Vector3 _originalCamLocalPos;
    private bool _shaking = false;

    void Start()
    {
        if (cameraTransform != null)
            _originalCamLocalPos = cameraTransform.localPosition;
    }

    void Update()
    {
        Debug.DrawRay(cameraTransform.position, cameraTransform.forward * shootRange, Color.black);
        _cooldownTimer -= Time.deltaTime;

        if (Input.GetMouseButtonDown(1) && _cooldownTimer <= 0f)
        {
            Shoot();
            _cooldownTimer = cooldown;
        }

        HandleShake();
        HandleFlash();
    }

    void Shoot()
    {
        if (flashImage != null)
            flashImage.color = new Color(1, 1, 1, 0.4f);
        GetComponent<AudioSource>().PlayOneShot(AudioStore.Instance.GunShootAudio);
        StartCoroutine(PlayerKnockback(cameraTransform.forward));

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, shootRange, hitMask, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("hitbox"))
            {
                SpiderAI spider = hit.collider.GetComponentInParent<SpiderAI>();
                if (spider != null)
                    spider.TakeHit();
            }
        }
    }

    IEnumerator PlayerKnockback(Vector3 shotDirection)
    {
        Vector3 kickDir = new Vector3(-shotDirection.x, 0, -shotDirection.z).normalized;
        float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _playerTransform.position += kickDir * 2f * Time.deltaTime;
            yield return null;
        }
    }

    void HandleShake()
    {
        if (!_shaking) return;
        impulseSource.GenerateImpulse();
        _shakeTimer -= Time.deltaTime;
        if (_shakeTimer <= 0f)
        {
            _shaking = false;
            cameraTransform.localPosition = _originalCamLocalPos;
            return;
        }

        float x = Random.Range(-1f, 1f) * screenShakeMagnitude;
        float y = Random.Range(-1f, 1f) * screenShakeMagnitude;
        cameraTransform.localPosition = _originalCamLocalPos + new Vector3(x, y, 0);
    }

    void HandleFlash()
    {
        if (flashImage == null) return;
        Color c = flashImage.color;
        c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * flashOffSpeed);
        flashImage.color = c;
    }
}