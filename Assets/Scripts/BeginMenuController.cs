using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Interaction logic for the authored Begin scene UI. All visual objects and
/// event bindings live in Begin.unity so they remain inspectable and editable.
/// </summary>
public sealed class BeginMenuController : MonoBehaviour
{
    private const string GameplayScene = "Level_01_BambooCourtyard";

    [Header("State Groups")]
    [SerializeField] private GameObject panelMainMenu;
    [SerializeField] private GameObject panelAudioOptions;

    [Header("Navigation")]
    [SerializeField] private Button buttonStart;
    [SerializeField] private Button buttonBack;

    [Header("Audio Controls")]
    [SerializeField] private Slider sliderMaster;
    [SerializeField] private Slider sliderMusic;
    [SerializeField] private Slider sliderSfx;
    [SerializeField] private Text textMasterValue;
    [SerializeField] private Text textMusicValue;
    [SerializeField] private Text textSfxValue;

    private void Awake()
    {
        GameAudioSettings.Load();

        sliderMaster.SetValueWithoutNotify(GameAudioSettings.MasterVolume);
        sliderMusic.SetValueWithoutNotify(GameAudioSettings.MusicVolume);
        sliderSfx.SetValueWithoutNotify(GameAudioSettings.SfxVolume);

        UpdatePercent(textMasterValue, GameAudioSettings.MasterVolume);
        UpdatePercent(textMusicValue, GameAudioSettings.MusicVolume);
        UpdatePercent(textSfxValue, GameAudioSettings.SfxVolume);

        panelMainMenu.SetActive(true);
        panelAudioOptions.SetActive(false);
    }

    private void Start()
    {
        Select(buttonStart);
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true && panelAudioOptions.activeSelf)
        {
            ShowMainMenu();
        }
    }

    public void StartGame()
    {
        GameAudioSettings.Save();
        SceneManager.LoadScene(GameplayScene);
    }

    public void ShowAudioOptions()
    {
        panelMainMenu.SetActive(false);
        panelAudioOptions.SetActive(true);
        Select(buttonBack);
    }

    public void ShowMainMenu()
    {
        GameAudioSettings.Save();
        panelAudioOptions.SetActive(false);
        panelMainMenu.SetActive(true);
        Select(buttonStart);
    }

    public void SetMasterVolume(float value)
    {
        GameAudioSettings.MasterVolume = value;
        UpdatePercent(textMasterValue, value);
    }

    public void SetMusicVolume(float value)
    {
        GameAudioSettings.MusicVolume = value;
        UpdatePercent(textMusicValue, value);
    }

    public void SetSfxVolume(float value)
    {
        GameAudioSettings.SfxVolume = value;
        UpdatePercent(textSfxValue, value);
    }

    public void QuitGame()
    {
        GameAudioSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void Select(Selectable selectable)
    {
        if (EventSystem.current != null && selectable != null)
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    private static void UpdatePercent(Text label, float value)
    {
        if (label != null)
        {
            label.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
