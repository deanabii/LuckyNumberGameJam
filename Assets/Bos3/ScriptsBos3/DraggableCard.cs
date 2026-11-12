using UnityEngine;
using UnityEngine.EventSystems; // Wajib untuk mendeteksi mouse

public class DraggableCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public Transform parentAfterDrag;
    private CanvasGroup canvasGroup;

    private void Awake()
    {
        // Menambahkan CanvasGroup otomatis agar kartu bisa "tembus pandang" dari mouse saat ditarik
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        parentAfterDrag = transform.parent; // Ingat dari mana kartu ini berasal
        transform.SetParent(transform.root); // Angkat kartu ke paling depan (Canvas)
        transform.SetAsLastSibling(); 
        canvasGroup.blocksRaycasts = false; // Matikan deteksi mouse ke kartu agar slot di bawahnya bisa terdeteksi
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Menggunakan eventData.position berfungsi otomatis di Input System baru maupun lama, 
        // dan juga langsung mendukung layar sentuh (mobile/Android).
        transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        transform.SetParent(parentAfterDrag); // Kembalikan kartu ke tempat asal atau tempat baru
        transform.localPosition = Vector3.zero; // Pusatkan posisi kartu
        canvasGroup.blocksRaycasts = true; // Nyalakan kembali deteksi mouse
    }
}