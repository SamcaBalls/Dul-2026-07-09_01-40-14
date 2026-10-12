using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Předehřátí shaderů itemů: při startu scény na pár snímků vykreslí (neviditelně malé)
// kopie meshů + materiálů všech zadaných itemů. Shader varianta (např. průhledný Lit
// s emisí) se tak zkompiluje hned při načtení, ne až při prvním vytažení itemu → žádný zásek.
// Kopie nemají žádné skripty ani fyziku, jen MeshFilter + MeshRenderer.
public class ShaderPrewarmer : MonoBehaviour
{
    [Tooltip("Itemy, jejichž materiály se mají předehřát (inGamePrefab i worldPrefab).")]
    [SerializeField] private ItemData[] items;
    [Tooltip("Kde se kopie vykreslí – měl by to být bod v záběru kamery (např. handSocket). Prázdné = před Camera.main.")]
    [SerializeField] private Transform renderPoint;
    [Tooltip("Kolik snímků kopie zůstanou vykreslené.")]
    [SerializeField] private int framesToRender = 2;

    private IEnumerator Start()
    {
        Transform parent = renderPoint;
        if (parent == null && PlayerEquipment.Instance != null) parent = PlayerEquipment.Instance.handSocket;
        bool inFrontOfCamera = false;
        if (parent == null && Camera.main != null)
        {
            parent = Camera.main.transform;
            inFrontOfCamera = true;
        }
        if (parent == null || items == null) yield break;

        var root = new GameObject("ShaderPrewarm");
        root.transform.SetParent(parent, false);
        // Musí to být ve frustumu; drobné měřítko = nic není vidět, draw call (a kompilace shaderu) ale proběhne.
        root.transform.localPosition = inFrontOfCamera ? Vector3.forward * 0.5f : Vector3.zero;
        root.transform.localScale = Vector3.one * 0.001f;

        var seen = new HashSet<GameObject>();
        foreach (ItemData item in items)
        {
            if (item == null) continue;
            AddCopies(item.inGamePrefab, root.transform, seen);
            AddCopies(item.worldPrefab, root.transform, seen);
        }

        for (int i = 0; i < Mathf.Max(1, framesToRender); i++)
            yield return null;

        Destroy(root);
    }

    // Pro každý renderer prefabu vytvoří holou kopii (mesh + materiály) pod rootem.
    private static void AddCopies(GameObject prefab, Transform root, HashSet<GameObject> seen)
    {
        if (prefab == null || !seen.Add(prefab)) return;

        foreach (Renderer src in prefab.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null;
            if (src is MeshRenderer)
            {
                MeshFilter mf = src.GetComponent<MeshFilter>();
                if (mf != null) mesh = mf.sharedMesh;
            }
            else if (src is SkinnedMeshRenderer smr)
            {
                mesh = smr.sharedMesh;
            }
            if (mesh == null) continue;

            var copy = new GameObject(src.name);
            copy.layer = src.gameObject.layer; // stejná vrstva = vykreslí ji stejná kamera jako skutečný item
            copy.transform.SetParent(root, false);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = copy.AddComponent<MeshRenderer>();
            mr.sharedMaterials = src.sharedMaterials;
            mr.shadowCastingMode = src.shadowCastingMode;
            mr.receiveShadows = src.receiveShadows;
        }
    }
}
