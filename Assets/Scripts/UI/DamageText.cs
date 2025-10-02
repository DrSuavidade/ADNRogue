using UnityEngine;
using System.Collections;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class DamageText : MonoBehaviour
{
    [Tooltip("Assign the TMP_Text in the prefab")]
    public TMP_Text valueText;

    [Tooltip("Normal color for non‐critical damage")]
    public Color normalColor = Color.white;
    [Tooltip("Color for critical hits")]
    public Color critColor   = Color.red;

    [Tooltip("How long before fading out and destroy")]
    public float fadeDuration = 1f;

    [Tooltip("How far it rises over fadeDuration")]
    public Vector3 riseDistance = new Vector3(0, 1f, 0);

    CanvasGroup canvasGroup;
    Vector3 startPos;
    Camera cam;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        cam = Camera.main;
    }

    /// <summary>
    /// Call right after Instantiate.
    /// </summary>
    public void Initialize(float damage, bool wasCrit = false)
    {
        startPos = transform.position;
        valueText.text  = Mathf.CeilToInt(damage).ToString();
        valueText.color = wasCrit ? critColor : normalColor;
        canvasGroup.alpha = 1f;
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            // rise
            transform.position = startPos + riseDistance * (t / fadeDuration);
            // fade
            canvasGroup.alpha = 1f - (t / fadeDuration);
            // face camera
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            t += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}
