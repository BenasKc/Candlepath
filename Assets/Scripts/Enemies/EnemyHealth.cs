using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Enemy))]
public class EnemyHealth : MonoBehaviour, IDifficultyScaled
{
    [SerializeField] private float maxHealth = 10f;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Color litColor = new Color(1f, 0.6f, 0.25f); // tint while being burned
    [SerializeField, Range(0f, 1f)] private float minAlpha = 0.3f;        // how faded it gets near death

    public float CurrentHealth { get; private set; }
    public float Fraction => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
    public bool IsLit => litTimer > 0f;

    private Enemy enemy;
    private Color baseColor;
    private float litTimer;
    public static readonly List<EnemyHealth> activeEnemies = new List<EnemyHealth>();

    void OnEnable() => activeEnemies.Add(this);
    void OnDisable() => activeEnemies.Remove(this);

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        baseColor = spriteRenderer.color;
        CurrentHealth = maxHealth;
    }

    // Called by WaveSpawner right after Instantiate
    public void ApplyDifficulty(DifficultySettings settings)
    {
        maxHealth *= settings.EnemyHealthMultiplier;
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f) return;
        CurrentHealth -= amount;
        litTimer = 0.1f;
        if (CurrentHealth <= 0f) enemy.Kill();
    }

    void Update()
    {
        if (litTimer > 0f) litTimer -= Time.deltaTime;

        // State change: the shadow "dissolves" as it loses health and glows while lit
        Color c = IsLit ? Color.Lerp(baseColor, litColor, 0.6f) : baseColor;
        c.a = Mathf.Lerp(minAlpha, 1f, Fraction);
        spriteRenderer.color = c;
    }

    [ContextMenu("Debug: take 3 damage")]
    void DebugDamage() => TakeDamage(3f);
}
