using UnityEngine;

// One owned cave effect prefab handles a fixed warning/rock or forward sonic waves.
// All visuals are generated pixel-art sprites (VARCO); no flat-coloured boxes are drawn for the hit areas.
public class BossCaveEffect : MonoBehaviour
{
    [SerializeField] SpriteRenderer warning;
    [SerializeField] SpriteRenderer rock;
    [SerializeField] SpriteRenderer[] waves;
    [SerializeField] Sprite chargeWarningSprite; // ground scrape streak shown while the charge winds up
    readonly Collider2D[] hits = new Collider2D[32];
    readonly Rigidbody2D[] unique = new Rigidbody2D[32];
    Boss owner; BossSound sound; bool sonic, charge, struck; float age, direction, shelterTop, rockScale;
    const float WaveWidth = 4.5f, WaveTravel = .4f; // wave sprite width (height ~2.2 = roar box) and seconds per pass
    public void Initialize(Boss source, BossSound audio, bool isSonic, bool isCharge, float facing)
    {
        owner = source; sound = audio; sonic = isSonic; charge = isCharge; direction = facing;
        if (!sonic && !charge) shelterTop = FindShelterTop();
        warning.enabled = !sonic;
        if (charge && chargeWarningSprite) warning.sprite = chargeWarningSprite;
        // Ground decal: keep the artwork's proportions and rest its bottom edge on the floor (the effect sits on the floor).
        float warningScale = (charge ? 8f : 2.6f) / warning.sprite.bounds.size.x;
        warning.transform.localScale = new Vector3(charge ? direction * warningScale : warningScale, warningScale, 1);
        warning.transform.localPosition = new Vector3(0, warning.sprite.bounds.size.y * warningScale * .5f, 0);
        warning.color = Color.white; rock.enabled = false;
        rockScale = 1.8f / rock.sprite.bounds.size.x; rock.transform.localScale = Vector3.one * rockScale;
        foreach (var wave in waves) wave.enabled = false;
    }
    // Fixed target position, one impact only; the controller owns cancellation on stagger/death.
    void Update()
    {
        if (!owner || owner.IsDead) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        // Warning artwork pulses so the danger spot reads clearly without a flat colour.
        if (warning.enabled) warning.color = new Color(1, 1, 1, Mathf.Lerp(.65f, 1f, Mathf.PingPong(age * 4f, 1f)));
        if (charge) return;
        if (sonic)
        {
            // One wave per damage tick (0.5s and 1.5s); the wave front covers the 6.5-long hit box.
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i]; float start = .5f + i;
                bool active = i < 2 && age >= start && age < start + WaveTravel; wave.enabled = active;
                if (!active) continue;
                float t = (age - start) / WaveTravel, scale = WaveWidth / wave.sprite.bounds.size.x;
                wave.transform.localPosition = new Vector3(direction * Mathf.Lerp(1f, 4.4f, t), 0, 0);
                wave.transform.localScale = new Vector3(direction * scale, scale, 1);
                wave.color = new Color(1, 1, 1, Mathf.Clamp01(1.4f - t));
            }
        }
        else
        {
            // Under a RockShelter roof the rock stops on top of it (shelterTop is height above the floor).
            float endY = shelterTop > 0 ? shelterTop + 1.04f : 1.2f, startY = Mathf.Max(3, endY + 1.8f);
            rock.enabled = age >= .6f; rock.transform.localPosition = Vector3.up * (Mathf.Lerp(startY, endY, Mathf.Clamp01((age-.6f)/.2f)));
            if (!struck && age >= .8f) { struck = true; warning.enabled = false; Impact(); }
            if (struck) { rock.color = new Color(1,1,1,Mathf.Clamp01((1.4f-age)/.6f)); rock.transform.localScale = Vector3.one * rockScale * (1f + (age - .8f) * .5f); }
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
