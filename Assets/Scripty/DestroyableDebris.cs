using System.Collections;
using UnityEngine;

// Sedí na rootu prefabu úlomků (např. rozbité dveře poskládané z kusů jako child objekty).
// Destroyable ho spawne na místě zničeného objektu a zavolá Explode – každý kus s rendererem
// dostane Rigidbody (+ konvexní MeshCollider, když collider nemá), odletí od místa úderu
// a po čase se zmenší a smaže.
public class DestroyableDebris : MonoBehaviour
{
    [Header("Fyzika kusů")]
    [Tooltip("Hmotnost jednoho kusu, když ho Rigidbody teprve přidáváme (existující Rigidbody se nemění).")]
    [SerializeField] private float pieceMass = 2f;
    [Tooltip("Síla výbuchu z místa úderu – ať se kusy rozletí.")]
    [SerializeField] private float explosionForce = 150f;
    [Tooltip("Poloměr výbuchu (m). Kusy dál od místa úderu dostanou menší sílu.")]
    [SerializeField] private float explosionRadius = 2f;
    [Tooltip("Zvednutí výbuchu – kladné = kusy víc vyskočí nahoru.")]
    [SerializeField] private float upwardsModifier = 0.3f;
    [Tooltip("Dodatečný impuls ve směru úderu (od hráče), na kus.")]
    [SerializeField] private float pushImpulse = 1.5f;
    [Tooltip("Náhodná rotace kusů (rad/s), ať nepadají všechny stejně.")]
    [SerializeField] private float randomTorque = 3f;

    [Header("Zmizení")]
    [Tooltip("Za kolik sekund kusy začnou mizet. 0 = zůstanou ležet navždy.")]
    [SerializeField] private float lifetime = 6f;
    [Tooltip("Jak dlouho se kusy zmenšují do ztracena, než se smažou.")]
    [SerializeField] private float shrinkDuration = 1f;

    private Rigidbody[] pieces;

    // point = místo úderu, direction = směr úderu (od hráče do objektu).
    public void Explode(Vector3 point, Vector3 direction)
    {
        pieces = SetupPieces();

        foreach (Rigidbody rb in pieces)
        {
            rb.AddExplosionForce(explosionForce, point, explosionRadius, upwardsModifier);
            rb.AddForce(direction * pushImpulse, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * randomTorque, ForceMode.VelocityChange);
        }

        if (lifetime > 0f)
            StartCoroutine(DespawnRoutine());
    }

    // Každý kus s MeshRendererem dostane Rigidbody a collider, pokud je ještě nemá.
    private Rigidbody[] SetupPieces()
    {
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        Rigidbody[] result = new Rigidbody[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            GameObject piece = renderers[i].gameObject;

            if (piece.GetComponent<Collider>() == null)
            {
                MeshCollider col = piece.AddComponent<MeshCollider>();
                col.convex = true; // nekonvexní MeshCollider s dynamickým Rigidbody nefunguje
            }

            Rigidbody rb = piece.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = piece.AddComponent<Rigidbody>();
                rb.mass = pieceMass;
            }
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            result[i] = rb;
        }

        return result;
    }

    private IEnumerator DespawnRoutine()
    {
        yield return new WaitForSeconds(lifetime);

        Vector3[] startScales = new Vector3[pieces.Length];
        for (int i = 0; i < pieces.Length; i++)
            if (pieces[i] != null) startScales[i] = pieces[i].transform.localScale;

        for (float t = 0f; t < shrinkDuration; t += Time.deltaTime)
        {
            float k = 1f - t / shrinkDuration;
            for (int i = 0; i < pieces.Length; i++)
                if (pieces[i] != null) pieces[i].transform.localScale = startScales[i] * k;
            yield return null;
        }

        Destroy(gameObject);
    }
}
