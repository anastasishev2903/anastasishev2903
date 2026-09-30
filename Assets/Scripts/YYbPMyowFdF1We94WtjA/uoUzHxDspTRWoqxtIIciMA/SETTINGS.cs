using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public static class SETTINGS
{
    public static class SCENES
    {
        public static readonly int SCENE_0 = 0;
        public static readonly int SCENE_1 = 1;
    }

    public static class PANELS
    {
        public static readonly int SPLASH = 0;
        public static readonly int DEFAULT = 1;
        public static readonly int EMPTY = 2;
        public static readonly int TUTORIAL0 = 13;
        public static readonly int TUTORIAL1 = 14;
        public static readonly int TUTORIAL2 = 15;
        public static readonly int TUTORIAL3 = 16;
        public static readonly int TUTORIAL4 = 17;
        public static readonly int TUTORIAL5 = 18;
        public static readonly int TUTORIAL6 = 19;
    }

    public static class POPS
    {
        public static readonly int PAUSE = 6;
        public static readonly int WIN = 7;
        public static readonly int LOSE = 8;
    }

    public static class PlayerSYSTEM
    {
        public static int Coins
        {
            get
            {
                if (!PlayerPrefs.HasKey("Coins"))
                    PlayerPrefs.SetInt("Coins", 0);
                return PlayerPrefs.GetInt("Coins");
            }

            set
            {
                PlayerPrefs.SetInt("Coins", value);
                BasicController.Instance.UpdateMoneyCountContainers();
            }
        }
    }

    public class GameScenesSettingSingletonsContainer
    {
        private static readonly GameScenesSettingSingletonsContainer Instance = new();
        public static readonly GameScenesSettingSingletonsContainer[] ALL_SCENES_SETTING_SINGLETONS =
        {
            Instance,
            Instance,
            Instance,
        };
        private int SCENE_INDEX => 0;
        private int LEVELS_COUNT => 10;
        private string NAME => "Menu";
        private string LEVEL_SCENE_STRING_FORMAT => "LEVEL{0}";

        private int CurrentGlobalChapterIndex
        {
            get
            {
                if (!PlayerPrefs.HasKey("CurrentGlobalChapterIndex"))
                    PlayerPrefs.SetInt("CurrentGlobalChapterIndex", 0);
                return PlayerPrefs.GetInt("CurrentGlobalChapterIndex");
            }

            set => PlayerPrefs.SetInt("CurrentGlobalChapterIndex", value);
        }

        public int CurrentLevelIndex
        {
            get
            {
                if (!PlayerPrefs.HasKey($"{this.NAME}CurrentLevelIndex"))
                    PlayerPrefs.SetInt($"{this.NAME}CurrentLevelIndex", 0);
                return PlayerPrefs.GetInt($"{this.NAME}CurrentLevelIndex");
            }

            set => PlayerPrefs.SetInt($"{this.NAME}CurrentLevelIndex", value);
        }

        public int BestScore
        {
            get
            {
                if (!PlayerPrefs.HasKey($"{this.NAME}BestScore"))
                    this.BestScore = 0;
                return PlayerPrefs.GetInt($"{this.NAME}BestScore");
            }

            set => PlayerPrefs.SetInt($"{this.NAME}BestScore", value);
        }

        public bool IsTutorPassed
        {
            get
            {
                if (!PlayerPrefs.HasKey($"{this.NAME}IsGameTutorPassed"))
                    PlayerPrefs.SetInt($"{this.NAME}IsGameTutorPassed", Convert.ToInt32(false));
                return PlayerPrefs.GetInt($"{this.NAME}IsGameTutorPassed") == 1;
            }

            set => PlayerPrefs.SetInt($"{this.NAME}IsGameTutorPassed", Convert.ToInt32(value));
        }
    }
}