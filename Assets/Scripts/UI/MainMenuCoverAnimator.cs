using System.Collections.Generic;
using UnityEngine;

namespace LuckyNumber.UI
{
    /// <summary>
    /// Animates the visual elements of the Main Menu cover:
    /// 1. Floating & breathing animation on 'title_0' logo.
    /// 2. Warm flickering lights on cottage lanterns/windows.
    /// 3. Ambient twilight fireflies/embers floating gently across the scene.
    /// 4. Subtle camera drift for atmospheric depth.
    /// </summary>
    public class MainMenuCoverAnimator : MonoBehaviour
    {
        [Header("Title Floating Animation")]
        [SerializeField] private Transform titleTransform;
        [SerializeField] private float titleFloatDistance = 0.12f;
        [SerializeField] private float titleFloatSpeed = 1.4f;
        [SerializeField] private float titleBreatheAmount = 0.015f;
        [SerializeField] private float titleBreatheSpeed = 1.1f;

        [Header("Cottage Lanterns Flickering")]
        [SerializeField] private bool enableLanterns = true;
        [SerializeField] private Color lanternColor = new Color(1.0f, 0.78f, 0.38f, 0.65f);
        [SerializeField] private float lanternFlickerSpeed = 3.2f;

        [Header("Twilight Fireflies / Embers")]
        [SerializeField] private bool enableFireflies = true;
        [SerializeField] private int maxFireflies = 35;

        [Header("Subtle Ambient Camera Sway")]
        [SerializeField] private bool enableCameraSway = true;
        [SerializeField] private float cameraSwayAmount = 0.06f;
        [SerializeField] private float cameraSwaySpeed = 0.45f;

        private Vector3 _titleBasePos;
        private Vector3 _titleBaseScale = Vector3.one;
        private Camera _mainCam;
        private Vector3 _camBasePos;
        private static Sprite _lanternGlowSprite;

        private readonly List<LanternGlow> _lanterns = new List<LanternGlow>();
        private ParticleSystem _fireflyPS;

        private struct LanternGlow
        {
            public SpriteRenderer renderer;
            public Vector3 baseScale;
            public float seed;
            public float baseAlpha;
        }

        private void Awake()
        {
            FindReferences();
            SetupLanterns();
            SetupFireflies();
        }

        private void Start()
        {
            if (titleTransform != null)
            {
                _titleBasePos = titleTransform.localPosition;
                _titleBaseScale = titleTransform.localScale;
            }

            if (_mainCam != null)
            {
                _camBasePos = _mainCam.transform.position;
            }
        }

        private void FindReferences()
        {
            if (titleTransform == null)
            {
                GameObject tObj = GameObject.Find("title_0");
                if (tObj != null) titleTransform = tObj.transform;
            }

            _mainCam = Camera.main;
        }

        private void SetupLanterns()
        {
            if (!enableLanterns) return;

            // Coordinates for cottage windows/lanterns on mainmenu cover art
            Vector3[] lanternPositions = new Vector3[]
            {
                new Vector3(-6.95f, 0.65f, -0.1f),   // Left cottage window
                new Vector3(-4.85f, 0.35f, -0.1f),   // Left cottage doorway lantern
                new Vector3(-5.80f, 1.45f, -0.1f),   // Left cottage roof lamp
                new Vector3(7.60f, 1.35f, -0.1f)     // Right watchtower window
            };

            Sprite glowSprite = GetOrCreateLanternSprite();

            Transform lanternContainer = transform.Find("CoverLanterns");
            if (lanternContainer == null)
            {
                GameObject contObj = new GameObject("CoverLanterns");
                contObj.transform.SetParent(transform, false);
                lanternContainer = contObj.transform;
            }

            for (int i = 0; i < lanternPositions.Length; i++)
            {
                string lName = $"Lantern_{i + 1}";
                Transform existing = lanternContainer.Find(lName);
                GameObject lObj = existing != null ? existing.gameObject : new GameObject(lName, typeof(SpriteRenderer));
                lObj.transform.SetParent(lanternContainer, false);
                lObj.transform.position = lanternPositions[i];
                lObj.transform.localScale = Vector3.one * 0.85f;

                SpriteRenderer sr = lObj.GetComponent<SpriteRenderer>();
                sr.sprite = glowSprite;
                sr.sortingOrder = 15; // Above background (0), under fog (20) & title (25)
                sr.color = lanternColor;

                _lanterns.Add(new LanternGlow
                {
                    renderer = sr,
                    baseScale = lObj.transform.localScale,
                    seed = i * 137.49f,
                    baseAlpha = lanternColor.a
                });
            }
        }

