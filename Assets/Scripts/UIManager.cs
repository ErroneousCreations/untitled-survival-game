using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine.InputSystem;
using System.Linq;

public class UIManager : MonoBehaviour
{
    private static UIManager instance;

    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject pauseMenu;

    public static Canvas GetCanvas => instance.canvas;

    private void Awake()
    {
        instance = this;

        instance.SetLobbyCustomisationSliders();

        VivoxManager.InitialisationComplete += () => { VivoxManager.InputDevicesChanged += UpdateInputDevices; };
    }

    public static void ResetUI()
    {
        ShowVitalsIndication(false);
        HideGameOverScreen();
        SetReadyTimerText("");
        SetReadyUpButtonText("Ready Up");
        instance.StopAllCoroutines();
        instance.RegionTitleText.text = "";
        instance.regiontitlecountdown = 0;
        instance.markedForDeathIcon.alpha = 0;
        instance.markedForDeathIcon.DOKill();
        instance.markedTargetTime = 0;
        instance.markedTarget = null;
    }

    public static void TogglePauseMenu(bool open)
    {
        instance.pauseMenu.SetActive(open);
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        if (open) { instance.ResetPauseSettings(); }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && (Player.LocalPlayer || GameManager.IsSpectating))
        {
            TogglePauseMenu(!GetPauseMenuOpen);
        }
        respawnButton.SetActive(!VivoxManager.LeavingChannel);

        if (regiontitlecountdown > 0)
        {
            regiontitlecountdown -= Time.deltaTime;
            if (regiontitlecountdown <= 0)
            {
                if(currDisplayCoroutine != null) { StopCoroutine(currDisplayCoroutine); RegionTitleText.DOKill(); }
                currDisplayCoroutine = StartCoroutine(DisplayRegionTitle());
            }
        }
        if (!NetworkManager.Singleton) { return; }
        var k = 0;
        foreach (var butt in GamemodeButtons)
        {
            if(k>0) { butt.gameObject.SetActive(NetworkManager.Singleton.ConnectedClients.Count > 1); } 
            butt.interactable = NetworkManager.Singleton.IsServer;
            k++;
        }

        foreach (var butt in SaveslotButtons)
        {
            butt.interactable = NetworkManager.Singleton.IsServer;
        }

        SeedInput.interactable = NetworkManager.Singleton.IsServer;

        WinscreenHostButton.SetActive(NetworkManager.Singleton.IsServer);

        if(GameManager.GetGamestate == GameStateEnum.Lobby)
        {
            instance.selectedSaveslot.transform.position = instance.SaveslotButtons[currSave].transform.position;
            instance.selectedGamemode.transform.position = instance.GamemodeButtons[(int)currMode].transform.position;
        }

