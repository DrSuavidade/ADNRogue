using UnityEngine;

namespace Geneforge.Gameplay.Cameras
{
    public class FocusCamera : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Camera cam;
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0, 1.6f, 0); // Offset to look at head, not feet

        private Transform target;
        private bool isActive;
        private float originalDepth;

        private void Awake()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            
            if (cam != null) 
            {
                originalDepth = cam.depth;
                cam.enabled = false; // Starts disabled
            }
        }

        public void Activate(Transform playerTransform)
        {
            Debug.Log($"[FocusCamera] Activating focus camera for: {playerTransform.name}");
            target = playerTransform;
            isActive = true;
            
            if (cam != null) 
            {
                cam.enabled = true;
                cam.depth = 1000; // Extremely high value to ensure it stays on top of everything
                cam.nearClipPlane = 0.01f; // Prevent clipping the player when close
                
                // --- Initial Snap ---
                // Rotate instantly to target on activation
                Vector3 targetPos = target.position + lookAtOffset;
                Vector3 direction = targetPos - cam.transform.position;
                if (direction != Vector3.zero)
                {
                    cam.transform.rotation = Quaternion.LookRotation(direction);
                }
                
                // If there's an AudioListener on this camera, enable it
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;
            }
        }

        public void Deactivate()
        {
            Debug.Log("[FocusCamera] Deactivating focus camera.");
            isActive = false;
            if (cam != null) 
            {
                cam.enabled = false;
                cam.depth = originalDepth;
                
                AudioListener listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (isActive && target != null && cam != null)
            {
                // Calculate direction to player with offset
                Vector3 targetPos = target.position + lookAtOffset;
                Vector3 direction = targetPos - cam.transform.position;
                
                if (direction != Vector3.zero)
                {
                    // Smoothly rotate to look at target
                    Quaternion targetRotation = Quaternion.LookRotation(direction);
                    cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, targetRotation, smoothSpeed * Time.deltaTime);
                }
            }
        }
    }
}