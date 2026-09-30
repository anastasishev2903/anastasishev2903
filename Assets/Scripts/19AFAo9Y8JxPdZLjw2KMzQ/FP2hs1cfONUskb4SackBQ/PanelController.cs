using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static SETTINGS;

public class PanelController : MonoBehaviour
{
    public static PanelController Instance;
    public List<Panel> Panels;
    public int CurrentPanelIndex;
    [HideInInspector]
    public List<int> LastPanelIndexes = new()
    {
        1
    };
    public float ScaleDuration = 0.4f;
    public bool IsShowSplashOnStart = true;
    public float StaticBlurMaterialInitialValue;
    private void Awake()
    {
        Instance = this.gameObject.GetComponent<PanelController>();
    }

    private void Start()
    {
        this.RunSplashAnim();
    }

    private void RunSplashAnim()
    {
        this.ShowImmediately(PANELS.SPLASH);
        if (BasicController.Instance.GameSceneSettingsIndex == SCENES.SCENE_0)
        {
        }
        else
        {
            this.Invoke(nameof(this.SwitchSplash), SplashPanel.Instance.DefaultAnimationTime);
        }
    }

    private void SwitchSplash()
    {
        if (SetUpAwakeNoDestroyController.Instance.IsTutorialEnabled && !BasicController.C_SCENE_SETTINGS.IsTutorPassed)
            this.ShowPanel(PANELS.TUTORIAL0);
        else
            this.ShowPanel(PANELS.DEFAULT);
    }

    private void HideAllPanelsExceptNext(int nextPanelIndex)
    {
        this.LastPanelIndexes.Add(nextPanelIndex);
        this.CurrentPanelIndex = nextPanelIndex;
        for (int i = 0; i < this.Panels.Count; i++)
            if (i != nextPanelIndex && this.Panels[i] != null)
                this.Panels[i].Hide();
    }

    private void HideAllPanelsExceptNextWithSaveLast(int nextPanelIndex)
    {
        this.LastPanelIndexes.Add(nextPanelIndex);
        this.CurrentPanelIndex = nextPanelIndex;
        for (int i = 0; i < this.Panels.Count; i++)
            if (i != nextPanelIndex && this.Panels[i] != null)
                this.Panels[i].Hide();
    }

    public void ShowPanel(int index)
    {
        this.HideAllPanelsExceptNextWithSaveLast(index);
        this.BeforeShowAdditionalHandler(index);
        this.CurrentPanelIndex = index;
        this.Panels[index].Show();
    }

    private void ShowImmediately(int index)
    {
        this.HideAllPanelsExceptNextWithSaveLast(index);
        this.BeforeShowAdditionalHandler(index);
        this.CurrentPanelIndex = index;
        this.Panels[index].ShowImmediately();
    }

    public void ShowLastPanel()
    {
        this.LastPanelIndexes.RemoveAll(x => x == this.CurrentPanelIndex);
        int lastPanelIndex = this.LastPanelIndexes.Last();
        this.BeforeShowAdditionalHandler(lastPanelIndex);
        this.HideAllPanelsExceptNext(lastPanelIndex);
        this.CurrentPanelIndex = lastPanelIndex;
        this.Panels[lastPanelIndex].Show();
    }

    private Panel GetPanel(int index)
    {
        return this.Panels[index];
    }

    private void BeforeShowAdditionalHandler(int index)
    {
        if (index == PANELS.SPLASH)
            SplashPanel.Instance.ResetAnimation();
        if (BasicController.Instance.GameSceneSettingsIndex == SCENES.SCENE_0)
        {
        }
    }

    public void AfterShowAdditionalHandler(int index)
    {
        if (index == PANELS.SPLASH && BasicController.Instance.GameSceneSettingsIndex != SCENES.SCENE_0)
            SplashPanel.Instance.Animate();
        if (BasicController.Instance.GameSceneSettingsIndex != SCENES.SCENE_0)
        {
            if (index == PANELS.SPLASH || index == PANELS.TUTORIAL0)
                BasicController.Instance.SetPhysicsRun(false);
            else if (index == PANELS.DEFAULT)
                BasicController.Instance.SetPhysicsRun(true);
        }
    }
}