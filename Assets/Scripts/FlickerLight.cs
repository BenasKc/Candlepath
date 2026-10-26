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
            yield return new WaitForSeconds(timeBetweenChanges);
            
            if (frameAnimator != null) frameAnimator.SetStage(i);
            else spriteRenderer.sprite = sprites[i];

            maxIntensity -= decreaseFactor;
            minIntensity -= decreaseFactor;
            
            if (minIntensity < 0f) minIntensity = 0f;
            if (maxIntensity < 0.2f) maxIntensity = 0.2f;
        }

        isBurning = false;
        lanternLight.intensity = 0f; 
    }
}