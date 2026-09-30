using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TMProImageSyncer : MonoBehaviour
{
    private TMP_Text Text;
    private Image Image;
    private void Update()
    {
        this.SyncTextDueToImage();
    }

    private void SyncTextDueToImage()
    {
        if (this.Image.canvasRenderer.GetColor() != this.Text.canvasRenderer.GetColor())
            this.Text.canvasRenderer.SetColor(this.Image.canvasRenderer.GetColor());
    }
}