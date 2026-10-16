using UnityEngine;
// AI work record: Codex; model unknown; 2026-10-16.
// A dead flying patrol falls vertically; it cannot drift into a platform travel corridor.
[RequireComponent(typeof(MonsterStats),typeof(Rigidbody2D))]
public class MonsterCorpseDriftStop : MonoBehaviour
{
    private MonsterStats stats;
    private Rigidbody2D body;
    // Subscribe once; live patrol and player combat remain unchanged.
    private void Awake(){stats=GetComponent<MonsterStats>();body=GetComponent<Rigidbody2D>();stats.OnDeath+=StopDrift;}
    // MonsterStats already disables colliders and applies death gravity before this event.
    private void StopDrift(){body.linearVelocity=new Vector2(0,Mathf.Min(0,body.linearVelocity.y));}
    private void OnDestroy(){if(stats!=null)stats.OnDeath-=StopDrift;}
}
