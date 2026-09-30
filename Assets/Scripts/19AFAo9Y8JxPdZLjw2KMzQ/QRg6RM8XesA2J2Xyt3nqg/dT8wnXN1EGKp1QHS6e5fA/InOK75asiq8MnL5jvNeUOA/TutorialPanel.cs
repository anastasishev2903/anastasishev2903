using UnityEngine;
using UnityEngine.UI;

public class TutorialPanel : MonoBehaviour
{
    public bool IsTutorialEndPanel;
    public int NextTutorialPanelIndex;
    public int EndTutorialPanelIndex = 1;
    public Button NextTutorialButton;
    public Button TutorialEndButton;
    private void Start()
    {
        if (this.NextTutorialButton != null)
        {
            if (this.IsTutorialEndPanel)
            {
                this.NextTutorialButton.onClick.AddListener(() => PanelController.Instance.ShowPanel(this.EndTutorialPanelIndex));
                this.NextTutorialButton.onClick.AddListener(() => BasicController.Instance.SetTutorPassed());
            }
            else
            {
                this.NextTutorialButton.onClick.AddListener(() => PanelController.Instance.ShowPanel(this.NextTutorialPanelIndex));
            }
        }

        if (this.TutorialEndButton != null)
        {
            this.TutorialEndButton.onClick.AddListener(() => PanelController.Instance.ShowPanel(this.EndTutorialPanelIndex));
            this.TutorialEndButton.onClick.AddListener(() => BasicController.Instance.SetTutorPassed());
        }
    }
}