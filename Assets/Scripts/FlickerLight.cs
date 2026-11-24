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

    
    private float randomizer;
    private bool isBurning = true;

    public bool IsBurnedOut => !isBurning;
    // On its last stage: the next step puts it out (CandleSoundscape plays a faster crackle meanwhile)
    public bool IsDying => isBurning && burningOut && sprites != null && sprites.Length > 1 && stage >= sprites.Length - 2;

    private int stage; // index of the sprite being shown
    public event System.Action BurnedOut;

    void Start()
    {
        randomizer = Random.Range(0f, 100f);
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
            // Burn time only passes during waves (see WaveSpawner.CandlesBurn)
            float burnt = 0f;
            while (burnt < timeBetweenChanges)
            {
                yield return null;
                if (WaveSpawner.CandlesBurn) burnt += Time.deltaTime;
            }
            
            if (frameAnimator != null) frameAnimator.SetStage(i);
            else spriteRenderer.sprite = sprites[i];
            stage = i;

            maxIntensity -= decreaseFactor;
            minIntensity -= decreaseFactor;
            
            if (minIntensity < 0f) minIntensity = 0f;
            if (maxIntensity < 0.2f) maxIntensity = 0.2f;
        }

        isBurning = false;
        lanternLight.intensity = 0f;
        BurnedOut?.Invoke();
    }
}