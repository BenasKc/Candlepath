using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CandleSpark : MonoBehaviour
{
    [SerializeField] private Light2D candleLight;
    [SerializeField] private SparkProjectile sparkPrefab;
    [SerializeField] private Transform firePoint;          // empty = the candle's own position
    [SerializeField] private float range = 2.6f;           // roughly the Light2D outer radius
    [SerializeField] private float damagePerHit = 4f;
    [SerializeField] private float fireInterval = 1.2f;    // seconds between shots

    public bool IsAttacking { get; private set; }
    public float Range => range;

    private float cooldown;

    void Awake()
    {
        if (candleLight == null) candleLight = GetComponent<Light2D>();
        if (firePoint == null) firePoint = transform;
    }

    void Update()
    {
        if (cooldown > 0f) cooldown -= Time.deltaTime;

        // A burned-out candle (intensity 0, see FlickerLight) can't shoot
        EnemyHealth target = candleLight.intensity > 0.05f ? FindNearest() : null;
        IsAttacking = target != null;
        if (target == null || cooldown > 0f) return;

        SparkProjectile spark = Instantiate(sparkPrefab, firePoint.position, Quaternion.identity);
        spark.Launch(target, damagePerHit);
        Sfx.SparkFired(firePoint.position);
        cooldown = fireInterval;
    }

    EnemyHealth FindNearest()
    {
        EnemyHealth best = null;
        float bestDist = range * range;
        foreach (EnemyHealth e in EnemyHealth.activeEnemies)
        {
            float d = (e.transform.position - transform.position).sqrMagnitude;
            if (d <= bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}