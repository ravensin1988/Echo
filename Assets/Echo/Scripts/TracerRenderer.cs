using UnityEngine;
using System.Collections.Generic;

public class TracerRenderer : MonoBehaviour
{
    [SerializeField] private int maxTracers = 20;
    [SerializeField] private float tracerDuration = 0.1f;
    
    private readonly Queue<TracerMover> tracerPool = new Queue<TracerMover>();
    private readonly List<TracerMover> activeTracers = new List<TracerMover>();
    private readonly GameObject tracerPrefab;
    
    void Awake()
    {
        InitializePool();
    }
    
    void InitializePool()
    {
        for (int i = 0; i < maxTracers; i++)
        {
            GameObject obj = new GameObject($"Tracer_{i}");
            obj.transform.SetParent(transform);
            obj.SetActive(false);
            
            var lineRenderer = obj.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startWidth = 0.05f;
            lineRenderer.endWidth = 0.05f;
            lineRenderer.positionCount = 2;
            
            var mover = obj.AddComponent<TracerMover>();
            mover.Initialize(this);
            tracerPool.Enqueue(mover);
        }
    }
    
    public void ShowTracer(Vector3 start, Vector3 end)
    {
        if (tracerPool.Count == 0)
        {
            // Переиспользуем самый старый трассер
            if (activeTracers.Count > 0)
            {
                var oldest = activeTracers[0];
                oldest.ResetTracer(start, end, tracerDuration);
                activeTracers.RemoveAt(0);
                activeTracers.Add(oldest);
                return;
            }
            return;
        }
        
        var tracer = tracerPool.Dequeue();
        tracer.ResetTracer(start, end, tracerDuration);
        activeTracers.Add(tracer);
    }
    
    public void ReturnTracer(TracerMover tracer)
    {
        if (activeTracers.Contains(tracer))
        {
            activeTracers.Remove(tracer);
        }
        
        tracerPool.Enqueue(tracer);
    }
    
    void Update()
    {
        // Автоочистка застрявших трассеров
        for (int i = activeTracers.Count - 1; i >= 0; i--)
        {
            if (activeTracers[i] == null)
            {
                activeTracers.RemoveAt(i);
            }
        }
    }
    
    void OnDestroy()
    {
        foreach (var tracer in activeTracers)
        {
            if (tracer != null) Destroy(tracer.gameObject);
        }
        
        foreach (var tracer in tracerPool)
        {
            if (tracer != null) Destroy(tracer.gameObject);
        }
        
        activeTracers.Clear();
        tracerPool.Clear();
    }
}