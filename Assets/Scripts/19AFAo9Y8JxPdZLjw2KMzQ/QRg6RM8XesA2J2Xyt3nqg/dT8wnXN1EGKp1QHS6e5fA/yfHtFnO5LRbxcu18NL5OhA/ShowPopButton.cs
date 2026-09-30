using UnityEngine;
using UnityEngine.UI;

public class ShowPopButton : MonoBehaviour
{
    public Button Button;
    public int PopToShowIndex;
    public bool IsHideAllPops;
    public bool IsShowLastPop;
    private void Awake()
    {
        if (this.Button == null)
            if (!this.TryGetComponent(out this.Button))
                this.Button = this.GetComponentInChildren<Button>();
    }

    private void Start()
    {
        if (this.IsShowLastPop)
            this.Button.onClick.AddListener(() =>
            {
                PopsController.Instance.ShowLastPop();
            });
        else if (this.IsHideAllPops)
            this.Button.onClick.AddListener(() => PopsController.Instance.HideAllPops());
        else
            this.Button.onClick.AddListener(() => PopsController.Instance.ShowPop(this.PopToShowIndex));
    }
}