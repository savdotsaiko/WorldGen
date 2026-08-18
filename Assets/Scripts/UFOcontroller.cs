using UnityEngine;

public class UFOcontroller : MonoBehaviour
{
    private float FB, LR;
    public float moveSpeed = 15f;
    private float turnAngle = 0f, pitchAngle = 0f;
    public float pitchSensitivity = 3f;
    public float minPitch = -80f;
    public float maxPitch = 80f;


    public float mouseSensitivity = 3f;
    public Transform movementRoot;

    private void Start()
    {
        movementRoot = transform;
    }
    void Update()
    {
        LR = Input.GetAxis("Horizontal");
        FB = Input.GetAxis("Vertical");

        Vector3 desiredDir = (movementRoot.forward * FB + movementRoot.right * LR);
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        turnAngle += mouseX * mouseSensitivity;

        float pitchInput = -mouseY ;
        pitchAngle = Mathf.Clamp(
            pitchAngle + pitchInput * pitchSensitivity,
            minPitch,
            maxPitch
        );

        Quaternion yawRotation = Quaternion.Euler(0f, turnAngle, 0);
        Quaternion pitchRotation = Quaternion.Euler(pitchAngle, 0f, 0f);
        movementRoot.rotation = yawRotation * pitchRotation;

        Vector3 forward = movementRoot.transform.forward;
        Vector3 right = movementRoot.transform.right;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        transform.position += forward * FB * moveSpeed * Time.deltaTime;
        transform.position += right * LR * moveSpeed * Time.deltaTime;

    }
}
