using UnityEngine;
using System;

public class PathFollower : MonoBehaviour

{
    [SerializeField] private float speed = 1.5f;
    public event Action ReachedEnd;
    public Vector3 Velocity { get; private set; }

    private PathNode target;

    public void Begin(PathNode start, float speedMultiplier = 1f)
    {
        speed *= speedMultiplier;
        transform.position = start.transform.position;
        target = start.GetNext();
    }

    void Update()
    {
        if (target == null) { Velocity = Vector3.zero; return; }
        Vector3 before = transform.position; 

        transform.position = Vector3.MoveTowards(transform.position, target.transform.position, speed * Time.deltaTime);
        if (Time.deltaTime > 0f) Velocity = (transform.position - before) / Time.deltaTime;
        if ((transform.position - target.transform.position).sqrMagnitude < 0.0001f)
        {
            target = target.GetNext();
            if (target == null)
                ReachedEnd?.Invoke();
        }
    }
}
