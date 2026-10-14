using UnityEngine;

// Marker for a roof that blocks the Blind Cave Horror's falling rock.
// The rock effect looks for this component above its target; a player standing under the roof is
// not hit, because the rock lands on top of it instead. Put it on the roof's collider object.
public class RockShelter : MonoBehaviour
{
}
