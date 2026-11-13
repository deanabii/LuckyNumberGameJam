using UnityEngine;
using LuckyNumber.Characters;

namespace LuckyNumber.Core
{
    /// <summary>
    /// SceneTrigger: Komponen untuk memindahkan scene saat player menyentuh trigger
    /// collider atau menekan tombol interaksi [E] di dekat portal/pintu/area transisi.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SceneTrigger : MonoBehaviour, IWorldInteractable
    {
        [Header("Target Scene")]
        [Tooltip("Nama scene tujuan yang ingin dimuat (harus terdaftar di File -> Build Settings)")]
        [SerializeField] private string targetSceneName = "TestMap";

        [Header("Trigger Mode")]
        [Tooltip("Jika true, scene langsung berpindah seketika player melangkah masuk collider trigger.")]
        [SerializeField] private bool triggerOnEnter = true;

        [Tooltip("Jika true, player harus menekan [E] untuk masuk.")]
        [SerializeField] private bool requireInteraction = false;

        [Tooltip("Teks prompt yang muncul jika requireInteraction = true.")]
        [SerializeField] private string customPrompt = "[E] Masuk ke Area Baru";

        [Tooltip("Radius interaksi tombol [E].")]
        [SerializeField] private float interactionRadius = 2.0f;

        [Header("Transition")]
        [Tooltip("Gunakan efek fade-to-black saat berpindah scene.")]
        [SerializeField] private bool useFade = true;

        [Tooltip("Durasi fade (detik).")]
        [SerializeField] private float fadeDuration = 0.5f;

        // IWorldInteractable implementation
        public Transform Transform => transform;
        public string InteractionPrompt => customPrompt;
        public float InteractionRadius => interactionRadius;
        public bool CanInteract => requireInteraction && enabled;

        private Collider2D _col;

        private void Awake()
        {
            _col = GetComponent<Collider2D>();
            if (_col != null && !_col.isTrigger)
            {
                _col.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            if (requireInteraction)
            {
                PlayerController.RegisterInteractable(this);
            }
        }

        private void OnDisable()
        {
            if (requireInteraction)
            {
                PlayerController.UnregisterInteractable(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!triggerOnEnter) return;

            // Pastikan yang menyentuh adalah player
            if (other.CompareTag("Player") || other.GetComponent<PlayerController>() != null)
            {
                TriggerSceneChange();
            }
        }

        public void Interact(PlayerController player)
        {
            TriggerSceneChange();
        }

        public void TriggerSceneChange()
        {
            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning($"[SceneTrigger] targetSceneName pada {gameObject.name} masih kosong!");
                return;
            }

            Debug.Log($"[SceneTrigger] Berpindah ke scene '{targetSceneName}'...");

            if (SceneLoader.Instance != null)
            {
                if (useFade)
                    SceneLoader.Instance.LoadSceneWithFade(targetSceneName, fadeDuration);
                else
                    SceneLoader.Instance.LoadSceneInstant(targetSceneName);
            }
            else
            {
                SceneLoader.Load(targetSceneName, useFade);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.45f);
            Collider2D c = GetComponent<Collider2D>();
            if (c != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                if (c is BoxCollider2D box)
                {
                    Gizmos.DrawCube(box.offset, box.size);
                    Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.9f);
                    Gizmos.DrawWireCube(box.offset, box.size);
                }
                else if (c is CircleCollider2D circle)
                {
                    Gizmos.DrawSphere(circle.offset, circle.radius);
                    Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.9f);
                    Gizmos.DrawWireSphere(circle.offset, circle.radius);
                }
            }
        }
#endif
    }
}
