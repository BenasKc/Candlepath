using UnityEngine;
using System.Collections.Generic;

public class PathNode : MonoBehaviour
{

    [SerializeField] private List<PathNode> next = new List<PathNode>();
    
    public PathNode GetNext() {
        if (next.Count == 0) return null;
        return next[Random.Range(0, next.Count)];
    }

    void OnDrawGizmos() {
        Gizmos.color = next.Count == 0 ? Color.red : Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.15f);
        foreach(var n in next) {
            if (n != null) Gizmos.DrawLine(transform.position, n.transform.position);
        }
    }
}
