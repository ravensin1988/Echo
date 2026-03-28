using UnityEngine;

public static class AimUtils // ← класс тоже static!
{
    // Этот метод можно вызвать откуда угодно: AimUtils.TryGetHitPoint(...)
    public static bool TryGetHitPoint(Camera camera, LayerMask targetLayer, float maxDistance, out Vector3 hitPoint)
    {
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, targetLayer))
        {
            hitPoint = hit.point;
            return true; // попали
        }
        else
        {
            hitPoint = ray.origin + ray.direction * maxDistance;
            return false; // промах
        }
    }
}