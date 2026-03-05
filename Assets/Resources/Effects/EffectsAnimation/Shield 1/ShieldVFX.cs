using UnityEngine;

public class ShieldVFX : MonoBehaviour
{
    public ParticleSystem hitEffect;

    public void Impact(Vector3 pos)
    {
        Instantiate(hitEffect, pos, Quaternion.identity);
    }
}