using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;

[DisallowMultipleComponent]
public class GameFlowController : MonoBehaviour
{
    public enum State { MainMenu, Playing, Paused, Dead }
    public State state = State.MainMenu;

    [Header("Panels & Buttons")]
    public GameObject mainMenuPanel;   // chứa Start + Tutorial
    public GameObject tutorialPanel;   // panel hiển thị hướng dẫn + nút Return
    public GameObject pausePanel;      // Resume + Restart + Main Menu + Time label
    public GameObject deathPanel;      // Restart + Kills + Time
    public GameObject pauseButton;     // nút "||" (TogglePause) - nên đặt ở Canvas riêng trên cùng

    [Header("Pause Panel Texts")]
    public Text pauseTimeText;                // Unity UI Text (tùy chọn)
    public TextMeshProUGUI pauseTimeTMP;      // TMP (tùy chọn)

    [Header("Death Panel Texts")]
    public Text deathKillsText;
    public Text deathTimeText;
    public TextMeshProUGUI deathKillsTMP;
    public TextMeshProUGUI deathTimeTMP;

    [Header("Tutorial Text")]
    public Text tutorialUIText;               // có thể để trống nếu dùng TMP
    public TextMeshProUGUI tutorialTMP;       // có thể để trống nếu dùng Text
    [TextArea(6, 16)]
    public string tutorialBody =
@"• Move: left joystick
• Dash: swipe/right button (i-frames, costs oil)
• Auto-attack: fires at targets in FOV & LOS (costs oil)
• Pick up triangles: +5 / +15 oil
• Enemies: Runner (rush), Pouncer (pounce), Bomber (explode)
• Survive, manage oil, break fences with bomber explosions.";

    [Header("Gameplay HUD Roots (joystick, hearts, oil bar...)")]
    public GameObject[] gameplayHudRoots; // KÉO root của Joystick và mọi HUD in-game vào đây

    [Header("Refs")]
    public PlayerHealth playerHealth;  // auto-find nếu trống

    [Header("Main Menu (optional)")]
    public string mainMenuSceneName = ""; // để trống = reload scene hiện tại

    [Header("Audio")]
    public bool pauseAudioListener = true;

    float runStartTime;     // mốc tính thời gian chơi (Time.time) - không tăng khi pause
    bool initialised;

    void Reset()
    {
#if UNITY_2023_1_OR_NEWER
        if (!playerHealth) playerHealth = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
#else
        if (!playerHealth) playerHealth = FindObjectOfType<PlayerHealth>();
#endif
    }

    void Awake()
    {
        // Khi vào scene: dừng thời gian ở Main Menu
        Time.timeScale = 0f;
        if (pauseAudioListener) AudioListener.pause = true;
    }

    void OnEnable()
    {
#if UNITY_2023_1_OR_NEWER
        if (!playerHealth) playerHealth = Object.FindFirstObjectByType<PlayerHealth>(FindObjectsInactive.Include);
#else
        if (!playerHealth) playerHealth = FindObjectOfType<PlayerHealth>();
#endif
        if (playerHealth) playerHealth.OnDied += HandlePlayerDied;
        EnterMainMenu();
        AutoBindUI(); // <-- thêm dòng này
    }

    void OnDisable()
    {
        if (playerHealth) playerHealth.OnDied -= HandlePlayerDied;
        if (state == State.Playing)
        {
            Time.timeScale = 1f;
            if (pauseAudioListener) AudioListener.pause = false;
        }
    }

    // ========= STATES =========
    void EnterMainMenu()
    {
        state = State.MainMenu;
        Show(mainMenuPanel, true);
        Show(tutorialPanel, false);
        Show(pausePanel, false);
        Show(deathPanel, false);
        Show(pauseButton, false);

        SetGameplayHud(false, true); // ẩn HUD/joystick & mở lại CanvasGroup khi cần

        KillCounter.ResetStatic();
        initialised = true;

        Time.timeScale = 0f;
        if (pauseAudioListener) AudioListener.pause = true;
    }

    public void StartGame()
    {
        if (!initialised) EnterMainMenu();

        state = State.Playing;
        Show(mainMenuPanel, false);
        Show(tutorialPanel, false);
        Show(pausePanel, false);
        Show(deathPanel, false);
        Show(pauseButton, true);

        KillCounter.ResetStatic();
        runStartTime = Time.time; // Time.time dừng khi pause → không tính thời gian pause

        SetGameplayHud(true, true); // bật HUD/joystick

        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;
    }

    public void TogglePause()
    {
        if (state == State.Playing) PauseGame();
        else if (state == State.Paused) ResumeGame();
    }

    void PauseGame()
    {
        if (state != State.Playing) return;
        state = State.Paused;

        UpdatePauseTimeLabel();
        Show(pausePanel, true);
        Show(pauseButton, false);

        SetGameplayHud(false, true); // ẩn HUD/joystick để không chặn UI

        Time.timeScale = 0f;
        if (pauseAudioListener) AudioListener.pause = true;
    }

    public void Btn_Resume() => ResumeGame();

    void ResumeGame()
    {
        if (state != State.Paused) return;
        state = State.Playing;

        Show(pausePanel, false);
        Show(pauseButton, true);

        SetGameplayHud(true, true); // bật lại HUD/joystick

        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;
    }

