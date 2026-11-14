using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using LuckyNumber.Core;

namespace LuckyNumber.Characters
{
    public interface IWorldInteractable
    {
        Transform Transform { get; }
        string InteractionPrompt { get; }
        float InteractionRadius { get; }
        bool CanInteract { get; }
        void Interact(PlayerController player);
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(CharacterSpriteAnimator))]
    [RequireComponent(typeof(PlayerFootstepVFX))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5.5f;

        [Header("Walking Sound Effect (Placeholder)")]
        [Tooltip("AudioSource untuk memutar suara langkah kaki karakter. Otomatis dibuat jika dibiarkan kosong.")]
        public AudioSource footstepAudioSource;

        [Tooltip("Placeholder klip suara langkah kaki / berjalan karakter. Masukkan AudioClip langkah kaki di sini.")]
        public AudioClip walkingSoundClip;

        [Tooltip("Daftar variasi suara langkah kaki (opsional jika memiliki beberapa variasi AudioClip langkah).")]
        public AudioClip[] walkingSoundVariations;

        [Range(0f, 1f)]
        [Tooltip("Volume suara langkah karakter")]
        public float walkingSoundVolume = 0.5f;

        [Tooltip("Jeda interval antar suara langkah saat berjalan (detik). Default: 0.32 detik.")]
        [SerializeField] private float stepSoundInterval = 0.32f;

        private float _stepSoundTimer;

        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private CharacterSpriteAnimator _animator;
        private Vector2 _moveInput;
        private bool _movementLocked;

        private static readonly List<IWorldInteractable> RegisteredInteractables = new List<IWorldInteractable>();
        private IWorldInteractable _currentNearestInteractable;

        public IWorldInteractable NearestInteractable => _currentNearestInteractable;
        public CharacterSpriteAnimator Animator => _animator;

        public bool IsMovementLocked
        {
            get => _movementLocked;
            set
            {
                _movementLocked = value;
                if (_movementLocked)
                {
                    _moveInput = Vector2.zero;
                    if (_rb != null) _rb.linearVelocity = Vector2.zero;
                    if (_animator != null) _animator.SetMovementState(Vector2.zero, false);
                }
            }
        }

        public static void RegisterInteractable(IWorldInteractable interactable)
        {
            if (interactable != null && !RegisteredInteractables.Contains(interactable))
            {
                RegisteredInteractables.Add(interactable);
            }
        }

        public static void UnregisterInteractable(IWorldInteractable interactable)
        {
            if (interactable != null)
            {
                RegisteredInteractables.Remove(interactable);
            }
        }

        private void Awake()
        {
            Instance = this;
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            _animator = GetComponent<CharacterSpriteAnimator>();

            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _collider.isTrigger = false;
            _collider.radius = 0.85f;
            _collider.offset = new Vector2(0f, -1.55f);

            EnsureFootstepAudioSource();
        }

        private void EnsureFootstepAudioSource()
        {
            if (footstepAudioSource == null)
            {
                footstepAudioSource = GetComponent<AudioSource>();
                if (footstepAudioSource == null)
                {
                    footstepAudioSource = gameObject.AddComponent<AudioSource>();
                }
                footstepAudioSource.playOnAwake = false;
                footstepAudioSource.spatialBlend = 0f;
            }
        }

        private void Update()
        {
            if (_movementLocked)
            {
                _moveInput = Vector2.zero;
                _animator.SetMovementState(Vector2.zero, false);
                _stepSoundTimer = 0.06f;
                return;
            }

            ReadMovementInput();
            bool isMoving = _moveInput.sqrMagnitude > 0.001f;
            _animator.SetMovementState(_moveInput, isMoving);

            UpdateWalkingSound(isMoving);

            UpdateNearestInteractable();

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.eKey.wasPressedThisFrame)
            {
                if (_currentNearestInteractable != null && _currentNearestInteractable.CanInteract)
                {
                    _currentNearestInteractable.Interact(this);
                }
            }
        }

        private void FixedUpdate()
        {
            if (_movementLocked)
            {
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            _rb.linearVelocity = _moveInput * moveSpeed;
        }

        private void ReadMovementInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                _moveInput = Vector2.zero;
                return;
            }

            float x = 0f;
            float y = 0f;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;

            Vector2 raw = new Vector2(x, y);
            if (raw.sqrMagnitude > 1f) raw.Normalize();
            _moveInput = raw;
        }

        private void UpdateNearestInteractable()
        {
            _currentNearestInteractable = null;
            float bestDistSq = float.MaxValue;
            Vector2 myPos = transform.position;

            for (int i = RegisteredInteractables.Count - 1; i >= 0; i--)
            {
                var item = RegisteredInteractables[i];
                if (item == null || item.Transform == null)
                {
                    RegisteredInteractables.RemoveAt(i);
                    continue;
                }

                if (!item.CanInteract) continue;

                float radius = item.InteractionRadius;
                float distSq = ((Vector2)item.Transform.position - myPos).sqrMagnitude;
                if (distSq <= radius * radius && distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    _currentNearestInteractable = item;
                }
            }
        }

        private void UpdateWalkingSound(bool isMoving)
        {
            if (!isMoving)
            {
                _stepSoundTimer = 0.06f;
                return;
            }

            _stepSoundTimer -= Time.deltaTime;
            if (_stepSoundTimer <= 0f)
            {
                PlayWalkingFootstepSound();
                _stepSoundTimer = Mathf.Max(0.1f, stepSoundInterval);
            }
        }

        private void PlayWalkingFootstepSound()
        {
            AudioClip clipToPlay = null;

            if (walkingSoundVariations != null && walkingSoundVariations.Length > 0)
            {
                List<AudioClip> validClips = new List<AudioClip>();
                for (int i = 0; i < walkingSoundVariations.Length; i++)
                {
                    if (walkingSoundVariations[i] != null) validClips.Add(walkingSoundVariations[i]);
                }
                if (validClips.Count > 0)
                {
                    clipToPlay = validClips[Random.Range(0, validClips.Count)];
                }
            }

            if (clipToPlay == null)
            {
                clipToPlay = walkingSoundClip;
            }

            // Fallback to PlayerFootstepVFX footstepClips if assigned there
            if (clipToPlay == null)
            {
                PlayerFootstepVFX vfx = GetComponent<PlayerFootstepVFX>();
                if (vfx != null && vfx.footstepClips != null && vfx.footstepClips.Length > 0)
                {
                    List<AudioClip> vfxClips = new List<AudioClip>();
                    for (int i = 0; i < vfx.footstepClips.Length; i++)
                    {
                        if (vfx.footstepClips[i] != null) vfxClips.Add(vfx.footstepClips[i]);
                    }
                    if (vfxClips.Count > 0)
                    {
                        clipToPlay = vfxClips[Random.Range(0, vfxClips.Count)];
                    }
                }
            }

            if (clipToPlay == null) return;

            EnsureFootstepAudioSource();
            if (footstepAudioSource != null)
            {
                footstepAudioSource.pitch = Random.Range(0.93f, 1.07f);
                footstepAudioSource.PlayOneShot(clipToPlay, walkingSoundVolume);
            }
        }

        public void TeleportTo(Vector3 targetPosition, FacingDirection? facing = null)
        {
            transform.position = targetPosition;
            if (_rb != null)
            {
                _rb.position = targetPosition;
                _rb.linearVelocity = Vector2.zero;
            }

            if (facing.HasValue && _animator != null)
            {
                _animator.SetFacingDirection(facing.Value);
            }

            if (SmoothCameraFollow.Instance != null)
            {
                SmoothCameraFollow.Instance.SnapToTarget();
            }
        }
    }
}
