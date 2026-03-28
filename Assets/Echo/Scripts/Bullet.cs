using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour
{
    // ВАЖНО: BulletMode должен быть объявлен ДО его использования
    public enum BulletMode
    {
        Projectile,
        Hitscan
    }
    [SerializeField] private TrailRenderer trailRenderer; // или получите через GetComponent
    [Header("Параметры жизни")]
    public float maxLifetime = 3f;
    public float maxDistance = 200f;

    private Vector3 direction;
    private float speed;
    private LayerMask targetLayers;
    private float baseDamage;
    private float falloffStart;
    private float falloffEnd;
    private float minDamagePercent;
    private Vector3 startPosition;
    private BulletMode mode;
    private bool isActive = false;
    
    // Оптимизационные поля
    private Transform cachedTransform;
    private WaitForSeconds lifetimeWait;
    private float maxDistanceSqr;
    
    // Статический счетчик
    private static int activeBulletCount = 0;
    private static readonly int MaxPooledBullets = 100;

    void Awake()
    {
        cachedTransform = transform;
        lifetimeWait = new WaitForSeconds(maxLifetime);
    }

    public void Initialize(
        Vector3 dir,
        float bulletSpeed,
        LayerMask layers,
        float damage,
        float startFalloff,
        float endFalloff,
        float minDmgPercent,
        BulletMode bulletMode = BulletMode.Projectile,
        float customMaxDistance = -1f)
    {
        if (activeBulletCount >= MaxPooledBullets)
        {
            //Debug.LogWarning("[Bullet] Pool limit reached, skipping bullet");
            ReturnToPoolImmediate();
            return;
        }
        
        direction = dir.normalized;
        speed = bulletSpeed;
        targetLayers = layers;
        baseDamage = damage;
        falloffStart = startFalloff;
        falloffEnd = endFalloff;
        minDamagePercent = minDmgPercent;
        startPosition = cachedTransform.position;
        mode = bulletMode;
        isActive = true;
        activeBulletCount++;
        
        float actualMaxDistance = customMaxDistance > 0 ? customMaxDistance : maxDistance;
        maxDistanceSqr = actualMaxDistance * actualMaxDistance;
        
        cachedTransform.rotation = Quaternion.LookRotation(direction);
        
        if (mode == BulletMode.Projectile)
        {
            StartCoroutine(LifetimeRoutine());
        }
    }

    void Update()
    {
        if (!isActive || mode != BulletMode.Projectile) return;

        float distanceSqr = (cachedTransform.position - startPosition).sqrMagnitude;
        if (distanceSqr > maxDistanceSqr)
        {
            ReturnToPool();
            return;
        }

        float step = speed * Time.deltaTime;

        if (Physics.Raycast(cachedTransform.position, direction, out RaycastHit hit, step, targetLayers))
        {
            ProcessHit(hit);
            return;
        }

        cachedTransform.position += direction * step;
    }

    private IEnumerator LifetimeRoutine()
    {
        yield return lifetimeWait;
        if (isActive)
        {
            ReturnToPool();
        }
    }

    private void ProcessHit(RaycastHit hit)
    {
        try
        {
            float distance = Vector3.Distance(startPosition, hit.point);
            float finalDamage = CalculateDamage(distance);

            // 1. Физическая реакция
            if (hit.collider.TryGetComponent<PhysicsReactObject>(out var physicsReact))
            {
                Vector3 hitDirection = (hit.point - startPosition).normalized;
                float force = Mathf.Lerp(baseDamage * 5f, baseDamage,
                                       Mathf.InverseLerp(0, 100f, distance));
                physicsReact.ReactToBulletHit(hit.point, hitDirection, force);
            }

            // 2. Урон (ТОЛЬКО ОДИН РАЗ!)
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(finalDamage);
            // 3. Эффекты попадания
            SurfaceTypeTag surfaceComp = hit.collider.GetComponentInParent<SurfaceTypeTag>();
            SurfaceMaterial type = surfaceComp != null ? surfaceComp.surfaceMaterial : SurfaceMaterial.Default;
            string effectKey = GetEffectKeyByMaterial(type);
            if (!string.IsNullOrEmpty(effectKey) && VFXPool.Instance != null)
            {
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);
                VFXPool.Instance.Get(effectKey, hit.point, rotation);
            }
            
            // 4. Декали
            if (BulletDecal.Instance != null)
            {
                Transform parentSurface = hit.collider.transform;
                if (hit.collider.attachedRigidbody != null)
                {
                    parentSurface = hit.collider.attachedRigidbody.transform;
                }
                
                BulletDecal.Instance.SpawnDecal(
                    hit.point + hit.normal * 0.001f,
                    hit.normal,
                    parentSurface,
                    type
                );
            }
        }
        finally
        {
            ReturnToPool();
        }
    }
    private string GetEffectKeyByMaterial(SurfaceMaterial material)
    {
        return material switch
        {
            SurfaceMaterial.Wood => "wood",
            SurfaceMaterial.Concrete => "concrete",
            SurfaceMaterial.Metal => "metal",
            SurfaceMaterial.Water => "water",
            _ => "default"
        };
    }

    private float CalculateDamage(float distance)
    {
        if (distance <= falloffStart) return baseDamage;
        if (distance >= falloffEnd) return baseDamage * minDamagePercent;
        float t = Mathf.InverseLerp(falloffStart, falloffEnd, distance);
        return Mathf.Lerp(baseDamage, baseDamage * minDamagePercent, t);
    }

    public void ReturnToPool()
    {
        if (!isActive) return;
        ReturnToPoolImmediate();
    }
    
    private void ReturnToPoolImmediate()
    {
        if (!isActive) return;
        
        isActive = false;
        activeBulletCount--;
        
        StopAllCoroutines();
        
        if (BulletPool.Instance != null)
        {
            BulletPool.Instance.ReturnBullet(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
public void ResetForPool()
{
    isActive = false;
    activeBulletCount = Mathf.Max(0, activeBulletCount - 1);
    
    // Сбрасываем след
    if (trailRenderer != null)
    {
        trailRenderer.Clear(); // ← КЛЮЧЕВАЯ СТРОКА
    }
}
    
    void OnDestroy()
    {
        if (isActive)
        {
            isActive = false;
            activeBulletCount = Mathf.Max(0, activeBulletCount - 1);
        }
    }
}