using UnityEngine;
using UnityEngine.EventSystems;

public class GridDropZone : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        // Tangkap kartu yang dijatuhkan
        DraggableCard droppedCard = eventData.pointerDrag.GetComponent<DraggableCard>();
        if (droppedCard != null)
        {
            // Jadikan CardGrid sebagai rumah barunya
            droppedCard.parentAfterDrag = transform;
        }
    }
}