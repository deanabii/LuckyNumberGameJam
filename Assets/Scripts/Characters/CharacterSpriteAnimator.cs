using UnityEngine;
using LuckyNumber.Core;

namespace LuckyNumber.Characters
{
    public enum FacingDirection
    {
        Down = 0,
        Left = 1,
        Right = 2,
        Up = 3
    }

    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterSpriteAnimator : MonoBehaviour
    {
        [Header("Sprite Sheet Frames (24 frames: 6 cols x 4 rows)")]
        [SerializeField] private Sprite[] spriteFrames;
        [SerializeField] private bool isArvandIdleNPC = false;

        [Header("Animation Settings")]
        [SerializeField] private float walkFrameRate = 9.5f;
        [SerializeField] private float idleFrameRate = 3.0f;
        [SerializeField] private bool enableBreathingPulse = true;
        [SerializeField] private float breathingAmplitude = 0.03f;
        [SerializeField] private float breathingSpeed = 3.2f;

        [Header("Sorting Settings")]
        [SerializeField] private string sortingLayerName = "Prop";
        [SerializeField] private int baseSortingOrder = 1000;

        private SpriteRenderer _spriteRenderer;
        private FacingDirection _currentDirection = FacingDirection.Down;
        private bool _isMoving;
        private float _animTimer;
        private int _currentFrameIndex;
        private Vector3 _baseScale;

        // Natural idle sequence for Arvand (eyes open + occasional wise blink from frames 0..5)
        private static readonly int[] ArvandIdleSequence = { 0, 1, 2, 1, 0, 0, 1, 2, 3, 4, 5, 3, 0, 1 };

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            if (baseSortingOrder < 200) baseSortingOrder = 1000;
            ConfigureSortingLayer();
        }

        private void Start()
        {
            EnsureSpritesLoaded();
            ApplyCurrentFrame();
        }

        public void SetSprites(Sprite[] frames, bool arvandMode = false)
        {
            spriteFrames = frames;
            isArvandIdleNPC = arvandMode;
            if (baseSortingOrder < 200) baseSortingOrder = 1000;
            if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
            ConfigureSortingLayer();
            ApplyCurrentFrame();
        }

        public Sprite GetPortraitSprite()
        {
            EnsureSpritesLoaded();
            if (spriteFrames != null && spriteFrames.Length > 0 && spriteFrames[0] != null)
            {
                return spriteFrames[0];
            }
            return _spriteRenderer != null ? _spriteRenderer.sprite : null;
        }

        public void SetMovementState(Vector2 moveDir, bool isMoving)
        {
            _isMoving = isMoving;

            if (isMoving && moveDir.sqrMagnitude > 0.001f)
            {
                if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
                {
                    _currentDirection = moveDir.x < 0f ? FacingDirection.Left : FacingDirection.Right;
                }
                else
                {
                    _currentDirection = moveDir.y < 0f ? FacingDirection.Down : FacingDirection.Up;
                }
            }
        }

        public void SetFacingDirection(FacingDirection dir)
        {
            _currentDirection = dir;
        }

        private void Update()
        {
            EnsureSpritesLoaded();
            if (spriteFrames == null || spriteFrames.Length == 0) return;

            float fps = (_isMoving && !isArvandIdleNPC) ? walkFrameRate : idleFrameRate;
            _animTimer += Time.deltaTime * fps;

            if (isArvandIdleNPC)
            {
                int seqIdx = Mathf.FloorToInt(_animTimer) % ArvandIdleSequence.Length;
                _currentFrameIndex = ArvandIdleSequence[seqIdx];
                if (_currentFrameIndex < spriteFrames.Length && spriteFrames[_currentFrameIndex] != null)
                {
                    _spriteRenderer.sprite = spriteFrames[_currentFrameIndex];
                }
            }
            else
            {
                int rowOffset = GetRowOffset(_currentDirection);
                if (_isMoving)
                {
                    int col = Mathf.FloorToInt(_animTimer) % 6;
                    int idx = Mathf.Clamp(rowOffset + col, 0, spriteFrames.Length - 1);
                    if (spriteFrames[idx] != null)
                    {
                        _spriteRenderer.sprite = spriteFrames[idx];
                    }
                }
                else
                {
                    // Stand completely still on the neutral standing frame (col 0) so Malios's feet do not move while idle
                    _animTimer = 0f;
                    int idx = Mathf.Clamp(rowOffset, 0, spriteFrames.Length - 1);
                    if (spriteFrames[idx] != null)
                    {
                        _spriteRenderer.sprite = spriteFrames[idx];
                    }
                }
            }

            if (enableBreathingPulse)
            {
                float pulse = Mathf.Sin(Time.time * breathingSpeed) * breathingAmplitude;
                float walkBounce = (_isMoving && !isArvandIdleNPC)
                    ? Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.035f
                    : 0f;

                transform.localScale = new Vector3(
                    _baseScale.x * (1f - pulse * 0.4f),
                    _baseScale.y * (1f + pulse + walkBounce),
                    _baseScale.z
                );
            }

            // Dynamic Y-sorting above Prop tilemaps (order 0) across all islands (Y up to ~65)
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = Mathf.Max(20, baseSortingOrder - Mathf.RoundToInt(transform.position.y * 10f));
            }
        }

        private int GetRowOffset(FacingDirection dir)
        {
            // In malios.png:
            // Row 0 (0..5)  = Down
            // Row 1 (6..11) = Left
            // Row 2 (12..17)= Right
            // Row 3 (18..23)= Up
            switch (dir)
            {
                case FacingDirection.Down: return 0;
                case FacingDirection.Left: return 6;
                case FacingDirection.Right: return 12;
                case FacingDirection.Up: return 18;
                default: return 0;
            }
        }

        private void EnsureSpritesLoaded()
        {
            if (spriteFrames != null && spriteFrames.Length >= 24 && spriteFrames[0] != null) return;

            var registry = GameAssetRegistry.Instance;
            if (registry != null)
            {
                spriteFrames = isArvandIdleNPC ? registry.arvandSprites : registry.maliosSprites;
            }
        }

        private void ConfigureSortingLayer()
        {
            if (_spriteRenderer == null) return;
            _spriteRenderer.sortingLayerName = sortingLayerName;
            _spriteRenderer.sortingOrder = baseSortingOrder;
        }

        private void ApplyCurrentFrame()
        {
            if (_spriteRenderer == null || spriteFrames == null || spriteFrames.Length == 0) return;
            if (spriteFrames[0] != null)
            {
                _spriteRenderer.sprite = spriteFrames[0];
            }
        }
    }
}
