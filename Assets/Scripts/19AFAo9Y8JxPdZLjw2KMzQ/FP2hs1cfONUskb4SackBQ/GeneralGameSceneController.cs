using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GeneralGameSceneController : MonoBehaviour
{
    private static GeneralGameSceneController Instance;
    public List<TMP_Text> TimerText = new();
    public List<TMP_Text> ScoreText = new();
    public List<TMP_Text> SubtitleText = new();
    public List<TMP_Text> LevelNumberText = new();
    public List<Button> HomeButtons = new();
    public List<Button> PauseButtons = new();
    public int CustomTimeInitial = 30;
    public int CustomTargetScore = 10;
    [HideInInspector]
    public bool IsGameEnd;
    [HideInInspector]
    public int TimeLeft;
    [HideInInspector]
    public int ScoreCurrent;
    [HideInInspector]
    public int CurrentGameIndex;
    private int TimeInitial => this.CustomTimeInitial + BasicController.C_SCENE_SETTINGS.CurrentLevelIndex * 10;
    private int ScoreTarget => this.CustomTargetScore + BasicController.C_SCENE_SETTINGS.CurrentLevelIndex * 10;
    private int CurrentReward => this.ScoreCurrent;

    private void Awake()
    {
        Instance = this.gameObject.GetComponent<GeneralGameSceneController>();
    }

    private void Start()
    {
        this.IsGameEnd = false;
        this.TimeLeft = this.TimeInitial;
        this.CurrentGameIndex = BasicController.Instance.GameSceneSettingsIndex;
        foreach (Button homeButton in this.HomeButtons)
            homeButton.onClick.AddListener(() =>
            {
                this.GoToMenu();
            });
        foreach (Button pauseButton in this.PauseButtons)
            pauseButton.onClick.AddListener(() =>
            {
                BasicController.Instance.SetPhysicsRun(false);
                PopsController.Instance.ShowPop(SETTINGS.POPS.PAUSE);
            });
        this.SetScoreText();
        this.LevelNumberText.ForEach(x => x.text = $"LVL {BasicController.C_SCENE_SETTINGS.CurrentLevelIndex + 1}");
        if (SetUpAwakeNoDestroyController.Instance.IsTimerEnabled)
        {
            this.SetTimerText();
            this.StartCoroutine(this.TimerCoroutine());
        }
    }

    private IEnumerator TimerCoroutine()
    {
        this.SetTimerText();
        while (!this.IsGameEnd && this.TimeLeft > 0 && BasicController.Instance.GameSceneSettingsIndex == this.CurrentGameIndex)
        {
            yield return new WaitForSeconds(1f);
            if (BasicController.Instance.IsGameRunning)
            {
                if (this.IsGameEnd)
                    break;
                this.TimeLeft--;
                this.SetTimerText();
            }
        }

        if (!this.IsGameEnd)
            this.GameLose();
    }

    private void SetTimerText()
    {
        this.TimerText.ForEach(x => x.text = TimeSpan.FromSeconds(this.TimeLeft).ToString(@"mm\:ss"));
    }

    private void SetScoreText()
    {
        if (SetUpAwakeNoDestroyController.Instance.IsCheckScoreEnabled)
            this.ScoreText.ForEach(x => x.text = $"{this.ScoreCurrent}/{this.ScoreTarget}");
        else
            this.ScoreText.ForEach(x => x.text = $"{this.ScoreCurrent}");
    }

    public void AddScore(int scoreToAdd)
    {
        if (!this.IsGameEnd)
        {
            this.ScoreCurrent += scoreToAdd;
            this.SetScoreText();
            this.CheckScore();
        }
    }

    private void CheckScore()
    {
        if (this.ScoreCurrent > BasicController.C_SCENE_SETTINGS.BestScore)
            BasicController.C_SCENE_SETTINGS.BestScore = this.ScoreCurrent;
        if (SetUpAwakeNoDestroyController.Instance.IsCheckScoreEnabled)
            if (this.ScoreCurrent >= this.ScoreTarget)
                this.GameWin();
    }

    public void GoToMenu()
    {
        BasicController.Instance.SetPhysicsRun(true);
        BasicController.Instance.LoadSceneByIndex(SETTINGS.SCENES.SCENE_0);
    }

    private void GameEnd()
    {
        this.IsGameEnd = true;
        BasicController.IsAfterLevelComplete = true;
    }

    public void GameLose()
    {
        if (SetUpAwakeNoDestroyController.Instance.IsOnlyWinGameEndEnabled)
            this.GameWin();
        if (!this.IsGameEnd)
        {
            this.GameEnd();
            BasicController.IsAfterLevelComplete = false;
            BasicController.IsAfterLevelFailed = true;
            Pop pop = PopsController.Instance.GetPop(SETTINGS.POPS.LOSE).GetComponent<Pop>();
            if (SetUpAwakeNoDestroyController.Instance.IsCheckScoreEnabled)
                pop.ContentMainText.text = $"{this.ScoreCurrent}/{this.ScoreTarget}";
            else
                pop.ContentMainText.text = $"{this.ScoreCurrent}";
            pop.ContentAdditionalText.text = $"{0}";
            SETTINGS.PlayerSYSTEM.Coins += 0;
            PopsController.Instance.ShowPop(SETTINGS.POPS.LOSE);
        }
    }

    public void GameWin()
    {
        if (!this.IsGameEnd)
        {
            this.GameEnd();
            BasicController.IsAfterLevelComplete = true;
            BasicController.IsAfterLevelFailed = false;
            Pop pop = PopsController.Instance.GetPop(SETTINGS.POPS.WIN).GetComponent<Pop>();
            if (SetUpAwakeNoDestroyController.Instance.IsCheckScoreEnabled)
                pop.ContentMainText.text = $"{this.ScoreCurrent}/{this.ScoreTarget}";
            else
                pop.ContentMainText.text = $"{this.ScoreCurrent}";
            if (SetUpAwakeNoDestroyController.Instance.IsBestScoreEnabled)
            {
                if (this.ScoreCurrent > SETTINGS.PlayerSYSTEM.Coins)
                    SETTINGS.PlayerSYSTEM.Coins = this.ScoreCurrent;
                pop.ContentAdditionalText.text = $"{SETTINGS.PlayerSYSTEM.Coins}";
            }
            else
            {
                pop.ContentAdditionalText.text = $"{this.CurrentReward}";
                SETTINGS.PlayerSYSTEM.Coins += this.CurrentReward;
            }

            if (SetUpAwakeNoDestroyController.Instance.IsLevelIncrementOnWin)
                ++BasicController.C_SCENE_SETTINGS.CurrentLevelIndex;
            PopsController.Instance.ShowPop(SETTINGS.POPS.WIN);
        }
    }

    private void GameEndExternally()
    {
        if (this.ScoreCurrent >= this.ScoreTarget)
            this.GameWin();
        else
            this.GameLose();
    }
}