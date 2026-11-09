using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FlickerLight : MonoBehaviour
{
    [Header("Light Settings")]
    public Light2D lanternLight;
    public float minIntensity = 0.5f;
    public float maxIntensity = 1.2f;
    public float flickerSpeed = 5f;
    public float decreaseFactor = 0.4f;
    public bool burningOut = false;

    [Header("Sprite Settings")]
    public SpriteRenderer spriteRenderer;
    public Sprite[] sprites;
    public float timeBetweenChanges = 5f;
    public SpriteFrameAnimator frameAnimator; // if set, burn-out stages switch its looping animation instead of a single sprite
    [Header("Wax puddle (see WaxPuddle)")]
    public float cleanFadeTime = 0.35f;

    // State change: true once the candle has burned down to a puddle (no light, no damage)
    public bool IsOut { get; private set; }
    public event Action<FlickerLight> BurnedOut;

    private float randomizer;
    private bool isBurning = true;
    private float startMin, startMax;

    void Awake()
    {
        startMin = minIntensity;
        startMax = maxIntensity;
    }

    void Start()
    {
        randomizer = UnityEngine.Random.Range(0f, 100f);
        if (burningOut)
        {
            StartCoroutine(BurnOutRoutine());
        }
    }

    void Update()
    {
        if (!isBurning) return;

        float noise = Mathf.PerlinNoise(randomizer, Time.time * flickerSpeed);
        lanternLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }

    private IEnumerator BurnOutRoutine()
    {
        for (int i = 1; i < sprites.Length; i++)
        {
            yield return new WaitForSeconds(timeBetweenChanges);

            SetStage(i);

            maxIntensity -= decreaseFactor;
            minIntensity -= decreaseFactor;

            if (minIntensity < 0f) minIntensity = 0f;
            if (maxIntensity < 0.2f) maxIntensity = 0.2f;
        }

        isBurning = false;
        lanternLight.intensity = 0f;

        IsOut = true;
        BurnedOut?.Invoke(this);
    }

    void SetStage(int i)
    {
        if (frameAnimator != null) frameAnimator.SetStage(i);
        else spriteRenderer.sprite = sprites[i];
    }

    // Puddle -> full candle again, and it starts burning down from the top
    public void Relight()
    {
        if (!IsOut) return;
        IsOut = false;
        isBurning = true;
        minIntensity = startMin;
        maxIntensity = startMax;
        SetStage(0);
        StopAllCoroutines();
        StartCoroutine(BurnOutRoutine());
    }

    // Puddle is wiped away: fade it out, then remove the object
    public void CleanUp()
    {
        StopAllCoroutines();
        isBurning = false;
        if (frameAnimator != null) frameAnimator.enabled = false; // so it doesn't keep resetting the sprite
        StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        Color c = spriteRenderer.color;
        for (float t = 0f; t < cleanFadeTime; t += Time.deltaTime)
        {
            spriteRenderer.color = new Color(c.r, c.g, c.b, c.a * (1f - t / cleanFadeTime));
            yield return null;
        }
        Destroy(gameObject);
    }
}