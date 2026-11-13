using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardBos4 : MonoBehaviour
{
    public int cardValue;
    public TextMeshProUGUI valueText;
    public Button cardButton;
    
    private Vector3 targetPosition;
    private float moveSpeed = 10f;
    private bool isMoving = false;

    public Transform originalHandSlot; 
    public bool isInTargetSlot = false; // Penanda lokasi kartu

    public void Initialize(int val, Transform startSlot)
    {
        cardValue = val;
        // Jika nanti pakai gambar kosong tanpa angka, Anda bisa mematikan/menghapus baris valueText ini:
        if(valueText != null) valueText.text = val.ToString(); 
        
        originalHandSlot = startSlot;
        isInTargetSlot = false;
        
        transform.SetParent(startSlot);
        transform.localPosition = Vector3.zero;
        targetPosition = transform.position;
        
        cardButton.onClick.RemoveAllListeners();
        cardButton.onClick.AddListener(OnClick);
    }

    private void Update()
    {
        if (isMoving)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, moveSpeed * Time.deltaTime);
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                isMoving = false;
            }
        }
    }

    public void MoveToTarget(Transform newParentTarget)
    {
        transform.SetParent(newParentTarget);
        targetPosition = newParentTarget.position;
        isMoving = true;
    }

    public void ReturnToHand()
    {
        MoveToTarget(originalHandSlot);
        isInTargetSlot = false;
    }

    private void OnClick()
    {
        GameManagerBos4.Instance.OnCardClicked(this);
    }

    public void SetInteractable(bool state)
    {
        cardButton.interactable = state;
    }
}