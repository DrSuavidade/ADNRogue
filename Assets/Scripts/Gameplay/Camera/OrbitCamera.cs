using UnityEngine;
using UnityEngine.InputSystem;

namespace Geneforge.Gameplay.Cameras
{
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

        [Tooltip("If true, only orbits while Right Mouse Button is pressed.")]
        public bool requireRightMouse = false;

        [Header("Follow")]
        public Vector3 followOffset = new Vector3(0f, 1.6f, 0f);

        float yaw;
        float pitch;

        void Start()
        {
            if (followTarget == null)
            {
                Debug.LogWarning("OrbitCamera: followTarget not set.", this);
            }

            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            var e = transform.rotation.eulerAngles;
            yaw = e.y;
            pitch = NormalizePitch(e.x);
        }

        void LateUpdate()
        {
            if (followTarget == null) return;
            if (Mouse.current == null) return;

            transform.position = followTarget.position + followOffset;

            bool orbiting = true;
            if (requireRightMouse)
                orbiting = Mouse.current.rightButton.isPressed;

            if (orbiting)
            {
                // Prevent rotation if cursor is unlocked (e.g. interacting with UI)
                if (lockCursor && Cursor.lockState != CursorLockMode.Locked) 
                    return;

                Vector2 delta = Mouse.current.delta.ReadValue();
                float dt = Time.unscaledDeltaTime;

                float mx = delta.x * mouseXSensitivity * 0.01f * dt;
                float my = delta.y * mouseYSensitivity * 0.01f * dt;

                yaw += mx;
                float ySign = invertY ? 1f : -1f;
                pitch += ySign * my;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // --- (Opcional) Zoom pelo scroll ---
            // float scrollY = Mouse.current.scroll.ReadValue().y; // ~±120 por notch
            // if (Mathf.Abs(scrollY) > 0.01f) { ... }
        }

        static float NormalizePitch(float xAngle)
        {
            xAngle = (xAngle > 180f) ? xAngle - 360f : xAngle;
            return Mathf.Clamp(xAngle, -89f, 89f);
        }
    }
}
