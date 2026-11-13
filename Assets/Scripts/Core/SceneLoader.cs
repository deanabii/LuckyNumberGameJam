using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace LuckyNumber.Core
{
    /// <summary>
    /// SceneLoader: Manajer transisi scene Unity yang tangguh dan fleksibel.
    /// Fitur:
    /// - Singleton persisten (DontDestroyOnLoad).
    /// - Auto-setup Canvas Fade (langsung bekerja tanpa perlu setup UI manual di setiap scene).
    /// - Dukungan transisi Fade-out & Fade-in halus (unscaled time, aman saat pause).
    /// - Asynchronous Loading dengan indikator progress / loading bar.
    /// - Mudah disambungkan ke UI Button OnClick() lewat Unity Inspector.
    /// - Static helper: SceneLoader.Load("NamaScene"), SceneLoader.Reload(), SceneLoader.Quit().
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [Header("Transition Settings")]
        [Tooltip("Warna overlay saat transisi antar scene")]
        [SerializeField] private Color fadeColor = Color.black;

        [Tooltip("Durasi transisi memudar (detik)")]
        [SerializeField] private float defaultFadeDuration = 0.5f;

        [Tooltip("Apakah otomatis memudar masuk (Fade-in) saat scene baru selesai dimuat?")]
        [SerializeField] private bool autoFadeInOnLoad = true;

        [Header("Optional UI References (Bisa Dikosongkan)")]
        [Tooltip("Canvas khusus untuk transisi. Jika kosong, script akan membuatnya otomatis.")]
        [SerializeField] private Canvas customTransitionCanvas;

        [Tooltip("CanvasGroup untuk fade overlay. Jika kosong, script akan membuatnya otomatis.")]
        [SerializeField] private CanvasGroup customFadeGroup;

        [Tooltip("Slider progress bar opsional untuk loading asynchronous.")]
        [SerializeField] private Slider progressBar;

        [Tooltip("Teks persentase loading opsional (TMP).")]
        [SerializeField] private TextMeshProUGUI progressText;

        [Header("Credits Modal Settings")]
        [Tooltip("Panel / Modal Credit di scene. Bisa di-assign manual di Inspector atau dicari otomatis jika kosong.")]
        [SerializeField] private GameObject creditsPanel;

        [Tooltip("CanvasGroup dari Panel Credit untuk efek fade in/out mulus.")]
        [SerializeField] private CanvasGroup creditsCanvasGroup;

        [Tooltip("Durasi animasi fade panel credit (detik).")]
        [SerializeField] private float creditsFadeDuration = 0.25f;

        [Tooltip("Tutup panel credit otomatis saat pemain menekan tombol ESC?")]
        [SerializeField] private bool closeCreditsOnEsc = true;

        // Internal references
        private Canvas _fadeCanvas;
        private CanvasGroup _fadeCanvasGroup;
        private Image _fadeImage;
        private bool _isTransitioning;
        private bool _isCreditsOpen;
        private Coroutine _creditFadeCoroutine;

        public bool IsCreditsOpen => _isCreditsOpen;

        // Events
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadCompleted;
        public event Action<float> OnLoadingProgressUpdated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            // Menjamin SceneLoader selalu ada di game runtime meski belum ditaruh manual di hierarki
            if (Instance == null)
            {
                GameObject loaderObj = new GameObject("[SceneLoader_Global]");
                loaderObj.AddComponent<SceneLoader>();
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;

                // Hanya DontDestroyOnLoad jika ini adalah root GameObject independen (bukan Canvas scene)
                if (transform.parent == null && (gameObject.name == "[SceneLoader_Global]" || GetComponent<Canvas>() == null))
                {
                    DontDestroyOnLoad(gameObject);
                }

                InitializeFadeUI();
            }
            else if (Instance != this)
            {
                // PENTING: JANGAN hancurkan gameObject karena gameObject bisa jadi adalah Canvas atau UI penting!
                // JANGAN pula hancurkan komponen ini agar referensi OnClick() di Inspector tetap valid.
                // Instance lokal ini akan otomatis meneruskan panggilan (delegator) ke singleton Instance global.
                if (creditsPanel != null)
                {
                    Instance.SetCreditsPanel(creditsPanel);
                }
            }
        }

        private void OnEnable()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
            }
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= HandleSceneLoaded;
            }
        }

        private void Start()
        {
            if (Instance == this)
            {
                // Jika saat game mulai layar tertutup fade, buka perlahan
                if (_fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.05f)
                {
                    StartCoroutine(FadeRoutine(1f, 0f, defaultFadeDuration));
                }
            }
            else
            {
                if (creditsPanel != null)
                {
                    Instance.SetCreditsPanel(creditsPanel);
                }
            }
        }

        /// <summary>
        /// Membuat atau menyiapkan overlay Canvas khusus transisi di layer paling atas.
        /// </summary>
        private void InitializeFadeUI()
        {
            if (customTransitionCanvas != null && customFadeGroup != null)
            {
                _fadeCanvas = customTransitionCanvas;
                _fadeCanvasGroup = customFadeGroup;
                return;
            }

            // Buat Canvas UI otomatis
            GameObject canvasObj = new GameObject("TransitionCanvas");
            canvasObj.transform.SetParent(transform, false);

            _fadeCanvas = canvasObj.AddComponent<Canvas>();
            _fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _fadeCanvas.sortingOrder = 9999; // Prioritas paling depan di atas semua UI

            canvasObj.AddComponent<CanvasScaler>();

            _fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
            _fadeCanvasGroup.alpha = 0f;
            _fadeCanvasGroup.blocksRaycasts = false;
            _fadeCanvasGroup.interactable = false;

            // Buat Image full screen hitam
            GameObject imgObj = new GameObject("FadeOverlayImage");
            imgObj.transform.SetParent(canvasObj.transform, false);

            _fadeImage = imgObj.AddComponent<Image>();
            _fadeImage.color = fadeColor;

            RectTransform rect = imgObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Pastikan timeScale selalu dinormalkan kembali jika scene sebelumnya dalam keadaan pause
            Time.timeScale = 1f;

            OnSceneLoadCompleted?.Invoke(scene.name);

            _isCreditsOpen = false;

            if (autoFadeInOnLoad && _fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.01f)
            {
                StartCoroutine(FadeRoutine(1f, 0f, defaultFadeDuration));
            }
        }

        private void Update()
        {
            if (closeCreditsOnEsc)
            {
                GameObject panel = creditsPanel;
                if (panel == null && _isCreditsOpen) panel = ResolveCreditsPanel();

                if (panel != null && panel.activeSelf)
                {
                    bool escPressed = false;
                    try
                    {
                        var kb = UnityEngine.InputSystem.Keyboard.current;
                        if (kb != null && kb.escapeKey.wasPressedThisFrame) escPressed = true;
                    }
                    catch { }

                    if (!escPressed && Input.GetKeyDown(KeyCode.Escape))
                    {
                        escPressed = true;
                    }

                    if (escPressed)
                    {
                        HideCredits();
                    }
                }
            }
        }

        // =========================================================================
        // PUBLIC API (BISA DIHUBUNGKAN KE UI BUTTON DI INSPECTOR)
        // =========================================================================

        /// <summary>
        /// Memuat scene berdasarkan nama dengan efek fade-to-black halus.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            if (Instance != null && Instance != this)
            {
                Instance.LoadScene(sceneName);
                return;
            }
            LoadSceneWithFade(sceneName, defaultFadeDuration);
        }

        /// <summary>
        /// Memuat scene berdasarkan nama dengan durasi fade kustom.
        /// </summary>
        public void LoadSceneWithFade(string sceneName, float duration = -1f)
        {
            if (Instance != null && Instance != this)
            {
                Instance.LoadSceneWithFade(sceneName, duration);
                return;
            }

            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[SceneLoader] Nama scene kosong atau null!");
                return;
            }

            // Jika user memanggil LoadScene("Credit") padahal Credit adalah UI Panel di MainMenu, buka Panel Credit
            if ((sceneName.Equals("Credit", StringComparison.OrdinalIgnoreCase) ||
                 sceneName.Equals("Credits", StringComparison.OrdinalIgnoreCase)) &&
                !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                ShowCredits();
                return;
            }

            if (_isTransitioning)
            {
                Debug.LogWarning($"[SceneLoader] Transisi sedang berlangsung, abaikan permintaan load: {sceneName}");
                return;
            }

            float fadeTime = duration > 0f ? duration : defaultFadeDuration;
            StartCoroutine(LoadSceneAsyncRoutine(sceneName, fadeTime));
        }

        /// <summary>
        /// Memuat scene secara instan tanpa efek fade.
        /// </summary>
        public void LoadSceneInstant(string sceneName)
        {
            if (Instance != null && Instance != this)
            {
                Instance.LoadSceneInstant(sceneName);
                return;
            }

            if (string.IsNullOrEmpty(sceneName)) return;
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Memuat scene berdasarkan build index di Build Settings.
        /// </summary>
        public void LoadSceneByIndex(int sceneBuildIndex)
        {
            if (Instance != null && Instance != this)
            {
                Instance.LoadSceneByIndex(sceneBuildIndex);
                return;
            }

            if (sceneBuildIndex < 0 || sceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"[SceneLoader] Build index {sceneBuildIndex} di luar rentang Build Settings!");
                return;
            }

            string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneBuildIndex);
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            LoadScene(sceneName);
        }

        /// <summary>
        /// Muat ulang (restart) scene aktif saat ini.
        /// </summary>
        public void ReloadCurrentScene()
        {
            if (Instance != null && Instance != this)
            {
                Instance.ReloadCurrentScene();
                return;
            }

            string currentSceneName = SceneManager.GetActiveScene().name;
            LoadScene(currentSceneName);
        }

        /// <summary>
        /// Muat scene berikutnya sesuai urutan build index.
        /// </summary>
        public void LoadNextScene()
        {
            if (Instance != null && Instance != this)
            {
                Instance.LoadNextScene();
                return;
            }

            int nextIndex = (SceneManager.GetActiveScene().buildIndex + 1) % SceneManager.sceneCountInBuildSettings;
            LoadSceneByIndex(nextIndex);
        }

        /// <summary>
        /// Menutup aplikasi / menghentikan Play Mode di editor.
        /// </summary>
        public void QuitGame()
        {
            if (Instance != null && Instance != this)
            {
                Instance.QuitGame();
                return;
            }

            Debug.Log("[SceneLoader] Keluar dari game...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Menampilkan Panel / Modal Credit di layar.
        /// </summary>
        public void ShowCredits()
        {
            GameObject panel = ResolveCreditsPanel();
            if (panel == null && Instance != null && Instance != this)
            {
                panel = Instance.ResolveCreditsPanel();
            }

            if (panel == null)
            {
                Debug.LogWarning("[SceneLoader] creditsPanel belum diatur di Inspector dan tidak ditemukan di scene!");
                return;
            }

            _isCreditsOpen = true;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();

            if (creditsCanvasGroup != null)
            {
                if (_creditFadeCoroutine != null) StopCoroutine(_creditFadeCoroutine);
                _creditFadeCoroutine = StartCoroutine(FadeCreditsRoutine(open: true));
            }
        }

        /// <summary>
        /// Menutup Panel / Modal Credit di layar.
        /// </summary>
        public void HideCredits()
        {
            GameObject panel = ResolveCreditsPanel();
            if (panel == null && Instance != null && Instance != this)
            {
                panel = Instance.ResolveCreditsPanel();
            }

            if (panel == null)
            {
                _isCreditsOpen = false;
                return;
            }

            _isCreditsOpen = false;

            if (creditsCanvasGroup != null && panel.activeInHierarchy)
            {
                if (_creditFadeCoroutine != null) StopCoroutine(_creditFadeCoroutine);
                _creditFadeCoroutine = StartCoroutine(FadeCreditsRoutine(open: false));
            }
            else
            {
                panel.SetActive(false);
            }
        }

        /// <summary>
        /// Membuka atau menutup Panel Credit (toggle).
        /// </summary>
        public void ToggleCredits()
        {
            GameObject panel = ResolveCreditsPanel();
            if (panel != null)
            {
                if (panel.activeSelf)
                    HideCredits();
                else
                    ShowCredits();
            }
            else
            {
                if (_isCreditsOpen)
                    HideCredits();
                else
                    ShowCredits();
            }
        }

        // Alias fungsi publik INSTANCE agar bisa langsung dipilih di Unity Inspector Button OnClick()
        public void OpenCredits() => ShowCredits();
        public void CloseCredits() => HideCredits();
        public void ToggleCreditsModal() => ToggleCredits();
        public void OpenCreditsModal() => ShowCredits();
        public void CloseCreditsModal() => HideCredits();

        /// <summary>
        /// Menghubungkan GameObject Panel Credit secara runtime jika diperlukan.
        /// </summary>
        public void SetCreditsPanel(GameObject panel)
        {
            if (panel != null && IsMenuButtonObject(panel)) return;
            creditsPanel = panel;
            if (panel != null)
            {
                creditsCanvasGroup = panel.GetComponent<CanvasGroup>();
            }
        }

        // =========================================================================
        // STATIC CONVENIENCE METHODS (BISA DIPANGGIL DARI SCRIPT APAPUN TANPA REFERENSI)
        // =========================================================================

        public static void OpenCreditsStatic()
        {
            var local = FindFirstObjectByType<SceneLoader>();
            if (local != null) local.ShowCredits();
            else if (Instance != null) Instance.ShowCredits();
        }

        public static void CloseCreditsStatic()
        {
            var local = FindFirstObjectByType<SceneLoader>();
            if (local != null) local.HideCredits();
            else if (Instance != null) Instance.HideCredits();
        }

        public static void Load(string sceneName, bool useFade = true)
        {
            if (Instance == null)
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            if (useFade)
                Instance.LoadScene(sceneName);
            else
                Instance.LoadSceneInstant(sceneName);
        }

        public static void Reload(bool useFade = true)
        {
            if (Instance != null && useFade)
                Instance.ReloadCurrentScene();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static void Quit()
        {
            if (Instance != null)
                Instance.QuitGame();
            else
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        // =========================================================================
        // COROUTINES TRANSISI DAN ASYNC LOADING
        // =========================================================================

        private IEnumerator LoadSceneAsyncRoutine(string sceneName, float fadeDuration)
        {
            _isTransitioning = true;
            OnSceneLoadStarted?.Invoke(sceneName);

            // Blok raycast selama proses transisi agar user tidak spam klik tombol
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.blocksRaycasts = true;
            }

            // Tampilkan progress bar opsional jika ada
            if (progressBar != null)
            {
                progressBar.gameObject.SetActive(true);
                progressBar.value = 0f;
            }
            if (progressText != null)
            {
                progressText.gameObject.SetActive(true);
                progressText.text = "Loading... 0%";
            }

            // 1. Fade Out (Layar menjadi gelap)
            yield return StartCoroutine(FadeRoutine(0f, 1f, fadeDuration));

            // 2. Load Scene Asynchronously di latar belakang
            AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
            if (asyncOp == null)
            {
                Debug.LogError($"[SceneLoader] Gagal memuat scene '{sceneName}'. Pastikan scene sudah dimasukkan ke File -> Build Settings!");
                _isTransitioning = false;
                if (_fadeCanvasGroup != null) _fadeCanvasGroup.blocksRaycasts = false;
                yield break;
            }

            // Jangan langsung aktifkan scene jika ingin kontrol persentase loading
            asyncOp.allowSceneActivation = false;

            while (!asyncOp.isDone)
            {
                // Unity async loading mencapai 0.9 saat asset sudah selesai di-load
                float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                OnLoadingProgressUpdated?.Invoke(progress);

                if (progressBar != null)
                    progressBar.value = progress;
                if (progressText != null)
                    progressText.text = $"Loading... {Mathf.RoundToInt(progress * 100f)}%";

                // Jika loading sudah 0.9, aktifkan scene
                if (asyncOp.progress >= 0.9f)
                {
                    // Delay singkat untuk kehalusan visual
                    yield return new WaitForSecondsRealtime(0.15f);
                    asyncOp.allowSceneActivation = true;
                }

                yield return null;
            }

            // Sembunyikan progress bar jika ada
            if (progressBar != null) progressBar.gameObject.SetActive(false);
            if (progressText != null) progressText.gameObject.SetActive(false);

            // 3. Fade In (Layar kembali terang di scene baru) ditangani oleh HandleSceneLoaded atau langsung di sini
            if (autoFadeInOnLoad)
            {
                yield return StartCoroutine(FadeRoutine(1f, 0f, fadeDuration));
            }

            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.blocksRaycasts = false;
            }

            _isTransitioning = false;
        }

        private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration)
        {
            if (_fadeCanvasGroup == null) yield break;

            _fadeCanvasGroup.blocksRaycasts = true;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // Gunakan unscaledDeltaTime agar tetap berjalan meskipun game sedang di-pause (Time.timeScale = 0)
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            _fadeCanvasGroup.alpha = targetAlpha;
            _fadeCanvasGroup.blocksRaycasts = (targetAlpha > 0.05f);
        }

        private static bool IsMenuButtonObject(GameObject go)
        {
            if (go == null) return false;
            // Jangan salah mengenali tombol menu "Credit" (yang memiliki komponen Button) sebagai Panel Credit!
            return go.GetComponent<Button>() != null ||
                   go.GetComponent<LuckyNumber.UI.MainMenuButtonHoverEffect>() != null;
        }

        private GameObject ResolveCreditsPanel()
        {
            if (creditsPanel != null && !IsMenuButtonObject(creditsPanel))
            {
                if (creditsCanvasGroup == null)
                    creditsCanvasGroup = creditsPanel.GetComponent<CanvasGroup>();
                return creditsPanel;
            }

            string[] candidateNames = new string[] { "Credit", "Credits", "CreditPanel", "CreditsPanel", "Panel_Credit", "Modal_Credits", "CreditsModal" };

            // 1. Coba cari di child langsung GameObject ini (misal Canvas -> Credit)
            foreach (string cName in candidateNames)
            {
                Transform t = transform.Find(cName);
                if (t != null && !IsMenuButtonObject(t.gameObject))
                {
                    creditsPanel = t.gameObject;
                    creditsCanvasGroup = creditsPanel.GetComponent<CanvasGroup>();
                    return creditsPanel;
                }
            }

            // 2. Coba cari di MainMenuController jika ada
            var menuCtrl = FindFirstObjectByType<LuckyNumber.UI.MainMenuController>();
            if (menuCtrl != null && menuCtrl.CreditPanel != null && !IsMenuButtonObject(menuCtrl.CreditPanel))
            {
                creditsPanel = menuCtrl.CreditPanel;
                creditsCanvasGroup = creditsPanel.GetComponent<CanvasGroup>();
                return creditsPanel;
            }

            // 3. Coba cari di seluruh root scene aktif (termasuk hierarki objek non-aktif, skip yang merupakan Button)
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                GameObject[] roots = activeScene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var tr in allTransforms)
                    {
                        if (IsMenuButtonObject(tr.gameObject)) continue;
                        foreach (string cName in candidateNames)
                        {
                            if (tr.name.Equals(cName, StringComparison.OrdinalIgnoreCase))
                            {
                                creditsPanel = tr.gameObject;
                                creditsCanvasGroup = creditsPanel.GetComponent<CanvasGroup>();
                                return creditsPanel;
                            }
                        }
                    }
                }
            }

            // 4. Coba cari di seluruh objek loaded scene
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (go.hideFlags != HideFlags.None || !go.scene.isLoaded || IsMenuButtonObject(go)) continue;
                foreach (string cName in candidateNames)
                {
                    if (go.name.Equals(cName, StringComparison.OrdinalIgnoreCase))
                    {
                        creditsPanel = go;
                        creditsCanvasGroup = creditsPanel.GetComponent<CanvasGroup>();
                        return creditsPanel;
                    }
                }
            }

            return null;
        }

        private IEnumerator FadeCreditsRoutine(bool open)
        {
            if (creditsCanvasGroup == null) yield break;

            if (open && creditsPanel != null)
            {
                creditsPanel.SetActive(true);
            }

            float startAlpha = creditsCanvasGroup.alpha;
            float targetAlpha = open ? 1.0f : 0.0f;
            float elapsed = 0f;
            float duration = creditsFadeDuration > 0f ? creditsFadeDuration : 0.25f;

            creditsCanvasGroup.blocksRaycasts = open;
            creditsCanvasGroup.interactable = open;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                creditsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            creditsCanvasGroup.alpha = targetAlpha;
            creditsCanvasGroup.blocksRaycasts = open;
            creditsCanvasGroup.interactable = open;

            if (!open && creditsPanel != null)
            {
                creditsPanel.SetActive(false);
            }

            _creditFadeCoroutine = null;
        }
    }
}
