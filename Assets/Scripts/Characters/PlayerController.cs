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
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5.5f;

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
        }

        private void Update()
        {
            if (_movementLocked)
            {
                _moveInput = Vector2.zero;
                _animator.SetMovementState(Vector2.zero, false);
                return;
            }

            ReadMovementInput();
            _animator.SetMovementState(_moveInput, _moveInput.sqrMagnitude > 0.001f);

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
