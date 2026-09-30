using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Panel : MonoBehaviour
{
    public GameObject OuterBackground;
    public GameObject Content;
    public float ScaleDuration = 0.4f;
    public Ease Ease = Ease.OutSine;
    public TMP_Text HeaderText;
    public TMP_Text MainText;
    public bool IsScaledDownOnAwake = true;
    private bool IsActive => this.Content.transform.localScale.x > 0.5f && this.Content.transform.localScale.y > 0.5f;

    private void Awake()
    {
        this.Content.SetActive(true);
        if (this.OuterBackground != null)
            this.OuterBackground.gameObject.SetActive(true);
        if (this.IsScaledDownOnAwake)
            this.HideImmediately();
    }

    public void Show()
    {
        this.BackgroundShow();
        if (this.Content != null)
        {
            DOTween.Kill(this.Content.transform, true);
            this.Content.SetActive(true);
            this.Content.transform.DOScale(1f, this.ScaleDuration).SetEase(this.Ease).OnComplete(() =>
            {
                PanelController.Instance.AfterShowAdditionalHandler(PanelController.Instance.CurrentPanelIndex);
            });
        }
    }

    public void Hide()
    {
        this.BackgroundHide();
        DOTween.Kill(this.Content.transform, true);
        this.Content.transform.DOScale(0f, this.ScaleDuration).SetEase(this.Ease).OnComplete(() =>
        {
            this.Content.SetActive(false);
        });
    }

    private void BackgroundShow()
    {
        if (this.OuterBackground != null)
        {
            Image outerBackgroundImage = this.OuterBackground.GetComponent<Image>();
            DOTween.Kill(outerBackgroundImage, true);
            outerBackgroundImage.DOFade(1f, this.ScaleDuration / 2f);
        }
    }

    private void BackgroundShowImmediately()
    {
        if (this.OuterBackground != null)
        {
            Image outerBackgroundImage = this.OuterBackground.GetComponent<Image>();
            DOTween.Kill(outerBackgroundImage, true);
            outerBackgroundImage.DOFade(1f, 0f);
        }
    }

    private void BackgroundHide()
    {
        if (this.OuterBackground != null)
        {
            Image outerBackgroundImage = this.OuterBackground.GetComponent<Image>();
            DOTween.Kill(outerBackgroundImage, true);
            outerBackgroundImage.DOFade(0f, this.ScaleDuration);
        }
    }

    public void ShowImmediately()
    {
        this.BackgroundShowImmediately();
        this.Content.SetActive(true);
        DOTween.Kill(this.Content.transform, true);
        this.Content.transform.localScale = Vector3.one;
        PanelController.Instance.AfterShowAdditionalHandler(PanelController.Instance.CurrentPanelIndex);
    }

    private void HideImmediately()
    {
        if (this.OuterBackground != null)
        {
            Image outerBackgroundImage = this.OuterBackground.GetComponent<Image>();
            DOTween.Kill(outerBackgroundImage, true);
            outerBackgroundImage.DOFade(0f, 0.01f);
        }

        DOTween.Kill(this.Content.transform, true);
        this.Content.transform.DOScale(0f, 0.01f);
    }
}