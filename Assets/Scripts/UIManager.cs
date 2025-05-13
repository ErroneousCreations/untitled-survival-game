using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using Unity.Netcode;

public class UIManager : MonoBehaviour
{
    private static UIManager instance;

    [SerializeField] private GameObject pauseMenu;

    private void Awake()
    {
        instance = this;

        instance.SetLobbyCustomisationSliders();
    }

    public static void TogglePauseMenu(bool open)
    {
        instance.pauseMenu.SetActive(open);
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && Player.LocalPlayer && Player.LocalPlayer.ph.isAlive.Value)
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
    public static void UpdatePlayerList(Dictionary<ulong, string> conns, List<ulong> readied)
    {
        foreach (Transform child in instance.basePlayerIcon.transform.parent)
        {
            if (child.gameObject.activeSelf) { Destroy(child.gameObject); }
        }

        foreach (var conn in conns)
        {
            var ob = Instantiate(instance.basePlayerIcon, instance.basePlayerIcon.transform.parent);
            ob.gameObject.SetActive(true);
            var username = conn.Value.Contains('\r') ? conn.Value.Split('\r')[0] : conn.Value;
            var specialindex = conn.Value.Contains('\r') ? int.Parse(conn.Value.Split('\r')[1]) : -1;
            ob.GetComponentInChildren<TMP_Text>().text = username;
            ob.sprite = specialindex==-1? instance.playerIcons[Extensions.GetDeterministicStringIndex(username, instance.playerIcons.Length)] : instance.specialPlayerIcons[specialindex];
            ob.transform.GetChild(1).GetComponent<Image>().sprite = readied.Contains(conn.Key) ? instance.ready : instance.notready;
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
        instance.selectedGamemode.transform.parent = instance.GamemodeButtons[(int)mode].transform;
        instance.selectedGamemode.transform.localPosition = Vector3.zero;
    }

    public static void SetSaveIndicator(int save)
    {
        instance.selectedSaveslot.transform.parent = instance.SaveslotButtons[save].transform;
        instance.selectedGamemode.transform.localPosition = Vector3.zero;
    }

    [SerializeField] private TMP_Text WinnerText, YouWinText;

    public static void SetWinText(string winners, string youwin)
    {
        instance.WinnerText.text = winners;
        instance.YouWinText.text = youwin;
    }

    public void GoToLobbyFromWin()
    {
        GameManager.BackToLobbyFromWin();
    }
}