using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static SETTINGS;

public class BasicController : MonoBehaviour
{
    public static BasicController Instance;
    public static bool IsAfterLevelComplete;
    public static bool IsAfterLevelFailed = false;
    [HideInInspector]
    public GameObject RootGameObject; // tag - "Root"
    public Canvas MainCanvas;
    public Transform Environment;
    public Transform EnvironmentWithTweensToToggle;
    public Button DeleteProgressDataButton;
    public Button ShowResetTutorialButton;
    [HideInInspector]
    public List<MoneyCount> MoneyCountContainers = new();
    public int GameSceneSettingsIndex => SceneManager.GetActiveScene().buildIndex;
    public bool IsGameRunning { get; private set; }
    private static GameScenesSettingSingletonsContainer GLOBAL_SETTINGS => GameScenesSettingSingletonsContainer.ALL_SCENES_SETTING_SINGLETONS[0];
    public static GameScenesSettingSingletonsContainer C_SCENE_SETTINGS => GameScenesSettingSingletonsContainer.ALL_SCENES_SETTING_SINGLETONS[Instance.GameSceneSettingsIndex];

    private void Awake()
    {
        Instance = this.gameObject.GetComponent<BasicController>();
        this.RootGameObject = GameObject.FindWithTag("Root");
        if (this.GameSceneSettingsIndex == SCENES.SCENE_0)
            this.SetPhysicsRun(true);
        else
            this.SetPhysicsRun(false);
        this.MoneyCountContainers = this.RootGameObject.GetComponentsInChildren<MoneyCount>(true).ToList();
    }

    private void Start()
    {
        if (this.GameSceneSettingsIndex != SCENES.SCENE_0)
            Screen.orientation = ScreenOrientation.Portrait;
        this.DeleteProgressDataButton?.onClick.AddListener(() =>
        {
            PlayerPrefs.DeleteAll();
            //AudioController.Instance.UpdateMusics();
            //AudioController.Instance.UpdateSfxes();
            Instance.LoadSceneByIndex(SCENES.SCENE_0);
        });
        this.ShowResetTutorialButton?.onClick.AddListener(() =>
        {
            C_SCENE_SETTINGS.IsTutorPassed = false;
            PopsController.Instance.HideAllPops();
            PanelController.Instance.ShowPanel(PANELS.TUTORIAL0);
        });
    }

    public void SetTutorPassed()
    {
        C_SCENE_SETTINGS.IsTutorPassed = true;
    }

    public void SetPhysicsRun(bool isRun)
    {
        this.IsGameRunning = isRun;
        this.SetFreezeAllRigidbodies(!this.IsGameRunning);
        Physics2D.simulationMode = this.IsGameRunning ? SimulationMode2D.FixedUpdate : SimulationMode2D.Script;
        if (this.EnvironmentWithTweensToToggle != null)
            this.ToggleTweenInChilds(this.EnvironmentWithTweensToToggle);
    }

    private void ToggleTweenInChilds(Transform parent)
    {
        Transform[] childs = parent.GetComponentsInChildren<Transform>();
        foreach (Transform item in childs)
            if (item != null && DOTween.IsTweening(item))
            {
                if (this.IsGameRunning)
                    DOTween.Play(item);
                else
                    DOTween.Pause(item);
            }
    }

    private void SetFreezeAllRigidbodies(bool isFreeze)
    {
        Rigidbody2D[] rbs = this.RootGameObject.GetComponentsInChildren<Rigidbody2D>(true);
        foreach (Rigidbody2D rb in rbs)
            if (isFreeze)
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            else
                rb.constraints = RigidbodyConstraints2D.None;
    }

    public void LoadCurrentScene()
    {
        this.LoadSceneByIndex(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadSceneByIndex(int sceneIndex)
    {
        //if (SceneManager.GetActiveScene().buildIndex == sceneIndex)
        //    AdsInitializer.Instance?.ShowAd();
        this.StartCoroutine(this.LoadSceneByIndexAsyncCoroutine(sceneIndex));
    }

    private static GameScenesSettingSingletonsContainer GAME_INDEX_SETTINGS(int index)
    {
        return GameScenesSettingSingletonsContainer.ALL_SCENES_SETTING_SINGLETONS[index];
    }

    private IEnumerator LoadSceneByNameAsync(string sceneName)
    {
        PanelController.Instance.ShowPanel(PANELS.SPLASH);
        //AudioController.Instance.SaveLastMusicTimes();
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        while (!asyncLoad.isDone)
            yield return null;
    }

    private IEnumerator LoadSceneByIndexAsyncCoroutine(int index)
    {
        PanelController.Instance.ShowPanel(PANELS.SPLASH);
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(index);
        while (!asyncLoad.isDone)
            yield return null;
    }

    private void LoadMenuOnWin()
    {
        IsAfterLevelComplete = true;
        Instance.LoadSceneByIndex(SCENES.SCENE_0);
    }

    public void UpdateMoneyCountContainers()
    {
        foreach (MoneyCount item in this.MoneyCountContainers)
            item.UpdateText();
    }

    private static void MakeGrid(List<RectTransform> grid, AspectRatioFitter aspectRatioFitter, float widthAspectRatio, int colsCount, int rowsCount)
    {
        aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        aspectRatioFitter.aspectRatio = widthAspectRatio;
        foreach (RectTransform item in grid)
        {
            int index = item.transform.GetSiblingIndex();
            item.anchorMin = new Vector3(Mathf.FloorToInt((float)index % colsCount) * (1f / colsCount), (rowsCount - (Mathf.FloorToInt((float)index / colsCount) % rowsCount + 1f)) * (1f / rowsCount));
            item.anchorMax = new Vector3(Mathf.FloorToInt((float)index % colsCount + 1f) * (1f / colsCount), (rowsCount - Mathf.FloorToInt((float)index / colsCount) % rowsCount) * (1f / rowsCount));
            item.offsetMin = Vector2.zero;
            item.offsetMax = Vector2.zero;
        }
    }

    private static void ExitGame()
    {
        Application.Quit();
    }
}