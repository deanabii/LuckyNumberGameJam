using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace LuckyNumber.UI
{
    /// <summary>
    /// Attach to buttons in Canvas 'Button Main Menu' (Play, Credit, Exit):
    /// - Smooth Child of Light-inspired scale up and text brightening on hover/select.
    /// - Soft horizontal luminous glow bar that fades in behind the button.
    /// - Audio sound effect hooks.
    /// - Clean click handling (Play -> TestMap, Credit -> open popup, Exit -> quit).
    /// </summary>
    public class MainMenuButtonHoverEffect : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [Header("Hover Visual Settings")]
        [SerializeField] [Range(1.0f, 1.25f)] private float hoverScale = 1.08f;
        [SerializeField] private Color normalTextColor = new Color(0.85f, 0.90f, 0.95f, 0.85f);
        [SerializeField] private Color hoveredTextColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        [SerializeField] private Color glowBarColor = new Color(1.0f, 1.0f, 1.0f, 0.70f);

        [Header("Target References (Auto-detected if null)")]
        [SerializeField] private TextMeshProUGUI buttonLabel;
        [SerializeField] private Image glowBackingImg;

        private RectTransform _rt;
        private Vector3 _baseScale = Vector3.one;
        private bool _isHovered;
        private float _currentGlowAlpha;
        private float _currentScale = 1.0f;
        private static Sprite _proceduralGlowBarSprite;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (_rt != null) _baseScale = _rt.localScale;

            EnsureButtonLabel();
            EnsureGlowBacking();
            ApplyInitialState();
        }

        private void OnEnable()
        {
            ApplyInitialState();
        }

        private void Update()
        {
            float targetAlpha = _isHovered ? 1.0f : 0.0f;
            float targetScale = _isHovered ? hoverScale : 1.0f;

            _currentGlowAlpha = Mathf.MoveTowards(_currentGlowAlpha, targetAlpha, Time.unscaledDeltaTime * 6.5f);
            _currentScale = Mathf.Lerp(_currentScale, targetScale, Time.unscaledDeltaTime * 14f);

            if (_rt != null)
            {
                _rt.localScale = _baseScale * _currentScale;
            }

            if (glowBackingImg != null)
            {
                Color c = glowBarColor;
                c.a = glowBarColor.a * _currentGlowAlpha;
                glowBackingImg.color = c;
            }

            if (buttonLabel != null)
            {
                buttonLabel.color = Color.Lerp(normalTextColor, hoveredTextColor, _currentGlowAlpha);
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
        public void OnPointerExit(PointerEventData eventData) => SetHovered(false);
        public void OnSelect(BaseEventData eventData) => SetHovered(true);
        public void OnDeselect(BaseEventData eventData) => SetHovered(false);

        public void OnPointerClick(PointerEventData eventData) => TriggerClick();
        public void OnSubmit(BaseEventData eventData) => TriggerClick();

        private void SetHovered(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;

            if (_isHovered)
            {
                MainMenuController controller = GetComponentInParent<MainMenuController>();
                if (controller != null)
                {
                    controller.PlayHoverSound();
                }
            }
        }

        private void TriggerClick()
        {
            // Jika ada komponen Button standar Unity, biarkan Button.onClick yang menangani klik
            // agar tidak terjadi eksekusi dobel (misal toggle credit langsung terbuka dan tertutup kembali)
            Button btn = GetComponent<Button>();
            if (btn != null) return;

            MainMenuController controller = GetComponentInParent<MainMenuController>();
            if (controller != null)
            {
                controller.HandleButtonClick(gameObject.name);
            }
        }

        public void EnsureButtonLabel()
        {
            if (buttonLabel == null)
            {
                buttonLabel = GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (buttonLabel != null)
            {
                buttonLabel.color = normalTextColor;
            }
        }

        public void EnsureGlowBacking()
        {
            if (glowBackingImg != null) return;

            Transform existing = transform.Find("GlowHighlightBar");
            if (existing != null)
            {
                glowBackingImg = existing.GetComponent<Image>();
                return;
            }

            GameObject glowObj = new GameObject("GlowHighlightBar", typeof(RectTransform), typeof(Image));
            glowObj.transform.SetParent(transform, false);
            glowObj.transform.SetAsFirstSibling(); // Behind text

            RectTransform grt = glowObj.GetComponent<RectTransform>();
            grt.anchorMin = new Vector2(0f, 0.05f);
            grt.anchorMax = new Vector2(1f, 0.95f);
            grt.offsetMin = new Vector2(-20f, -6f);
            grt.offsetMax = new Vector2(20f, 6f);

            glowBackingImg = glowObj.GetComponent<Image>();
            glowBackingImg.sprite = GetOrCreateGlowBarSprite();
            glowBackingImg.type = Image.Type.Simple;
            glowBackingImg.color = new Color(1f, 1f, 1f, 0f);
            glowBackingImg.raycastTarget = false;
        }

        private void ApplyInitialState()
        {
            _isHovered = false;
            _currentGlowAlpha = 0f;
            _currentScale = 1.0f;
            if (_rt != null) _rt.localScale = _baseScale;
            if (glowBackingImg != null) glowBackingImg.color = new Color(1f, 1f, 1f, 0f);
            if (buttonLabel != null) buttonLabel.color = normalTextColor;
        }

        public static Sprite GetOrCreateGlowBarSprite()
        {
            if (_proceduralGlowBarSprite != null) return _proceduralGlowBarSprite;

            const int width = 256;
            const int height = 32;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                float ny = ((float)y / (height - 1)) * 2f - 1f;
                float vertFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - Mathf.Abs(ny)));

                for (int x = 0; x < width; x++)
                {
                    float nx = ((float)x / (width - 1)) * 2f - 1f;
                    float horizFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - Mathf.Abs(nx)));

                    float alpha = Mathf.Pow(vertFade * horizFade, 1.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            _proceduralGlowBarSprite = Sprite.Create(
                tex,
                new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f)
            );
            return _proceduralGlowBarSprite;
        }
    }
}
