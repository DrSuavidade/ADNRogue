using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Usually the Player root transform.")]
    public Transform followTarget;

    [Header("Orbit")]
    public float mouseXSensitivity = 200f;
    public float mouseYSensitivity = 150f;
    [Range(-80f, 85f)] public float minPitch = -35f;
    [Range(-80f, 85f)] public float maxPitch = 65f;
    public bool invertY = false;
    public bool lockCursor = true;
    [Tooltip("Kept for compatibility; ignored when always-on orbit is desired.")]
    public bool requireRightMouse = false;

    [Header("Follow")]
    public Vector3 followOffset = new Vector3(0f, 1.6f, 0f);

    float yaw;
    float pitch;

    void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        Vector3 e = transform.rotation.eulerAngles;
        yaw = e.y;
        pitch = NormalizePitch(e.x);
    }

    void LateUpdate()
    {
        if (followTarget == null) return;

        transform.position = followTarget.position + followOffset;

        // ALWAYS rotate on mouse movement (no button needed)
        float mx = Input.GetAxis("Mouse X");
        float my = Input.GetAxis("Mouse Y");

        yaw   += mx * mouseXSensitivity * Time.deltaTime;
        float ySign = invertY ? 1f : -1f;
        pitch += ySign * my * mouseYSensitivity * Time.deltaTime;
        pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    static float NormalizePitch(float xAngle)
    {
        xAngle = (xAngle > 180f) ? xAngle - 360f : xAngle;
        return Mathf.Clamp(xAngle, -89f, 89f);
    }
}
