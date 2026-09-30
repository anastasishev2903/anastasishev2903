using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class TMProAspectExtension : MonoBehaviour
{
    private float AspectOfOneChar = 0.6f;
    private float BaseAspectAdd;
    private float MinAspect = 1.5f;
    private float MaxAspect = 4;
    private List<string> CharsIgnore = new();
    private TMP_Text Text;
    private AspectRatioFitter AspectRatioFitter;
    private void Update()
    {
        int textLength = 1;
        if (this.CharsIgnore.Count > 0)
        {
            string tmpText = this.Text.text;
            foreach (string charIgnore in this.CharsIgnore)
                while (tmpText.Contains(charIgnore))
                    tmpText = tmpText.Replace(charIgnore, "");
            textLength = tmpText.Length;
        }
        else
        {
            textLength = this.Text.text.Length;
        }

        float newAspectRatio = Mathf.Clamp(this.BaseAspectAdd + this.AspectOfOneChar * textLength, this.MinAspect, this.MaxAspect);
        if (!Mathf.Approximately(this.AspectRatioFitter.aspectRatio, newAspectRatio))
            this.AspectRatioFitter.aspectRatio = newAspectRatio;
    }
}