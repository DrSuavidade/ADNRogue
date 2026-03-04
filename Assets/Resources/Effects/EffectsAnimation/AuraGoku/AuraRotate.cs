using UnityEngine;

public class AuraRotate : MonoBehaviour
{
    public float speed = 40f;

    void Update()
    {
        transform.Rotate(0f, speed * Time.deltaTime, 0f);
    }
}