using UnityEngine;
using UnityEngine.Events;

// Sedí na objektu s Animatorem drženého itemu (animation eventy se volají jen na něm).
// Klip použití v daném momentu (např. když švih trefí) zavolá PrimaryHit()
// a gameplay skript itemu (např. HeldGlowstick) se navěsí na onPrimaryHit.
public class HeldItemAnimationEvents : MonoBehaviour
{
    [Header("Události z animace")]
    public UnityEvent onPrimaryHit;

    // Volá animation event v klipu primárního použití (Function = PrimaryHit).
    public void PrimaryHit()
    {
        onPrimaryHit?.Invoke();
    }
}