    void HandlePlayerDied()
    {
        state = State.Dead;

        Time.timeScale = 0f;
        if (pauseAudioListener) AudioListener.pause = true;

        int kills = KillCounter.Instance ? KillCounter.Instance.TotalKills : 0;
        float dt = Mathf.Max(0f, Time.time - runStartTime);
        string tstr = FormatTimeMMSS(dt);

        SetText(deathKillsText, $"Kills: {kills}");
        SetText(deathKillsTMP, $"Kills: {kills}");
        SetText(deathTimeText, $"Time: {tstr}");
        SetText(deathTimeTMP, $"Time: {tstr}");

        Show(pauseButton, false);
        Show(pausePanel, false);
        Show(deathPanel, true);

        SetGameplayHud(false, true);
    }

    // ========= MAIN MENU: Tutorial =========
    public void Btn_TutorialOpen()
    {
        if (state != State.MainMenu) return;

        if (!string.IsNullOrEmpty(tutorialBody))
        {
            SetText(tutorialUIText, tutorialBody);
            SetText(tutorialTMP, tutorialBody);
        }

        Show(mainMenuPanel, false);
        Show(tutorialPanel, true);

        Time.timeScale = 0f;
        if (pauseAudioListener) AudioListener.pause = true;
    }

    public void Btn_TutorialReturn()
    {
        if (state != State.MainMenu) return;
        Show(tutorialPanel, false);
        Show(mainMenuPanel, true);
    }

    // ========= BUTTONS =========
    public void Btn_Restart()
    {
        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void Btn_MainMenu()
    {
        Time.timeScale = 1f;
        if (pauseAudioListener) AudioListener.pause = false;

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ========= HELPERS =========
    void UpdatePauseTimeLabel()
    {
        string tstr = FormatTimeMMSS(Mathf.Max(0f, Time.time - runStartTime));
        SetText(pauseTimeText, $"Time: {tstr}");
        SetText(pauseTimeTMP, $"Time: {tstr}");
    }

    static void Show(GameObject go, bool on) { if (go) go.SetActive(on); }

    static void SetText(Text t, string s) { if (t) t.text = s; }
    static void SetText(TextMeshProUGUI t, string s) { if (t) t.text = s; }

    static string FormatTimeMMSS(float seconds)
    {
        int s = Mathf.FloorToInt(seconds);
        int m = s / 60; int ss = s % 60;
        return $"{m:00}:{ss:00}";
    }
    void AutoBindUI()
    {
        // Pause time
        if (!pauseTimeTMP && !pauseTimeText && pausePanel)
        {
            pauseTimeTMP = pausePanel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
            if (!pauseTimeTMP)
                pauseTimeText = pausePanel.GetComponentsInChildren<Text>(true).FirstOrDefault();
        }
        // Death kills & time
        if (deathPanel)
        {
            if (!deathKillsTMP && !deathKillsText)
            {
                // ưu tiên tên chứa "Kill"
                deathKillsTMP = deathPanel.GetComponentsInChildren<TextMeshProUGUI>(true)
                                           .FirstOrDefault(t => t.name.ToLower().Contains("kill"))
                                 ?? deathPanel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
                if (!deathKillsTMP)
                    deathKillsText = deathPanel.GetComponentsInChildren<Text>(true)
                                               .FirstOrDefault(t => t.name.ToLower().Contains("kill"))
                                    ?? deathPanel.GetComponentsInChildren<Text>(true).FirstOrDefault();
            }
            if (!deathTimeTMP && !deathTimeText)
            {
                deathTimeTMP = deathPanel.GetComponentsInChildren<TextMeshProUGUI>(true)
                                         .FirstOrDefault(t => t.name.ToLower().Contains("time"))
                               ?? deathPanel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
                if (!deathTimeTMP)
                    deathTimeText = deathPanel.GetComponentsInChildren<Text>(true)
                                              .FirstOrDefault(t => t.name.ToLower().Contains("time"))
                                   ?? deathPanel.GetComponentsInChildren<Text>(true).FirstOrDefault();
            }
        }
        // Tutorial body
        if (!tutorialTMP && !tutorialUIText && tutorialPanel)
        {
            tutorialTMP = tutorialPanel.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
            if (!tutorialTMP)
                tutorialUIText = tutorialPanel.GetComponentsInChildren<Text>(true).FirstOrDefault();
        }
    }

    /// Bật/tắt HUD: vừa SetActive các root, vừa bật lại mọi CanvasGroup con (interactable/blocksRaycasts).
    void SetGameplayHud(bool on, bool alsoFixCanvasGroups)
    {
        if (gameplayHudRoots == null) return;

        for (int i = 0; i < gameplayHudRoots.Length; i++)
        {
            var root = gameplayHudRoots[i];
            if (!root) continue;

            root.SetActive(on);

            if (alsoFixCanvasGroups)
            {
                var cgSelf = root.GetComponent<CanvasGroup>();
                if (cgSelf)
                {
                    if (on && cgSelf.alpha <= 0f) cgSelf.alpha = 1f;
                    cgSelf.interactable = on;
                    cgSelf.blocksRaycasts = on;
                }

                var cgs = root.GetComponentsInChildren<CanvasGroup>(true);
                for (int c = 0; c < cgs.Length; c++)
                {
                    if (on && cgs[c].alpha <= 0f) cgs[c].alpha = 1f;
                    cgs[c].interactable = on;
                    cgs[c].blocksRaycasts = on;
                }
            }
        }
    }
}
