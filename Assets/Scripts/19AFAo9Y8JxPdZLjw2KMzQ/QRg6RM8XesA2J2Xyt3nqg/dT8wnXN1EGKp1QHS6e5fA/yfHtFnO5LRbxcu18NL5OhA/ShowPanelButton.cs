using UnityEngine;
using UnityEngine.UI;

public class ShowPanelButton : MonoBehaviour
{
    private Button Button;
    private int PanelToShowIndex;
    private bool IsShowLastPanel;
    private void Awake()
    {
        if (this.Button == null)
            if (!this.TryGetComponent(out this.Button))
                this.Button = this.GetComponentInChildren<Button>();
    }

    private void Start()
    {
        if (this.IsShowLastPanel)
            this.Button.onClick.AddListener(() => PanelController.Instance.ShowLastPanel());
        else
            this.Button.onClick.AddListener(() => PanelController.Instance.ShowPanel(this.PanelToShowIndex));
    }
}