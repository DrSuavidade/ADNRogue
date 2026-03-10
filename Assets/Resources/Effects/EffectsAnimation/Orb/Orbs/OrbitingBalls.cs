using System.Collections.Generic;
using UnityEngine;

public class OrbitingBalls : MonoBehaviour
{
    public GameObject ballPrefab;

    public int numberOfBalls = 3;
    public float radius = 2f;
    public float speed = 90f; // graus por segundo
    public float height = 0f;

    private List<Transform> balls = new List<Transform>();
    private float angle;

    void Start()
    {
        for (int i = 0; i < numberOfBalls; i++)
        {
            GameObject ball = Instantiate(ballPrefab);
            balls.Add(ball.transform);
        }
    }

    void Update()
    {
        angle += speed * Time.deltaTime;

        float step = 360f / balls.Count;

        for (int i = 0; i < balls.Count; i++)
        {
            float currentAngle = angle + step * i;
            float rad = currentAngle * Mathf.Deg2Rad;

            float x = Mathf.Cos(rad) * radius;
            float z = Mathf.Sin(rad) * radius;

            balls[i].position = transform.position + new Vector3(x, height, z);
        }
    }
}