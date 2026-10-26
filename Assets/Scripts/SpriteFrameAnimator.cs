using System;
using UnityEngine;

// Plays looping sprite frames on a SpriteRenderer. Each stage is its own frame set, so burning candles can
// switch to the next wax-height animation (see FlickerLight). Uses scaled time, so pause and game speed apply.
public class SpriteFrameAnimator : MonoBehaviour
{
    [Serializable]
    public class Stage
    {
        public Sprite[] frames;
        public float fps = 10f;
    }

    public SpriteRenderer target;
    public Stage[] stages;
    public bool randomizeStart = true; // desyncs objects that share the same animation

    private int stage;
    private float time;

    void Awake()
    {
        if (target == null) target = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        time = randomizeStart ? UnityEngine.Random.value * 10f : 0f;
        Apply();
    }

    void Update()
    {
        time += Time.deltaTime;
        Apply();
    }

    public void SetStage(int index)
    {
        if (stages == null || stages.Length == 0) return;
        stage = Mathf.Clamp(index, 0, stages.Length - 1);
        Apply();
    }

    void Apply()
    {
        if (target == null || stages == null || stages.Length == 0) return;
        Stage s = stages[stage];
        if (s.frames == null || s.frames.Length == 0) return;

        int i = s.frames.Length > 1 ? (int)(time * s.fps) % s.frames.Length : 0;
        if (target.sprite != s.frames[i]) target.sprite = s.frames[i];
    }
}
