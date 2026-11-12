using System.Collections;
using UnityEngine;

namespace LuckyNumber.UI
{
    /// <summary>
    /// Configures and animates the fog particle system in MainMenu scene to create a living,
    /// atmospheric twilight mist drifting softly across the landscape.
    /// Works in both Edit Mode ([ExecuteAlways]) and Play Mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(ParticleSystem))]
    public class MainMenuFogController : MonoBehaviour
    {
        [Header("Fog Atmosphere & Color")]
        [SerializeField] private Color primaryFogColor = new Color(0.92f, 0.82f, 0.98f, 0.22f);
        [SerializeField] private Color secondaryFogColor = new Color(1.0f, 0.88f, 0.75f, 0.16f);

        [Header("Fog Movement & Density")]
        [SerializeField] [Range(1f, 15f)] private float emissionRate = 5.0f;
        [SerializeField] [Range(0.1f, 2.0f)] private float horizontalDriftSpeed = 0.55f;
        [SerializeField] [Range(2f, 10f)] private float minParticleSize = 3.6f;
        [SerializeField] [Range(3f, 15f)] private float maxParticleSize = 6.4f;
        [SerializeField] [Range(5f, 25f)] private float particleLifetime = 14f;

        [Header("Fog Area Bounds")]
        [SerializeField] private Vector3 emitterCenter = new Vector3(-0.5f, -1.2f, 0f);
        [SerializeField] private Vector3 emitterBoxSize = new Vector3(25f, 4.5f, 2f);

        private ParticleSystem _ps;
        private ParticleSystemRenderer _psr;
        private bool _isConfigured;

        private void Awake()
        {
            ApplyFogSettings();
        }

        private void OnEnable()
        {
            ApplyFogSettings();
        }

        private void OnValidate()
        {
            _isConfigured = false;
            ApplyFogSettings();
        }

        public void ApplyFogSettings()
        {
            if (_isConfigured && Application.isPlaying) return;

            _ps = GetComponent<ParticleSystem>();
            if (_ps == null) return;
            _psr = GetComponent<ParticleSystemRenderer>();

            // 1. Position emitter nicely in lower-middle landscape
            transform.position = emitterCenter;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // 2. Material & Sorting
            if (_psr != null)
            {
                _psr.sortingOrder = 20; // In front of mainmenu_0 (0), behind character and UI
                Material smokeMat = Resources.Load<Material>("VFX/Smoke");
                if (smokeMat == null)
                {
                    smokeMat = LoadSmokeMaterialFallback();
                }
                if (smokeMat != null)
                {
                    _psr.sharedMaterial = smokeMat;
                }
            }

            // 3. Main Module
            var main = _ps.main;
            main.loop = true;
            main.prewarm = true; // Particles already rolling when scene opens!
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetime * 0.75f, particleLifetime * 1.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(horizontalDriftSpeed * 0.6f, horizontalDriftSpeed * 1.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(minParticleSize, maxParticleSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);

            // Two-color random gradient (twilight lilac & warm sunset amber)
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(primaryFogColor, 0f), new GradientColorKey(secondaryFogColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(primaryFogColor.a, 0f), new GradientAlphaKey(secondaryFogColor.a, 1f) }
            );
            main.startColor = new ParticleSystem.MinMaxGradient(primaryFogColor, secondaryFogColor);

            // 4. Emission Module
            var emission = _ps.emission;
            emission.enabled = true;
            emission.rateOverTime = emissionRate;

            // 5. Shape Module (broad box spanning the road and valley)
            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = emitterBoxSize;

            // 6. Velocity over Lifetime (horizontal drift left-to-right with slight wave)
            var velocity = _ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(horizontalDriftSpeed * 0.5f, horizontalDriftSpeed * 1.5f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.06f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // 7. Color over Lifetime (smooth fade in, hold, smooth fade out)
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            Gradient lifeGrad = new Gradient();
            lifeGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(lifeGrad);

            // 8. Size over Lifetime (starts medium, swells gently as it rolls, fades)
            var size = _ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.75f);
            sizeCurve.AddKey(0.45f, 1.15f);
            sizeCurve.AddKey(1f, 1.35f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // 9. Rotation over Lifetime (slow gentle organic drift)
            var rot = _ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-12f * Mathf.Deg2Rad, 12f * Mathf.Deg2Rad);

            // 10. Noise Module (gentle organic wavy fog billows)
            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.12f;
            noise.damping = true;

            _isConfigured = true;

            if (Application.isPlaying && !_ps.isPlaying)
            {
                _ps.Play();
            }
        }

        private Material LoadSmokeMaterialFallback()
        {
#if UNITY_EDITOR
            Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/VFXPACK_IMPACT_WALLCOEUR_FreeVersion/01_Material/Smoke.mat");
            if (mat != null) return mat;
#endif
            Texture2D smokeTex = Resources.Load<Texture2D>("VFX/Smoke");
            if (smokeTex != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    Material dynMat = new Material(shader);
                    if (dynMat.HasProperty("_BaseMap")) dynMat.SetTexture("_BaseMap", smokeTex);
                    if (dynMat.HasProperty("_MainTex")) dynMat.SetTexture("_MainTex", smokeTex);
                    return dynMat;
                }
            }
            return null;
        }
    }
}
