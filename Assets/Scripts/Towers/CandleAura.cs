using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CandleAura : MonoBehaviour
{
    [SerializeField] private Light2D candleLight;
    [SerializeField] private float radius = 2f;
    [SerializeField] private float damagePerSecond = 4f;
    [SerializeField] private Color attackColor = new Color(1f, 0.45f, 0.15f);
    [SerializeField] private float colorLerpSpeed = 6f;

    public bool IsAttacking { get; private set; }
    public float Radius => radius;

    private Color idleColor;

    void Awake()
    {
        if (candleLight == null) candleLight = GetComponent<Light2D>();
        idleColor = candleLight.color;
    }

    void Update()
    {
        // Light intensity drops as the candle burns down (FlickerLight), and is 0 when it's out,
        // so a dying candle burns weaker and a dead one doesn't burn at all
        float strength = candleLight.intensity;
        IsAttacking = false;

        if (strength > 0.05f)
        {
            float r2 = radius * radius;
            for (int i = EnemyHealth.activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyHealth e = EnemyHealth.activeEnemies[i];
                Vector2 offset = e.transform.position - transform.position;
                if (offset.sqrMagnitude > r2) continue;

                e.TakeDamage(damagePerSecond * strength * Time.deltaTime);
                IsAttacking = true;
            }
        }

        // State change: the flame flares orange while it's burning shadows
        Color target = IsAttacking ? attackColor : idleColor;
        candleLight.color = Color.Lerp(candleLight.color, target, colorLerpSpeed * Time.deltaTime);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}