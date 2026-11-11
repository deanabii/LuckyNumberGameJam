using System.Collections.Generic;
using UnityEngine;
using LuckyNumber.Core;

namespace LuckyNumber.Characters
{
    /// <summary>
    /// Spawns animated pixel-art dust puffs (Tiny Swords Dust_01 & Dust_02) at Malios's feet
    /// in world space whenever the character walks or changes direction.
    /// </summary>
    public class PlayerFootstepVFX : MonoBehaviour
    {
        [Header("Footstep VFX Settings")]
        [SerializeField] private float stepInterval = 0.17f;
        [SerializeField] private float dustScale = 0.68f;
        [SerializeField] private float animationFps = 18f;
        [SerializeField] private Vector2 footWorldOffset = new Vector2(0f, -0.34f);
        [SerializeField] private Color dustColor = new Color(1f, 0.97f, 0.90f, 0.88f);

        private class ActiveDustPuff
        {
            public GameObject gameObject;
            public Transform transform;
            public SpriteRenderer renderer;
            public Sprite[] frames;
            public float elapsed;
            public float startScale;
            public bool active;
        }

        private readonly List<ActiveDustPuff> _pool = new List<ActiveDustPuff>();
        private Transform _worldContainer;
        private Vector3 _lastPosition;
        private Vector2 _lastMoveDir;
        private float _stepTimer;
        private bool _leftFootNext;
        private bool _wasMoving;

        private void Awake()
        {
            _lastPosition = transform.position;
        }

        private void EnsureContainer()
        {
            if (_worldContainer != null) return;
            GameObject existing = GameObject.Find("LuckyNumber_VFXContainer");
            if (existing == null)
            {
                existing = new GameObject("LuckyNumber_VFXContainer");
            }
            _worldContainer = existing.transform;
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - _lastPosition;
            _lastPosition = currentPos;

            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            bool isMoving = speed > 0.35f;

            if (isMoving)
            {
                Vector2 moveDir = ((Vector2)delta).normalized;

                // Spawn immediate puff when starting to move or reversing direction sharply
                if (!_wasMoving || Vector2.Dot(moveDir, _lastMoveDir) < -0.35f)
                {
                    SpawnDustPuff(currentPos, moveDir, isBurst: true);
                    _stepTimer = stepInterval * 0.85f;
                }
                else
                {
                    _stepTimer -= Time.deltaTime;
                    if (_stepTimer <= 0f)
                    {
                        SpawnDustPuff(currentPos, moveDir, isBurst: false);
                        _stepTimer = stepInterval;
                    }
                }

                _lastMoveDir = moveDir;
            }
            else
            {
                _stepTimer = 0.04f;
            }

            _wasMoving = isMoving;
            UpdateActivePuffs(Time.deltaTime);
        }

        public void SpawnDustPuff(Vector3 characterPos, Vector2 moveDir, bool isBurst)
        {
            var registry = GameAssetRegistry.Instance;
            if (registry == null) return;

            Sprite[] frames = (isBurst && registry.dust2Sprites != null && registry.dust2Sprites.Length > 0)
                ? registry.dust2Sprites
                : (registry.dust1Sprites != null && registry.dust1Sprites.Length > 0 ? registry.dust1Sprites : registry.dust2Sprites);

            if (frames == null || frames.Length == 0 || frames[0] == null) return;

            EnsureContainer();

            ActiveDustPuff puff = GetOrCreatePuff();
            puff.frames = frames;
            puff.elapsed = 0f;
            puff.active = true;
            puff.startScale = isBurst ? dustScale * 1.18f : dustScale * Random.Range(0.88f, 1.08f);

            // Alternate slight left/right foot offset perpendicular to movement
            _leftFootNext = !_leftFootNext;
            Vector2 perp = new Vector2(-moveDir.y, moveDir.x) * (_leftFootNext ? 0.08f : -0.08f);
            Vector2 trailOffset = -moveDir * 0.07f;

            Vector3 spawnPos = new Vector3(
                characterPos.x + footWorldOffset.x + perp.x + trailOffset.x,
                characterPos.y + footWorldOffset.y + perp.y + trailOffset.y,
                0f
            );

            puff.transform.position = spawnPos;
            puff.transform.localScale = new Vector3(
                _leftFootNext ? puff.startScale : -puff.startScale,
                puff.startScale,
                1f
            );

            puff.renderer.sprite = frames[0];
            puff.renderer.color = dustColor;
            puff.renderer.sortingLayerName = "Prop";
            // Render just behind character feet
            puff.renderer.sortingOrder = Mathf.RoundToInt(-spawnPos.y * 100f) + 55;
            puff.gameObject.SetActive(true);
        }

        private void UpdateActivePuffs(float dt)
        {
            if (_pool.Count == 0) return;

            float frameDuration = 1f / Mathf.Max(6f, animationFps);

            for (int i = 0; i < _pool.Count; i++)
            {
                ActiveDustPuff p = _pool[i];
                if (!p.active || p.gameObject == null) continue;

                p.elapsed += dt;
                int frameIndex = Mathf.FloorToInt(p.elapsed / frameDuration);

                if (p.frames == null || frameIndex >= p.frames.Length)
                {
                    p.active = false;
                    p.gameObject.SetActive(false);
                    continue;
                }

                p.renderer.sprite = p.frames[frameIndex];

                float progress = (float)frameIndex / p.frames.Length;
                Color c = dustColor;
                c.a = dustColor.a * (1f - progress * 0.65f);
                p.renderer.color = c;

                // Subtle upward drift
                p.transform.position += new Vector3(0f, dt * 0.22f, 0f);
            }
        }

        private ActiveDustPuff GetOrCreatePuff()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].active && _pool[i].gameObject != null)
                {
                    return _pool[i];
                }
            }

            GameObject go = new GameObject("FootstepDustPuff", typeof(SpriteRenderer));
            go.transform.SetParent(_worldContainer, false);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sortingLayerName = "Prop";

            ActiveDustPuff created = new ActiveDustPuff
            {
                gameObject = go,
                transform = go.transform,
                renderer = sr,
                active = false
            };
            _pool.Add(created);
            return created;
        }
    }
}
