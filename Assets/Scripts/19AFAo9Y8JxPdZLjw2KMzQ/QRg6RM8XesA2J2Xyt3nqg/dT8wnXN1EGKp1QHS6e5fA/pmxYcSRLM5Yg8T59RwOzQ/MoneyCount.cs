using TMPro;
using UnityEngine;
using static SETTINGS;

public class MoneyCount : MonoBehaviour
{
    public TMP_Text MoneyCountText;
    private void Start()
    {
        if (this.MoneyCountText == null)
        {
            TMP_Text getTxt;
            if (this.gameObject.TryGetComponent(out getTxt))
                this.MoneyCountText = getTxt;
        }

        this.UpdateText();
    }

    public void UpdateText()
    {
        this.MoneyCountText.text = PlayerSYSTEM.Coins.ToString();
    }
}