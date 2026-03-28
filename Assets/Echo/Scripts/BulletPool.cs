// BulletPool.cs
using UnityEngine;
using System.Collections.Generic;

public class BulletPool : MonoBehaviour
{
    public static BulletPool Instance { get; private set; }

    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private int initialPoolSize = 200;
    //private static readonly int MaxPooledBullets = 300;
    private readonly Queue<Bullet> pool = new();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            GameObject obj = Instantiate(bulletPrefab, transform);
            obj.SetActive(false);
            pool.Enqueue(obj.GetComponent<Bullet>());
        }
    }

    public Bullet GetBullet()
    {
        if (pool.Count > 0)
        {
            Bullet bullet = pool.Dequeue();
            bullet.ResetForPool(); // ← СБРОС ФЛАГОВ!
            bullet.gameObject.SetActive(true);
            return bullet;
        }
        else
        {
            GameObject newObj = Instantiate(bulletPrefab, transform);
            newObj.SetActive(true);
            Bullet newBullet = newObj.GetComponent<Bullet>();
            // Новые пули не требуют ResetForPool, т.к. флаги по умолчанию false
            Debug.LogWarning("[BulletPool] Pool exhausted! Consider increasing initial size.");
            return newBullet;
        }
    }

    public void ReturnBullet(Bullet bullet)
    {
        if (bullet != null && bullet.gameObject != null)
        {
            bullet.gameObject.SetActive(false);
            bullet.transform.SetParent(transform);
            pool.Enqueue(bullet);
        }
    }
}