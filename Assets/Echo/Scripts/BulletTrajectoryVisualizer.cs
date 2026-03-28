using UnityEngine;
using System.Collections.Generic;

public class BulletTrajectoryVisualizer : MonoBehaviour
{
    [Header("Настройки визуализации")]
    [SerializeField] private bool showTrajectory = true;
    [SerializeField] private Color trajectoryColor = Color.red;
    [SerializeField] private float trajectoryWidth = 0.02f;
    [SerializeField] private float trajectoryDuration = 0.1f;
    [SerializeField] private int maxTrajectories = 20;

    [Header("Точки прицеливания")]
    [SerializeField] private Transform weaponTransform;
    [SerializeField] private Transform aimPoint;

    [Header("Настройки лучей")]
    [SerializeField] private int rayCount = 10;
    [SerializeField] private float maxDistance = 100f;

    private LineRenderer predictionLine;
    private readonly List<TrajectoryInfo> shotTrajectories = new();

    private struct TrajectoryInfo
    {
        public LineRenderer line;
        public float spawnTime;
    }

    void Start()
    {
        if (showTrajectory)
        {
            CreatePredictionLine();
        }
    }

    void Update()
    {
        if (!showTrajectory || weaponTransform == null) return;

        if (IsAiming())
        {
            VisualizePredictedTrajectory();
        }
        else if (predictionLine != null)
        {
            predictionLine.enabled = false;
        }

        CleanupOldTrajectories();
    }

    void CreatePredictionLine()
    {
        GameObject lineObj = new();
        lineObj.transform.SetParent(transform);

        predictionLine = lineObj.AddComponent<LineRenderer>();
        predictionLine.material = new Material(Shader.Find("Sprites/Default"));
        predictionLine.startColor = trajectoryColor;
        predictionLine.endColor = trajectoryColor;
        predictionLine.startWidth = trajectoryWidth;
        predictionLine.endWidth = trajectoryWidth;
        predictionLine.positionCount = 2;
        predictionLine.useWorldSpace = true;
        predictionLine.enabled = false;
    }

    void VisualizePredictedTrajectory()
    {
        if (predictionLine == null) return;

        Vector3 startPoint = weaponTransform.position;
        Vector3 direction = (aimPoint.position - startPoint).normalized;

        if (rayCount > 1)
        {
            for (int i = 0; i < rayCount; i++)
            {
                Vector3 spreadDirection = ApplyRandomSpread(direction, 0.1f);

                // Inlined RaycastHit declaration
                if (Physics.Raycast(startPoint, spreadDirection, out RaycastHit hit, maxDistance))
                {
                    Debug.DrawLine(startPoint, hit.point, new Color(1, 0, 0, 0.3f), Time.deltaTime);
                }
                else
                {
                    Debug.DrawLine(startPoint, startPoint + spreadDirection * maxDistance, new Color(1, 0, 0, 0.3f), Time.deltaTime);
                }
            }
        }

        // Inlined RaycastHit declaration
        Vector3 mainEndPoint;
        if (Physics.Raycast(startPoint, direction, out RaycastHit mainHit, maxDistance))
        {
            mainEndPoint = mainHit.point;
        }
        else
        {
            mainEndPoint = startPoint + direction * maxDistance;
        }

        predictionLine.enabled = true;
        predictionLine.SetPosition(0, startPoint);
        predictionLine.SetPosition(1, mainEndPoint);
    }

    Vector3 ApplyRandomSpread(Vector3 direction, float spreadAmount)
    {
        Vector2 randomCircle = Random.insideUnitCircle * spreadAmount;
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
        Vector3 up = Vector3.Cross(direction, right).normalized;

        return (direction + right * randomCircle.x + up * randomCircle.y).normalized;
    }

    public void ShowShotTrajectory(Vector3 startPoint, Vector3 endPoint, float spread)
    {
        if (!showTrajectory) return;

        GameObject shotLineObj = new();
        shotLineObj.transform.SetParent(transform);

        LineRenderer shotLine = shotLineObj.AddComponent<LineRenderer>();
        shotLine.material = new Material(Shader.Find("Sprites/Default"));
        shotLine.startColor = Color.yellow;
        shotLine.endColor = Color.yellow;
        shotLine.startWidth = trajectoryWidth * 1.5f;
        shotLine.endWidth = trajectoryWidth * 0.5f;
        shotLine.positionCount = 2;
        shotLine.useWorldSpace = true;

        shotLine.SetPosition(0, startPoint);
        shotLine.SetPosition(1, endPoint);

        shotTrajectories.Add(new TrajectoryInfo
        {
            line = shotLine,
            spawnTime = Time.time
        });

        if (shotTrajectories.Count > maxTrajectories)
        {
            Destroy(shotTrajectories[0].line.gameObject);
            shotTrajectories.RemoveAt(0);
        }

        if (spread > 0)
        {
            VisualizeSpread(endPoint, spread);
        }
    }

    void VisualizeSpread(Vector3 hitPoint, float spread)
    {
        int segments = 16;
        Vector3[] circlePoints = new Vector3[segments + 1];

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * (360f / segments);
            float x = Mathf.Sin(Mathf.Deg2Rad * angle) * spread;
            float z = Mathf.Cos(Mathf.Deg2Rad * angle) * spread;

            circlePoints[i] = hitPoint + new Vector3(x, 0, z);

            if (i > 0)
            {
                Debug.DrawLine(circlePoints[i - 1], circlePoints[i], Color.cyan, trajectoryDuration);
            }
        }

        // Re-ordered operands for better performance (constant first in multiplication)
        Debug.DrawRay(hitPoint, 0.5f * spread * Vector3.up, Color.cyan, trajectoryDuration);
    }

    void CleanupOldTrajectories()
    {
        for (int i = shotTrajectories.Count - 1; i >= 0; i--)
        {
            if (Time.time - shotTrajectories[i].spawnTime > trajectoryDuration)
            {
                if (shotTrajectories[i].line != null)
                {
                    Destroy(shotTrajectories[i].line.gameObject);
                }
                shotTrajectories.RemoveAt(i);
            }
        }
    }

    bool IsAiming()
    {
        return Input.GetMouseButton(1);
    }

    void OnDestroy()
    {
        if (predictionLine != null)
        {
            Destroy(predictionLine.gameObject);
        }

        foreach (var trajectory in shotTrajectories)
        {
            if (trajectory.line != null)
            {
                Destroy(trajectory.line.gameObject);
            }
        }
        shotTrajectories.Clear();
    }
}