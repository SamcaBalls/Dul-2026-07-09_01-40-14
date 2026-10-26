using UnityEngine;

// Data jednoho úderu nástrojem (sekyra…), které dostane zničitelný objekt.
public struct ToolHit
{
    public Vector3 point;  // bod dopadu raycastu
    public Vector3 normal; // normála povrchu
    public int damage;     // kolik HP úder ubere (sekyra = 1)
}

// Jediné, co nástroj „vidí" – sekyra nezná konkrétní typy zničitelných objektů.
public interface IDestroyable
{
    void TakeHit(ToolHit hit);
}

// Primární použití drženého nástroje. Když ho držený item má, PlayerEquipment
// zavolá OnPrimaryUse() místo přímého HeldItemAnimator.TryPrimaryUse() –
// nástroj sám rozhodne, jestli švihnout.
public interface IPrimaryUse
{
    void OnPrimaryUse();
}
