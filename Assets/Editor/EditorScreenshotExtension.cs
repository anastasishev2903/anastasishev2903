using System.IO;
using UnityEditor;
using UnityEngine;

public static class EditorScreenshotExtension
{
    [MenuItem("Screenshot/Take Screenshot %#z")]
    private static void Screenshot()
    {
        var timestamp = System.DateTime.Now;
        var stampString = string.Format("_{0}-{1:00}-{2:00}_{3:00}-{4:00}-{5:00}", timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, timestamp.Second);
        var currentResolution = Screen.width + "x" + Screen.height;
        var fileName = "Screenshot" + stampString + ".png";
        string projectName = Path.GetFileName(Path.GetDirectoryName(UnityEngine.Application.dataPath));

        var pathDirectoryToSave = Path.Combine(Application.streamingAssetsPath, projectName + "_SCREENS");
        pathDirectoryToSave = Path.Combine(pathDirectoryToSave, "WHITE");
        var pathFileToSave = Path.Combine(pathDirectoryToSave, fileName);

        if (!Directory.Exists(pathDirectoryToSave))
        {
            Directory.CreateDirectory(pathDirectoryToSave);
        }

        ScreenCapture.CaptureScreenshot(pathFileToSave);

        // Refresh the AssetsDatabase so the file actually appears in Unity
        AssetDatabase.Refresh();

        Debug.Log("New Screenshot taken");
    }
}