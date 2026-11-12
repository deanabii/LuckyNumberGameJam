using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardController : MonoBehaviour
{
    [Header("Identitas Kartu")]
    public int cardValue;
    public bool isFlipped = false;
    public bool isMatched = false;
    public bool isAnimating = false; // Mengunci klik saat animasi berjalan

    [Header("Referensi Gambar (Sprite)")]
    public Sprite cardClosedSprite; // Masukkan gambar kristal tertutup di sini
    public Sprite cardOpenSprite;   // Masukkan gambar bingkai putih kosong di sini

    [Header("Referensi Komponen UI")]
    [SerializeField] private Image cardImage; // Komponen Image pada kartu
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Button cardButton;

    // Dipanggil oleh GameManagerBos2 saat men-generate kartu
    public void SetupCard(int value)
    {
        cardValue = value;
        valueText.text = value.ToString();
        
        // Atur kondisi awal kartu (tertutup)
        isFlipped = false;
        cardImage.sprite = cardClosedSprite;
        valueText.gameObject.SetActive(false);
    }

    public void OnCardClicked()
    {
        // Cegah klik jika sedang terbuka, sudah cocok, sedang dianimasikan, atau game manager sedang mengecek
        if (isFlipped || isMatched || isAnimating || GameManagerBos2.Instance.isChecking) return;

        OpenCard();
        GameManagerBos2.Instance.OnCardSelected(this);
    }

    public void OpenCard()
    {
        StartCoroutine(AnimateCard(true));
    }

    public void CloseCard()
    {
        StartCoroutine(AnimateCard(false));
    }

    // Coroutine untuk transisi Fade Gelap dan Ganti Gambar
    private IEnumerator AnimateCard(bool opening)
    {
        isAnimating = true;
        
        float fadeDuration = 0.15f; // Durasi setengah animasi (total 0.3 detik)
        float timer = 0f;
        Color startColor = Color.white; // Terang (Normal)
        Color darkColor = new Color(0.3f, 0.3f, 0.3f, 1f); // Gelap keabu-abuan

        // 1. FADE OUT (Menjadi Gelap)
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            cardImage.color = Color.Lerp(startColor, darkColor, timer / fadeDuration);
            yield return null;
        }

        // 2. TITIK BUTA (Ganti Gambar & Teks)
        if (opening)
        {
            isFlipped = true;
            cardImage.sprite = cardOpenSprite;
            valueText.gameObject.SetActive(true);
        }
        else
        {
            isFlipped = false;
            cardImage.sprite = cardClosedSprite;
            valueText.gameObject.SetActive(false);
        }

        // 3. FADE IN (Kembali Terang)
        timer = 0f;
        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            cardImage.color = Color.Lerp(darkColor, startColor, timer / fadeDuration);
            yield return null;
        }

        cardImage.color = startColor; // Pastikan warnanya kembali normal 100%
        isAnimating = false;
    }
}