        if (markedTarget)
        {
            markedForDeathIcon.transform.localPosition = Extensions.TransformToHUDSpace(markedTarget.position);
            markedForDeathIcon.transform.localScale = new Vector3(1, 1, 1) * (1 + Mathf.PingPong(Time.time * 2, 0.5f));
            markedForDeathIcon.gameObject.SetActive(markedForDeathIcon.transform.position.z >= 0);
            markedTargetTime -= Time.deltaTime;
            if (markedTargetTime <= 0)
            {
                markedForDeathIcon.DOKill();
                markedForDeathIcon.DOFade(0, 1f);
                markedTarget = null;
            }
        }
    }

    public static bool GetPauseMenuOpen => instance.pauseMenu.activeSelf;

    [SerializeField] private GameObject muteIcon;

    public static void SetMuteIcon(bool mute)
    {
        instance.muteIcon.SetActive(mute);
    }

    [SerializeField] private Image VitalsIndicatorImage;
    [SerializeField] private CanvasGroup VitalsIndicatorCanvasGroup;
    [SerializeField] private Sprite ThumbsDown, ThumbsUp;

    public static void ShowVitalsIndication(bool thumbsup)
    {
        instance.VitalsIndicatorImage.sprite = thumbsup ? instance.ThumbsUp : instance.ThumbsDown;
        instance.VitalsIndicatorCanvasGroup.alpha = 0;
        instance.VitalsIndicatorCanvasGroup.DOKill();
        instance.VitalsIndicatorCanvasGroup.DOFade(1, 0.25f).onComplete += () =>
        {
            instance.VitalsIndicatorCanvasGroup.DOFade(0, 1f);
        };
    }

    [SerializeField] private GameObject gameOverScreen, respawnButton;
    [SerializeField] private CanvasGroup deathText;
    [SerializeField] private TMP_Text respawnbuttonText;

    public void Respawn()
    {
        if(GameManager.GetGameMode == GameModeEnum.Survival) { GameManager.RespawnPlayer(); }
        else { GameManager.EnableSpectator(); }
        HideGameOverScreen();
    }

    public static void ShowGameOverScreen()
    {
        instance.respawnbuttonText.text = GameManager.GetGameMode == GameModeEnum.Survival ? "Respawn" : "Spectate";
        instance.gameOverScreen.SetActive(true);
        instance.deathText.alpha = 0;
        instance.deathText.DOKill();
        instance.Invoke(nameof(ShowGameoverText), 1f);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void ShowGameoverText()
    {
        instance.deathText.DOFade(1, 10);
    }

    public static void HideGameOverScreen()
    {
        instance.gameOverScreen.SetActive(false);
        instance.deathText.alpha = 0;
        instance.deathText.DOKill();
    }

    //lobby players ui
    [SerializeField] private Image basePlayerIcon;
    [SerializeField] private Sprite[] playerIcons;
    [SerializeField] private Sprite[] specialPlayerIcons;
    [SerializeField] private Sprite ready, notready;

    private Dictionary<ulong, GameObject> speakingIndicators = new();

    public static void SetSpeakingIndicators(string whostalking)
    {
        if(instance.speakingIndicators.Count == 0) { return; }

        var talkers = whostalking.Split('|');

        foreach (var indicator in instance.speakingIndicators)
        {
            indicator.Value.SetActive(talkers.Contains(indicator.Key.ToString()));
        }
    }

    public static void UpdatePlayerList(Dictionary<ulong, string> conns, List<ulong> readied)
    {
        foreach (Transform child in instance.basePlayerIcon.transform.parent)
        {
            if (child.gameObject.activeSelf) { Destroy(child.gameObject); }
        }
        instance.speakingIndicators = new();

        foreach (var conn in conns)
        {
            var ob = Instantiate(instance.basePlayerIcon, instance.basePlayerIcon.transform.parent);
            ob.gameObject.SetActive(true);
            var username = conn.Value.Contains('\r') ? conn.Value.Split('\r')[0] : conn.Value;
            var specialindex = conn.Value.Contains('\r') ? int.Parse(conn.Value.Split('\r')[1]) : -1;
            ob.GetComponentInChildren<TMP_Text>().text = username;
            ob.sprite = specialindex==-1? instance.playerIcons[Extensions.GetDeterministicStringIndex(username, instance.playerIcons.Length)] : instance.specialPlayerIcons[specialindex];
            ob.transform.GetChild(1).GetComponent<Image>().sprite = readied.Contains(conn.Key) ? instance.ready : instance.notready;
            instance.speakingIndicators.Add(conn.Key, ob.transform.GetChild(2).gameObject);
        }
    }

    //lobby and menu
    [SerializeField] private CanvasGroup LobbyUI, GameUI, TransitionScreen, WinScreen;

    public static void ShowLobby()
    {
        instance.LobbyUI.alpha = 1;
        instance.LobbyUI.interactable = true;
        instance.LobbyUI.blocksRaycasts = true;

        instance.GameUI.alpha = 0;
        instance.GameUI.interactable = false;
        instance.GameUI.blocksRaycasts = false;

        instance.TransitionScreen.alpha = 0;
        instance.SetLobbyCustomisationSliders();

        instance.WinScreen.alpha = 0;
        instance.WinScreen.interactable = false;
        instance.WinScreen.blocksRaycasts = false;
    }

    public static void FadeToGame()
    {
        instance.TransitionScreen.alpha = 1;
        instance.TransitionScreen.DOFade(0, 2);

        instance.LobbyUI.alpha = 0; 
        instance.LobbyUI.interactable = false;
        instance.LobbyUI.blocksRaycasts = false;

        instance.GameUI.alpha = 1;
        instance.GameUI.interactable = true;
        instance.GameUI.blocksRaycasts = true;

        instance.WinScreen.alpha = 0;
        instance.WinScreen.interactable = false;
        instance.WinScreen.blocksRaycasts = false;
    }

    public static void ShowWinScreen()
    {
        instance.LobbyUI.alpha = 0;
        instance.LobbyUI.interactable = false;
        instance.LobbyUI.blocksRaycasts = false;
        instance.TransitionScreen.DOFade(1, 2).onComplete += () =>
        {
            instance.TransitionScreen.DOFade(0, 2);
            instance.GameUI.alpha = 0;
            instance.GameUI.interactable = false;
            instance.GameUI.blocksRaycasts = false;

            instance.WinScreen.alpha = 1;
            instance.WinScreen.interactable = true;
            instance.WinScreen.blocksRaycasts = true;
        };
    }

    public static void LobbyFromWinScreen()
    {
        instance.GameUI.alpha = 0;
        instance.GameUI.interactable = false;
        instance.GameUI.blocksRaycasts = false;
        instance.TransitionScreen.DOFade(1, 2).onComplete += () =>
        {
            instance.TransitionScreen.DOFade(0, 2);
            instance.LobbyUI.alpha = 1;
            instance.LobbyUI.interactable = true;
            instance.LobbyUI.blocksRaycasts = true;

            instance.WinScreen.alpha = 0;
            instance.WinScreen.interactable = false;
            instance.WinScreen.blocksRaycasts = false;
        };
    }

    public void ReadyupButton()
    {
        GameManager.ReadyUp();
    }

    public void BackToLobby()
    {
        GameManager.BackToLobby();
    }

    [SerializeField] TMP_Text ReadyUpTimerText;
    [SerializeField] TMP_Text ReadyUpButtonText;

    public static void SetReadyUpButtonText(string text)
    {
        instance.ReadyUpButtonText.text = text;
    }

    public static void SetReadyTimerText(string text)
    {
        instance.ReadyUpTimerText.text = text;
    }

    [SerializeField] private TMP_Text RegionTitleText;
    private float regiontitlecountdown = 0;
    private string regiontitle = "";
    private Coroutine currDisplayCoroutine;

    public static void ShowRegionTitle(string title, float countdown = 3)
    {
        instance.regiontitle = title;
        instance.regiontitlecountdown = countdown;
    }

    private IEnumerator DisplayRegionTitle()
    {
        RegionTitleText.text = "";
        for (int i = 0; i < regiontitle.Length; i++)
        {
            RegionTitleText.text += regiontitle[i];
            yield return new WaitForSeconds(0.075f);
        }
        yield return new WaitForSeconds(10);
        RegionTitleText.DOFade(0, 1);
    }

    [SerializeField] private Image HeadDamageIndicator, BodyDamageIndicator, FeetDamageIndicator;

    public static void ToggleDamageIndicator(bool value)
    {
        instance.HeadDamageIndicator.gameObject.SetActive(value);
        instance.BodyDamageIndicator.gameObject.SetActive(value);
        instance.FeetDamageIndicator.gameObject.SetActive(value);
    }

    public static void SetHeadDamage(float amount)
    {
        instance.HeadDamageIndicator.color = amount <= 0 ? Color.Lerp(Color.red, Color.black, Mathf.Abs(amount / 0.25f)) : Color.Lerp(Color.white, Color.red, 1 - amount);
    }
    public static void SetBodyDamage(float amount)
    {
        instance.BodyDamageIndicator.color = amount <= 0 ? Color.Lerp(Color.red, Color.black, Mathf.Abs(amount / 0.9f)) : Color.Lerp(Color.white, Color.red, 1 - amount);
    }

    public static void SetFeetDamage(float amount)
    {
        instance.FeetDamageIndicator.color = amount <= 0 ? Color.Lerp(Color.black, Color.red, amount+1) : Color.Lerp(Color.white, Color.red, 1 - amount);
    }

    [SerializeField] private Slider LobbyScarfR, LobbyScarfG, LobbyScarfB, LobbySkinTone;
    [SerializeField] private Image LobbyColIndicator, LobbySkinIndicator;
    [SerializeField] private List<Sprite> LobbySkinTones;

    private void SetLobbyCustomisationSliders()
    {
        LobbyScarfR.value = PlayerPrefs.GetFloat("SCARFCOL_R", 1);
        LobbyScarfG.value = PlayerPrefs.GetFloat("SCARFCOL_G", 0);
        LobbyScarfB.value = PlayerPrefs.GetFloat("SCARFCOL_B", 0);
        LobbySkinTone.value = PlayerPrefs.GetInt("SKINTEX", 0);
        LobbySkinIndicator.sprite = LobbySkinTones[PlayerPrefs.GetInt("SKINTEX", 0)];
        LobbyColIndicator.color = new Color(PlayerPrefs.GetFloat("SCARFCOL_R", 1), PlayerPrefs.GetFloat("SCARFCOL_G", 0), PlayerPrefs.GetFloat("SCARFCOL_B", 0));
    }

    public void SetScarfR(float value)
    {
        PlayerPrefs.SetFloat("SCARFCOL_R", value);
        LobbyColIndicator.color = new Color(PlayerPrefs.GetFloat("SCARFCOL_R", 1), PlayerPrefs.GetFloat("SCARFCOL_G", 0), PlayerPrefs.GetFloat("SCARFCOL_B", 0));
    }

    public void SetScarfG(float value)
    {
        PlayerPrefs.SetFloat("SCARFCOL_G", value);
        LobbyColIndicator.color = new Color(PlayerPrefs.GetFloat("SCARFCOL_R", 1), PlayerPrefs.GetFloat("SCARFCOL_G", 0), PlayerPrefs.GetFloat("SCARFCOL_B", 0));
    }

    public void SetScarfB(float value)
    {
        PlayerPrefs.SetFloat("SCARFCOL_B", value);
        LobbyColIndicator.color = new Color(PlayerPrefs.GetFloat("SCARFCOL_R", 1), PlayerPrefs.GetFloat("SCARFCOL_G", 0), PlayerPrefs.GetFloat("SCARFCOL_B", 0));
    }

    public void SetSkinTex(float value)
    {
        PlayerPrefs.SetInt("SKINTEX", (int)value);
        LobbySkinIndicator.sprite = LobbySkinTones[PlayerPrefs.GetInt("SKINTEX", 0)];
    }

    [SerializeField] private List<Button> GamemodeButtons, SaveslotButtons;
    [SerializeField] private Transform selectedGamemode, selectedSaveslot;
    [SerializeField] private TMP_Text WorldInfoText;
    [SerializeField] private TMP_InputField SeedInput;
    [SerializeField] private GameObject DeleteWorldButton;
    private GameModeEnum currMode;
    private int currSave;

    public void SelectGamemode(int gamemode)
    {
        GameManager.SetGamemode((GameModeEnum)gamemode);
    }

    public void SelectSaveslot(int slot)
    {
        GameManager.SetSave(slot);
    }

    public void SetSeed(string seed)
    {
        GameManager.SetSeed(seed);
    }

    public void DeleteSave()
    {
        GameManager.DeleteSave();
    }

    public static void SetSeedText(string text)
    {
        instance.SeedInput.text = text;
    }

    public static void SetWorldInfo(bool worldexists, string text)
    {
        instance.WorldInfoText.text = text;
        instance.WorldInfoText.gameObject.SetActive(true);
        instance.SeedInput.gameObject.SetActive(!worldexists);
        instance.DeleteWorldButton.SetActive(worldexists);
        instance.SeedInput.text = "";
    }

    public static void SetGamemodeIndicator(GameModeEnum mode)
    {
        instance.selectedGamemode.transform.position = instance.GamemodeButtons[(int)mode].transform.position;
        instance.currMode = mode;
    }

    public static void SetSaveIndicator(int save)
    {
        instance.selectedSaveslot.transform.position = instance.SaveslotButtons[save].transform.position;
        instance.currSave = save;
    }

    [SerializeField] private TMP_Text WinnerText, YouWinText;
    [SerializeField] private GameObject WinscreenHostButton;

    public static void SetWinText(string winners, string youwin)
    {
        instance.WinnerText.text = winners;
        instance.YouWinText.text = youwin;
    }

    public void GoToLobbyFromWin()
    {
        GameManager.BackToLobbyFromWin();
    }

    [SerializeField] private GameObject pauseMenuDefault, pauseMenuSettings;
    [SerializeField] private Slider inputvolumeSlider, outputvolumeSlider, sensSlider;
    [SerializeField] private TMP_Text inputvolumeText, outputvolumeText, sensText;
    [SerializeField] private Toggle reduceMotionSickness;
    [SerializeField] private TMP_Dropdown AudioInputs;

    public void ToPauseSettings()
    {
        pauseMenuDefault.SetActive(false);
        pauseMenuSettings.SetActive(true);
    }

    public void ToPauseDefault()
    {
        pauseMenuDefault.SetActive(true);
        pauseMenuSettings.SetActive(false);
    }

    private void ResetPauseSettings()
    {
        sensSlider.value = PlayerPrefs.GetFloat("SENS", 2);
        sensText.text = System.Math.Round(sensSlider.value, 2).ToString();
        inputvolumeSlider.value = PlayerPrefs.GetInt("INPUTVOLUME", 0);
        inputvolumeText.text = (100 - outputvolumeSlider.value).ToString();
        outputvolumeSlider.value = PlayerPrefs.GetInt("OUTPUTVOLUME", 0);
        outputvolumeText.text = (100 - outputvolumeSlider.value).ToString();
        reduceMotionSickness.isOn = PlayerPrefs.GetInt("HEADBOB", 0) == 1;
        UpdateInputDevices();
        AudioInputs.value = PlayerPrefs.GetInt("INPUTDEVICE", 0);
        pauseMenuDefault.SetActive(true);
        pauseMenuSettings.SetActive(false);
    }

    void UpdateInputDevices()
    {
        AudioInputs.ClearOptions();
        var devices = VivoxManager.GetInputDevices;
        var options = new List<string>();
        foreach (var device in devices)
        {
            options.Add(device.DeviceName);
        }
        AudioInputs.AddOptions(options);
    }

    public void SetMotionSickness(bool value)
    {
        PlayerPrefs.SetInt("HEADBOB", value ? 1 : 0);
    }

    public void SetInputDevice(int index)
    {
        VivoxManager.SetAudioInputDevice(VivoxManager.GetInputDevices[index]);
        PlayerPrefs.SetInt("INPUTDEVICE", index);
    }

    public void SetInputVolume(float value)
    {
        PlayerPrefs.SetInt("INPUTVOLUME", (int)value);
        VivoxManager.SetAudioInputVolume((int)value);
        inputvolumeText.text = (100 + value).ToString();
    }

    public void SetOutputVolume(float value)
    {
        PlayerPrefs.SetInt("OUTPUTVOLUME", (int)value);
        VivoxManager.SetAudioOutputVolume((int)value);
        outputvolumeText.text = (100 + value).ToString();
    }

    public void SetSens(float value)
    {
        PlayerPrefs.SetFloat("SENS", value);
        sensText.text = System.Math.Round(value, 2).ToString();
    }

    [SerializeField] private TMP_Text SpectatorTalkingText;

    public static void SetSpectatorTalkingText(string text)
    {
        instance.SpectatorTalkingText.text = text;
    }

    [SerializeField] private TMP_Text BottomscreenText;

    public static void SetBottomscreenText(string text)
    {
        instance.BottomscreenText.text = text;
    }

    [SerializeField] private CanvasGroup markedForDeathIcon;
    private float markedTargetTime;
    private Transform markedTarget;

    public static void SetMarkedForDeathIcon(ulong target)
    {
        var pos = Extensions.TransformToHUDSpace(Player.PLAYERBYID[target].transform.position);
        instance.markedTarget = Player.PLAYERBYID[target].transform;
        instance.markedForDeathIcon.transform.position = pos;
        instance.markedTargetTime = 5;
        instance.markedForDeathIcon.DOKill();
        instance.markedForDeathIcon.alpha = 0;
        instance.markedForDeathIcon.DOFade(1, 1f);
    }
}