        private void SetupFireflies()
        {
            if (!enableFireflies) return;

            Transform existing = transform.Find("CoverFireflies");
            if (existing != null)
            {
                _fireflyPS = existing.GetComponent<ParticleSystem>();
                return;
            }

            GameObject ffObj = new GameObject("CoverFireflies", typeof(ParticleSystem), typeof(ParticleSystemRenderer));
            ffObj.transform.SetParent(transform, false);
            ffObj.transform.position = new Vector3(0f, -2.5f, 0f);

            _fireflyPS = ffObj.GetComponent<ParticleSystem>();
            ParticleSystemRenderer psr = ffObj.GetComponent<ParticleSystemRenderer>();
            psr.sortingOrder = 22; // In front of fog (20), behind UI

            var main = _fireflyPS.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.maxParticles = maxFireflies;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.95f, 0.55f, 0.85f),
                new Color(0.70f, 1.0f, 0.60f, 0.80f)
            );

            var emission = _fireflyPS.emission;
            emission.rateOverTime = 5.0f;

            var shape = _fireflyPS.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(22f, 3.5f, 1f);

            var vel = _fireflyPS.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(0.25f, 0.60f); // Float upwards gently

            var noise = _fireflyPS.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;

            var col = _fireflyPS.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.3f), new GradientAlphaKey(0.85f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = g;

            var size = _fireflyPS.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sc = new AnimationCurve();
            sc.AddKey(0f, 0.2f);
            sc.AddKey(0.5f, 1.0f);
            sc.AddKey(1f, 0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sc);
        }

        private void Update()
        {
            float t = Time.time;

            // 1. Animate Title
            if (titleTransform != null)
            {
                float floatOffset = Mathf.Sin(t * titleFloatSpeed) * titleFloatDistance;
                titleTransform.localPosition = _titleBasePos + new Vector3(0f, floatOffset, 0f);

                float breatheFactor = 1.0f + Mathf.Sin(t * titleBreatheSpeed) * titleBreatheAmount;
                titleTransform.localScale = _titleBaseScale * breatheFactor;
            }

            // 2. Animate Lantern Flickering
            if (enableLanterns && _lanterns.Count > 0)
            {
                for (int i = 0; i < _lanterns.Count; i++)
                {
                    LanternGlow lg = _lanterns[i];
                    if (lg.renderer == null) continue;

                    float pNoise = Mathf.PerlinNoise(t * lanternFlickerSpeed, lg.seed);
                    float flickerAlpha = lg.baseAlpha * Mathf.Lerp(0.65f, 1.25f, pNoise);
                    Color c = lg.renderer.color;
                    c.a = Mathf.Clamp01(flickerAlpha);
                    lg.renderer.color = c;

                    float scalePulse = Mathf.Lerp(0.92f, 1.08f, pNoise);
                    lg.renderer.transform.localScale = lg.baseScale * scalePulse;
                }
            }

            // 3. Subtle Camera Sway
            if (enableCameraSway && _mainCam != null)
            {
                float camX = Mathf.Sin(t * cameraSwaySpeed) * cameraSwayAmount;
                float camY = Mathf.Cos(t * cameraSwaySpeed * 0.7f) * (cameraSwayAmount * 0.5f);
                _mainCam.transform.position = _camBasePos + new Vector3(camX, camY, 0f);
            }
        }

        private static Sprite GetOrCreateLanternSprite()
        {
            if (_lanternGlowSprite != null) return _lanternGlowSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float norm = Mathf.Clamp01(dist / radius);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - norm), 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 0.92f, 0.65f, alpha));
                }
            }
            tex.Apply();

            _lanternGlowSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f
            );
            return _lanternGlowSprite;
        }
    }
}
