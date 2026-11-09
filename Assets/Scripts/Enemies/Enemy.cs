using UnityEngine;
using System;

[RequireComponent(typeof(PathFollower))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private int lampDamage = 1;
    [SerializeField] private int waxReward = 5;

    public int LampDamage => lampDamage;
    public int WaxReward => waxReward;
    public PathFollower Movement { get; private set; }

    public event Action<Enemy> Killed;
    public event Action<Enemy> ReachedLamp;
    public event Action<Enemy> Removed; // fires exactly once, for either reason
    public static event Action<Enemy> AnyReachedLamp;

    private bool removed;

    void Awake()
    {
        Movement = GetComponent<PathFollower>();
        Movement.ReachedEnd += ReachLamp;
    }

    public void Kill()
    {
        if (removed) return;
        Killed?.Invoke(this);
        Remove();
    }


    void ReachLamp()
    {
        if (removed) return;
        ReachedLamp?.Invoke(this);
        AnyReachedLamp?.Invoke(this);
        Remove();
    }

    void Remove()
    {
        removed = true;
        Removed?.Invoke(this);
        Destroy(gameObject);
    }

}
