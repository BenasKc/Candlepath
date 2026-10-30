using UnityEngine;

// A spark fired by CandleSpark. It flies in a STRAIGHT line to where the target will be
// (leads the shot using PathFollower.Velocity), so the trail doesn't bend.
// The hit always lands if the target is still alive when the spark arrives.
//
// Children (both optional):
//   Trail - ParticleSystem, Simulation Space World, Rate over Distance. Detached on impact so it fades out.
//   Hit   - ParticleSystem, Play On Awake OFF, one Burst. Detached and played on impact.
public class SparkProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private ParticleSystem trail;
    [SerializeField] private ParticleSystem hitBurst;

    private EnemyHealth target;
    private float damage;
    private Vector3 aimPoint;

    public void Launch(EnemyHealth target, float damage)
    {
        this.target = target;
        this.damage = damage;

        // Lead the shot: predict where the target will be when the spark gets there
        Vector3 targetPos = target.transform.position;
        aimPoint = targetPos;
        PathFollower mover = target.GetComponent<PathFollower>();
        if (mover != null)
        {
            for (int i = 0; i < 2; i++) // two passes are plenty for slow enemies
            {
                float travelTime = Vector3.Distance(transform.position, aimPoint) / speed;
                aimPoint = targetPos + mover.Velocity * travelTime;
            }
        }
        aimPoint.z = transform.position.z;
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, aimPoint, speed * Time.deltaTime);
        if ((transform.position - aimPoint).sqrMagnitude > 0.0001f) return;

        // Arrived. The target may already be dead (another candle got it), then it just fizzles.
        bool hit = target != null;
        if (hit) target.TakeDamage(damage);
        Finish(hit);
    }

    void Finish(bool hit)
    {
        if (trail != null)
        {
            trail.transform.SetParent(null);
            trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(trail.gameObject, trail.main.startLifetime.constantMax + 0.1f);
        }

        if (hit && hitBurst != null)
        {
            hitBurst.transform.SetParent(null);
            hitBurst.Play();
            Destroy(hitBurst.gameObject, hitBurst.main.duration + hitBurst.main.startLifetime.constantMax);
        }

        Destroy(gameObject);
    }
}