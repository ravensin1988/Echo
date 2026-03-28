using UnityEngine;

public class RayHitObjectPlacer : MonoBehaviour
{
    [SerializeField] private Transform sourceTransform;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject hitObject;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float maxDistance = 100f;
    [SerializeField] private bool drawRayLine = true;
    [SerializeField] private Color lineColor = Color.red;
    [SerializeField] private float lineWidth = 0.05f;

    private LineRenderer lineRenderer;
    private static Material _sharedLineMaterial;

    void Awake()
    {
        if (_sharedLineMaterial == null)
        {
            _sharedLineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        // Автоматическое назначение MainCamera, если не указана
        if (mainCamera == null)
        {
            GameObject camObj = GameObject.FindWithTag("MainCamera");
            if (camObj != null)
                mainCamera = camObj.GetComponent<Camera>();
        }

        // Автоматическое назначение HitObject, если не указан
        if (hitObject == null)
        {
            GameObject hitObj = GameObject.FindWithTag("DebugTarget");
            if (hitObj != null)
                hitObject = hitObj;
        }

        if (drawRayLine)
        {
            lineRenderer = GetComponent<LineRenderer>() ?? gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = _sharedLineMaterial;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 2;
        }
        else if (TryGetComponent(out LineRenderer lr))
        {
            lr.enabled = false;
        }
    }

    void Update()
    {
        if (sourceTransform == null || mainCamera == null || hitObject == null)
        {
            if (lineRenderer != null) lineRenderer.enabled = false;
            return;
        }

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 hitPosition = ray.origin + ray.direction * maxDistance;
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxDistance, targetLayer);

        if (hasHit)
        {
            hitPosition = hit.point;
        }

        hitObject.SetActive(hasHit);
        hitObject.transform.position = hitPosition;

        if (drawRayLine && lineRenderer != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor = lineColor;
            lineRenderer.SetPosition(0, sourceTransform.position);
            lineRenderer.SetPosition(1, hitPosition);
        }
    }
}