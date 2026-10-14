using UnityEngine;

// One owned cave effect prefab handles a fixed warning/rock or forward sonic rings.
public class BossCaveEffect : MonoBehaviour
{
    [SerializeField] SpriteRenderer warning;
    [SerializeField] SpriteRenderer rock;
    [SerializeField] SpriteRenderer[] waves;
    readonly Collider2D[] hits = new Collider2D[32];
    readonly Rigidbody2D[] unique = new Rigidbody2D[32];
    Boss owner; BossSound sound; bool sonic, charge, struck; float age, direction, shelterTop;
    public void Initialize(Boss source, BossSound audio, bool isSonic, bool isCharge, float facing)
    {
        owner = source; sound = audio; sonic = isSonic; charge = isCharge; direction = facing;
        if (!sonic && !charge) shelterTop = FindShelterTop();
        warning.enabled = !sonic; warning.transform.localScale = new Vector3((charge ? 8 : 1.8f)/warning.sprite.bounds.size.x, .12f/warning.sprite.bounds.size.y, 1);
        warning.color = charge ? Color.red : new Color(1,.6f,.1f,.8f); rock.enabled = false; rock.transform.localScale = Vector3.one*(1.8f/rock.sprite.bounds.size.x);
        foreach (var wave in waves) wave.enabled = false;
    }
    // Fixed target position, one impact only; the controller owns cancellation on stagger/death.
    void Update()
    {
        if (!owner || owner.IsDead) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        if (charge) return;
        if (sonic)
        {
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i]; wave.enabled = age >= .5f && age < 1.7f;
                float t = Mathf.Repeat((age - .5f) * 1.4f + i / (float)waves.Length, 1);
                wave.transform.localPosition = new Vector3(direction * t * 6.5f, 0, 0);
                wave.transform.localScale = new Vector3((.3f + t)/wave.sprite.bounds.size.x, (.5f + t * 1.7f)/wave.sprite.bounds.size.y, 1);
                wave.color = new Color(.75f,.6f,1,1-t);
            }
        }
        else
        {
            // Under a RockShelter roof the rock stops on top of it (shelterTop is height above the floor).
            float endY = Mathf.Max(1.2f, shelterTop + .9f), startY = Mathf.Max(3, endY + 1.8f);
            rock.enabled = age >= .6f; rock.transform.localPosition = Vector3.up * (Mathf.Lerp(startY, endY, Mathf.Clamp01((age-.6f)/.2f)));
            if (!struck && age >= .8f) { struck = true; warning.enabled = false; Impact(); }
            if (struck) { rock.color = new Color(1,1,1,Mathf.Clamp01((1.4f-age)/.6f)); rock.transform.localScale = Vector3.one * (1.8f + (age-.8f)); }
        }
        if (age >= (sonic ? 1.9f : 1.4f)) Destroy(gameObject);
    }
    // Finds the roof (RockShelter) directly above the rock's target and returns its top height above the floor, or 0 if none.
    float FindShelterTop()
    {
        var filter = new ContactFilter2D(); filter.NoFilter();
        int count = Physics2D.OverlapBox((Vector2)transform.position + Vector2.up * 2.5f, new Vector2(.4f, 5f), 0, filter, hits);
        float top = 0;
        for (int i = 0; i < count; i++)
        {
            if (!hits[i].TryGetComponent<RockShelter>(out _)) continue;
            float height = hits[i].bounds.max.y - transform.position.y;
            if (top == 0 || height < top) top = height;
        }
        return top;
    }
    // Rock visual fall has no damage: impact deduplicates each player's rigidbody once.
    // With a roof above the target the hit box moves onto the roof, so only a player standing on top is hit.
    void Impact()
    {
        if (sound) sound.Play(BossSfx.CollapseImpact);
        int count = Physics2D.OverlapBoxNonAlloc((Vector2)transform.position + Vector2.up * (shelterTop + 1.2f), new Vector2(1.8f,2.4f),0,hits);
        int used=0;
        for(int i=0;i<count;i++)
        {
            var rb=hits[i].attachedRigidbody; if(!rb || !rb.TryGetComponent<PlayerMovement>(out _) || !rb.TryGetComponent<IDamageable>(out var health)) continue;
            bool duplicate=false; for(int j=0;j<used;j++) if(unique[j]==rb) duplicate=true;
            if(duplicate) continue; unique[used++]=rb; health.TakeDamage(20);
        }
    }
}
