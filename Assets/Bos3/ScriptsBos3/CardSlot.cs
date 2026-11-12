using UnityEngine;
using UnityEngine.EventSystems;

public class CardSlot : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        // Cek apakah benda yang dijatuhkan adalah kartu
        DraggableCard droppedCard = eventData.pointerDrag.GetComponent<DraggableCard>();
        
        if (droppedCard != null)
        {
            // Cek apakah slot ini masih kosong (belum ada kartu di dalamnya)
            if (transform.childCount == 0) 
            {
                // Beritahu kartu bahwa "rumah" barunya adalah slot ini
                droppedCard.parentAfterDrag = transform;
            }
        }
    }
}