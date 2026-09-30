using UnityEngine;
using UnityEngine.UI;

public class SetPhysicsRunButton : MonoBehaviour
{
    public Button Button;
    public bool IsPhysicsRunOnClick;
    private void Awake()
    {
        if (this.Button == null)
            if (!this.TryGetComponent(out this.Button))
                this.Button = this.GetComponentInChildren<Button>();
    }

    private void Start()
    {
        this.Button.onClick.AddListener(() => BasicController.Instance.SetPhysicsRun(this.IsPhysicsRunOnClick));
    }
}