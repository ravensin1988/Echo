// AutoDestroyFallback.cs
using UnityEngine;

public class AutoDestroyFallback : MonoBehaviour
{
    void Start()
    {
        if (TryGetComponent<ParticleSystem>(out var ps))
        {
            var main = ps.main;
            float maxLifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                ? main.startLifetime.constant
                : main.startLifetime.constantMax;
            Destroy(gameObject, main.duration + maxLifetime + 0.5f);
        }
        else
        {
            Destroy(gameObject, 2f);
        }
    }
}