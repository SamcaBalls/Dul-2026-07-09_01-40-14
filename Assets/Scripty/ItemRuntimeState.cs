// Stav konkrétního kusu itemu (ne ItemData – ta je sdílená pro všechny kusy).
// Žije na UIInventoryItem, takže přežije zničení modelu v ruce při schování/vytažení
// i přetažení mezi kapsami.
public class ItemRuntimeState
{
    // Glowstick: null = ještě nikdy nedržený (čerstvý kus).
    public GlowSnapshot? glow;
}

public struct GlowSnapshot
{
    public bool isGlowing;
    public bool isSpent;
    public float brightness01;
    public float life01;
}

// Komponenta na drženém prefabu, která si umí uložit/obnovit svůj stav.
// PlayerEquipment ji volá před zničením modelu v ruce a hned po jeho vytvoření.
public interface IPersistentItemState
{
    void SaveState(ItemRuntimeState state);
    void LoadState(ItemRuntimeState state);
}
