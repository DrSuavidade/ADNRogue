using UnityEngine;

[DisallowMultipleComponent]
public class KnockbackReceiver : MonoBehaviour
{
    [Header("Tuning")]
    [Tooltip("How quickly the knockback decays (per second). Higher = shorter knockback.")]
    public float decayRate = 6f;

    [Tooltip("Max horizontal speed applied by knockback alone.")]
    public float maxHorizontalSpeed = 10f;

    [Tooltip("Should we move using CharacterController if present? Otherwise we just modify transform.")]
    public bool useCharacterControllerIfPresent = true;

    private Vector3 _velocity; // knockback-only velocity
    private CharacterController _cc;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    public void ApplyImpulse(Vector3 impulse)
    {
        // Treat impulse as an instantaneous velocity change.
        _velocity += impulse;
    }

    void Update()
    {
        if (_velocity.sqrMagnitude <= 0.000001f) return;

        // Clamp horizontal part
        Vector3 horiz = new Vector3(_velocity.x, 0f, _velocity.z);
        if (horiz.magnitude > maxHorizontalSpeed)
        {
            horiz = horiz.normalized * maxHorizontalSpeed;
            _velocity = new Vector3(horiz.x, _velocity.y, horiz.z);
        }

        Vector3 delta = _velocity * Time.deltaTime;

        if (useCharacterControllerIfPresent && _cc != null && _cc.enabled)
        {
            _cc.Move(delta);
        }
        else
        {
            transform.position += delta;
        }

        // Exponential decay toward zero
        float k = Mathf.Clamp01(decayRate * Time.deltaTime);
        _velocity = Vector3.Lerp(_velocity, Vector3.zero, k);
    }
}
