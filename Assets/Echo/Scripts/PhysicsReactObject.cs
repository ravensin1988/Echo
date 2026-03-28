// File: PhysicsReactObject.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class PhysicsReactObject : MonoBehaviour, IDamageable
{
    [Header("Физическая реакция")]
    [SerializeField] private float hitForceMultiplier = 1f;
    [SerializeField] private float torqueMultiplier = 0.5f;
    [SerializeField] private float minForceToReact = 5f;
    
    [Header("Разрушение")]
    [SerializeField] private bool canBeDestroyed = true;
    [SerializeField] private float health = 10f;
    [SerializeField] private DestructionMode destructionMode = DestructionMode.Disappear;
    
    [Header("Взрывной урон (если это взрывающийся объект)")]
    [SerializeField] private bool isExplosive = false;
    [SerializeField] private float explosionDamage = 50f;
    [SerializeField] private float explosionRadius = 5f;
    [SerializeField] private float explosionForce = 100f;
    [SerializeField] private float explosionDuration = 2f; // Длительность эффекта взрыва
    [SerializeField] private LayerMask explosionAffectedLayers = ~0;
    [SerializeField] private bool causeChainReaction = true;
    
    [Header("Визуальные эффекты")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private bool attachHitEffectToParent = true; // Новая опция
    [SerializeField] private float hitEffectDuration = 2f; // Длительность эффекта попадания
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip explosionSound;
    
    [Header("Разрушение на осколки")]
    [SerializeField] private GameObject[] brokenPieces;
    [SerializeField] private int piecesCount = 3;
    [SerializeField] private float piecesExplosionForce = 10f;
    [SerializeField] private float piecesLifetime = 5f;
    
    [Header("Оптимизация")]
    [SerializeField] private bool startKinematic = true;
    [SerializeField] private float sleepThreshold = 0.001f;

    private Rigidbody rb;
    private AudioSource audioSource;
    private Collider objectCollider;
    private Renderer objectRenderer;
    private bool isDestroyed = false;
    private float currentHealth;
    private readonly List<GameObject> activeEffects = new (); // Для отслеживания эффектов
    
    // Для прикрепленных эффектов
    private readonly Dictionary<GameObject, Coroutine> attachedEffects = new ();
    
    private enum DestructionMode
    {
        Disappear,
        BreakApart
    }
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();
        objectRenderer = GetComponent<Renderer>();
        
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f;
            audioSource.playOnAwake = false;
        }

        SetupPhysicsOptimization();
        currentHealth = health;
    }
    
    void OnDestroy()
    {
        // Уничтожаем все созданные эффекты при уничтожении объекта
        CleanupAllEffects();
        
        // Останавливаем все корутины для прикрепленных эффектов
        foreach (var coroutine in attachedEffects.Values)
        {
            if (coroutine != null)
                StopCoroutine(coroutine);
        }
        attachedEffects.Clear();
    }
    
    void CleanupAllEffects()
    {
        foreach (GameObject effect in activeEffects)
        {
            if (effect != null)
            {
                Destroy(effect);
            }
        }
        activeEffects.Clear();
    }
    
    void SetupPhysicsOptimization()
    {
        rb.isKinematic = startKinematic;
        rb.sleepThreshold = sleepThreshold;
        rb.solverIterations = 6;
        rb.solverVelocityIterations = 1;
        rb.collisionDetectionMode = startKinematic ? 
            CollisionDetectionMode.Discrete : 
            CollisionDetectionMode.ContinuousDynamic;
        rb.mass = Mathf.Clamp(rb.mass, 0.5f, 10f);
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;
    }
    
    // Метод для создания эффекта с автоуничтожением
    GameObject CreateEffect(GameObject effectPrefab, Vector3 position, Quaternion rotation, float duration, bool attachToParent = false)
    {
        if (effectPrefab == null) return null;
        
        GameObject effect = Instantiate(effectPrefab, position, rotation);
        
        // Если нужно прикрепить к родителю
        if (attachToParent && attachHitEffectToParent)
        {
            effect.transform.SetParent(transform, true);
            
            // Если у эффекта есть ParticleSystem, отключаем наследование скорости
            var particleSystem = effect.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                var main = particleSystem.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }
        }
        
        activeEffects.Add(effect);
        
        // Автоуничтожение через указанное время
        if (duration > 0)
        {
            Destroy(effect, duration);
            
            // Удаляем из списка после уничтожения
            StartCoroutine(RemoveEffectFromList(effect, duration));
        }
        
        return effect;
    }
    
    // Метод для создания прикрепленного эффекта (например, вытекающей жидкости)
    GameObject CreateAttachedEffect(GameObject effectPrefab, Vector3 localPosition, Quaternion localRotation, float duration)
    {
        if (effectPrefab == null) return null;
        
        GameObject effect = Instantiate(effectPrefab, transform.position + localPosition, localRotation);
        effect.transform.SetParent(transform, false); // Локальная трансформация
        effect.transform.localPosition = localPosition;
        effect.transform.localRotation = localRotation;
        
        activeEffects.Add(effect);
        
        // Если указана длительность, запускаем корутину для уничтожения
        if (duration > 0)
        {
            Coroutine destroyCoroutine = StartCoroutine(DestroyAttachedEffect(effect, duration));
            attachedEffects[effect] = destroyCoroutine;
        }
        
        return effect;
    }
    
    System.Collections.IEnumerator DestroyAttachedEffect(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (effect != null)
        {
            // Если у эффекта есть ParticleSystem, останавливаем эмиссию и ждем завершения
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                
                // Ждем, пока все частицы исчезнут
                while (ps != null && ps.particleCount > 0)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            }
            
            Destroy(effect);
            
            if (attachedEffects.ContainsKey(effect))
                attachedEffects.Remove(effect);
            
            if (activeEffects.Contains(effect))
                activeEffects.Remove(effect);
        }
    }
    
    System.Collections.IEnumerator RemoveEffectFromList(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (activeEffects.Contains(effect))
        {
            activeEffects.Remove(effect);
        }
    }
    
    public void TakeDamage(float damage)
    {
        ReactToBulletHit(transform.position, Vector3.up, damage);
    }
    
    public void ReactToBulletHit(Vector3 hitPoint, Vector3 hitDirection, float force)
    {
        if (isDestroyed) return;

        if (rb.isKinematic) ActivatePhysics();
        
        float appliedForce = force * hitForceMultiplier;
        
        if (appliedForce >= minForceToReact)
        {
            ApplyHitForce(hitPoint, hitDirection, appliedForce);
            PlayHitEffects(hitPoint, hitDirection);
            
            if (canBeDestroyed)
            {
                ApplyDamage(appliedForce);
            }
            
            if (appliedForce > 30f) AddRandomTorque(appliedForce);
        }
    }
    
    void ActivatePhysics()
    {
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.WakeUp();
    }
    
    void ApplyHitForce(Vector3 hitPoint, Vector3 direction, float force)
    {
        rb.AddForceAtPosition(direction.normalized * force, hitPoint, ForceMode.Impulse);
        Vector3 torque = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ) * force * torqueMultiplier;
        rb.AddTorque(torque, ForceMode.Impulse);
    }
    
    void ApplyDamage(float damage)
    {
        currentHealth -= damage;
        
        if (currentHealth <= 0 && !isDestroyed)
        {
            DestroyObject();
        }
    }
    
    void PlayHitEffects(Vector3 hitPoint, Vector3 hitDirection)
    {
        // Создаем эффект попадания
        if (hitEffectPrefab != null)
        {
            // Вычисляем локальную позицию для эффекта
            Vector3 localHitPoint = transform.InverseTransformPoint(hitPoint);
            
            // Вычисляем поворот для эффекта (перпендикулярно поверхности)
            Quaternion effectRotation = Quaternion.LookRotation(-hitDirection, Vector3.up);
            Quaternion localEffectRotation = Quaternion.Inverse(transform.rotation) * effectRotation;
            
            // Создаем эффект как дочерний объект
            CreateAttachedEffect(hitEffectPrefab, localHitPoint, localEffectRotation, hitEffectDuration);
        }
        
        // Проигрываем звук попадания
        if (hitSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.PlayOneShot(hitSound, Mathf.Clamp01(rb.linearVelocity.magnitude / 10f));
        }
    }
    
    void AddRandomTorque(float force)
    {
        Vector3 randomTorque = new Vector3(
            Random.Range(-5f, 5f),
            Random.Range(-5f, 5f),
            Random.Range(-5f, 5f)
        ) * force * 0.1f;
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }
    
    void DestroyObject()
    {
        if (isDestroyed) return;
        isDestroyed = true;
        
        // Останавливаем все прикрепленные эффекты (например, вытекание жидкости)
        StopAllAttachedEffects();
        
        // Отключаем физику и визуал
        if (objectCollider != null) objectCollider.enabled = false;
        if (objectRenderer != null) objectRenderer.enabled = false;
        if (rb != null) rb.isKinematic = true;
        
        // Если это взрывающийся объект
        if (isExplosive)
        {
            CreateExplosion();
        }
        else
        {
            PlayDestructionEffects();
            
            switch (destructionMode)
            {
                case DestructionMode.Disappear:
                    Destroy(gameObject, 0.5f); // Даем время на эффекты
                    break;
                case DestructionMode.BreakApart:
                    CreateBrokenPieces();
                    Destroy(gameObject, 0.5f);
                    break;
            }
        }
    }
    
    void StopAllAttachedEffects()
    {
        // Останавливаем и уничтожаем все прикрепленные эффекты
        foreach (var kvp in attachedEffects)
        {
            if (kvp.Key != null)
            {
                ParticleSystem ps = kvp.Key.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                Destroy(kvp.Key, 1f); // Даем время на завершение анимации
            }
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);
        }
        attachedEffects.Clear();
    }
    
    void CreateExplosion()
    {
        // Создаем эффект взрыва (не прикрепленный)
        CreateEffect(explosionEffectPrefab, transform.position, Quaternion.identity, explosionDuration);
        
        // Звук взрыва
        if (explosionSound != null)
            AudioSource.PlayClipAtPoint(explosionSound, transform.position, 1f);
        
        // Применяем физическую силу взрыва
        ApplyExplosionPhysics();
        
        // Наносим урон всем в радиусе
        DealExplosionDamage();
        
        // Создаем осколки если нужно
        if (destructionMode == DestructionMode.BreakApart)
            CreateBrokenPieces();
        
        // Уничтожаем объект с небольшой задержкой
        Destroy(gameObject, 0.5f);
    }
    
    void ApplyExplosionPhysics()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, 
            explosionRadius, 
            explosionAffectedLayers
        );
        
        foreach (Collider hit in colliders)
        {
            Rigidbody hitRb = hit.GetComponent<Rigidbody>();
            if (hitRb != null && hitRb != rb)
            {
                hitRb.AddExplosionForce(
                    explosionForce,
                    transform.position,
                    explosionRadius,
                    3f,
                    ForceMode.Impulse
                );
                
                PhysicsReactObject physObj = hit.GetComponent<PhysicsReactObject>();
                if (physObj != null && causeChainReaction)
                {
                    physObj.ApplyExplosionForce(explosionForce * 0.5f, transform.position, explosionRadius);
                }
            }
        }
    }
    
    void DealExplosionDamage()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, 
            explosionRadius, 
            explosionAffectedLayers
        );
        
        foreach (Collider hit in colliders)
        {
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            float damageMultiplier = 1f - Mathf.Clamp01(distance / explosionRadius);
            float calculatedDamage = explosionDamage * damageMultiplier;
            
            Health healthComponent = hit.GetComponent<Health>();
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(calculatedDamage);
            }
            
            if (causeChainReaction)
            {
                PhysicsReactObject physObj = hit.GetComponent<PhysicsReactObject>();
                if (physObj != null && physObj != this && !physObj.isDestroyed)
                {
                    physObj.ApplyDamage(calculatedDamage * 0.5f);
                    
                    Vector3 direction = (hit.transform.position - transform.position).normalized;
                    physObj.ReactToBulletHit(
                        hit.ClosestPoint(transform.position),
                        direction,
                        explosionForce * damageMultiplier * 0.3f
                    );
                }
            }
        }
    }
    
    void PlayDestructionEffects()
    {
        // Для разрушения создаем эффект в мировых координатах
        CreateEffect(hitEffectPrefab, transform.position, Quaternion.identity, hitEffectDuration);
        
        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position, 0.5f);
    }
    
    void CreateBrokenPieces()
    {
        if (brokenPieces == null || brokenPieces.Length == 0) return;
        
        int piecesToCreate = Mathf.Min(piecesCount, brokenPieces.Length);
        float pieceForce = isExplosive ? explosionForce * 0.3f : piecesExplosionForce;
        
        for (int i = 0; i < piecesToCreate; i++)
        {
            GameObject piecePrefab = brokenPieces[Random.Range(0, brokenPieces.Length)];
            if (piecePrefab != null)
            {
                Vector3 randomOffset = Random.insideUnitSphere * 0.3f;
                GameObject piece = Instantiate(piecePrefab, transform.position + randomOffset, Random.rotation);
                SetupPiecePhysics(piece, pieceForce);
                
                // Автоуничтожение осколков
                Destroy(piece, piecesLifetime);
            }
        }
    }
    
    void SetupPiecePhysics(GameObject piece, float force)
    {
        Rigidbody pieceRb = piece.GetComponent<Rigidbody>();
        if (pieceRb == null) pieceRb = piece.AddComponent<Rigidbody>();
        
        pieceRb.mass = rb.mass / piecesCount;
        pieceRb.linearDamping = 0.1f;
        pieceRb.angularDamping = 0.05f;
        
        pieceRb.AddExplosionForce(
            force,
            transform.position,
            explosionRadius,
            1f,
            ForceMode.Impulse
        );
        
        pieceRb.AddTorque(Random.insideUnitSphere * force * 0.3f, ForceMode.Impulse);
    }
    
    public void ApplyExplosionForce(float force, Vector3 explosionPos, float radius)
    {
        if (isDestroyed) return;
        
        if (rb.isKinematic) ActivatePhysics();
        
        rb.AddExplosionForce(force, explosionPos, radius, 1f, ForceMode.Impulse);
        
        if (canBeDestroyed)
        {
            float distance = Vector3.Distance(transform.position, explosionPos);
            float damageMultiplier = 1f - Mathf.Clamp01(distance / radius);
            ApplyDamage(force * 0.05f * damageMultiplier);
            
            CreateEffect(hitEffectPrefab, transform.position, Quaternion.identity, hitEffectDuration);
        }
    }
    
    public void TriggerExplosion()
    {
        if (!isDestroyed && isExplosive)
        {
            currentHealth = 0;
            DestroyObject();
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (rb.IsSleeping()) rb.WakeUp();
        
        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce > 3f && !isDestroyed && audioSource != null && hitSound != null)
        {
            audioSource.pitch = Random.Range(0.8f, 1.2f);
            audioSource.PlayOneShot(hitSound, Mathf.Clamp01(impactForce / 15f));
        }
        
        if (isExplosive && !isDestroyed && impactForce > 10f)
        {
            ApplyDamage(impactForce * 2f);
        }
    }
    
    void OnCollisionStay(Collision collision)
    {
        if (rb.linearVelocity.magnitude < 0.01f && rb.angularVelocity.magnitude < 0.01f)
        {
            rb.Sleep();
        }
    }
    
    [ContextMenu("Test Hit Reaction")]
    public void TestHitReaction() => ReactToBulletHit(transform.position + Vector3.up * 0.5f, Vector3.forward, 20f);
    
    [ContextMenu("Test Destruction")] 
    public void TestDestruction()
    {
        if (canBeDestroyed)
        {
            currentHealth = 0;
            DestroyObject();
        }
    }
    
    [ContextMenu("Trigger Explosion")]
    public void TestExplosion() => TriggerExplosion();

    #if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (isExplosive)
        {
            Gizmos.color = new Color(1, 0.3f, 0, 0.5f);
            Gizmos.DrawSphere(transform.position, explosionRadius);
            
            Gizmos.color = new Color(1, 0.6f, 0, 0.3f);
            Gizmos.DrawSphere(transform.position, explosionRadius * 0.5f);
        }
    }
    #endif
}