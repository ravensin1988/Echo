using UnityEngine;
using System.Collections.Generic;

public class TracerMover : MonoBehaviour
{
    private TracerRenderer pool;
    private LineRenderer lineRenderer;
    private float elapsedTime;
    private float duration;
    private bool isActive;
    
    public void Initialize(TracerRenderer tracerPool)
    {
        pool = tracerPool;
        lineRenderer = GetComponent<LineRenderer>();
        gameObject.SetActive(false);
    }
    
    public void ResetTracer(Vector3 start, Vector3 end, float dur)
    {
        transform.position = start;
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
        
        elapsedTime = 0f;
        duration = dur;
        isActive = true;
        
        gameObject.SetActive(true);
        
        // Автоматический возврат в пул
        Invoke(nameof(ReturnToPool), dur + 0.1f);
    }
    
    void Update()
    {
        if (!isActive) return;
        
        elapsedTime += Time.deltaTime;
        float alpha = 1f - (elapsedTime / duration);
        
        var color = lineRenderer.startColor;
        color.a = alpha;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }
    
    void ReturnToPool()
    {
        if (!isActive) return;
        
        isActive = false;
        gameObject.SetActive(false);
        
        CancelInvoke(nameof(ReturnToPool));
        
        if (pool != null)
        {
            pool.ReturnTracer(this);
        }
    }
    
    void OnDestroy()
    {
        CancelInvoke();
    }
}