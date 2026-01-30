using UnityEngine;

namespace Geneforge.Gameplay.Items
{
    /// <summary>
    /// Adds a floating, rotating, and scaling animation to the object.
    /// Useful for rewards to make them look more dynamic.
    /// </summary>
    public class RewardRotator : MonoBehaviour
    {
        [Header("Rotation Settings")]
        [Tooltip("Speed of rotation in degrees per second.")]
        public float rotationSpeed = 50f;

        [Header("Bobbing Settings")]
        [Tooltip("Height of the bobbing motion.")]
        public float bobbingAmplitude = 0.5f;

        [Tooltip("Speed of the bobbing motion.")]
        public float bobbingFrequency = 1f;

        [Header("Scaling Settings")]
        [Tooltip("Amount of scaling applied (additive to original scale).")]
        public float scalingAmplitude = 0.1f;

        [Tooltip("Speed of the scaling pulsing effect.")]
        public float scalingFrequency = 1f;

        private Vector3 _startPosition;
        private Vector3 _startScale;

        private void Start()
        {
            // Store the initial position and scale to oscillate around them
            _startPosition = transform.position;
            _startScale = transform.localScale;
        }

        private void Update()
        {
            float time = Time.time;

            // 1. Rotation: Rotate around the Y axis
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);

            // 2. Bobbing: Calculate the new Y position using a Sine wave
            float newY = _startPosition.y + Mathf.Sin(time * bobbingFrequency) * bobbingAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // 3. Scaling: Pulse the scale upwards (starts at original scale and increases)
            float scaleFactor = (Mathf.Sin(time * scalingFrequency) + 1f) * 0.5f; // Range 0 to 1
            float scaleOffset = scaleFactor * scalingAmplitude;
            transform.localScale = _startScale + (_startScale * scaleOffset);
        }
    }
}
