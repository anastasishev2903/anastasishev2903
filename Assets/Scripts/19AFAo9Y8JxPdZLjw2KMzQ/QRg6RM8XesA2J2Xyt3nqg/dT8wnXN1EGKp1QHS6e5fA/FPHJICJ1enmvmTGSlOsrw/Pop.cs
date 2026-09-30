using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Pop : MonoBehaviour
{
    public GameObject Content;
    public TMP_Text ContentHeaderText;
    public TMP_Text ContentMainText;
    public TMP_Text ContentAdditionalText;
    public Image ContentImage;
    public float scaleDuration = 0.4f;
    public Ease ease = Ease.OutSine;
    public bool IsOnlyYScale;
    public bool IsScaledDownOnAwake = true;
    private bool IsActive => this.Content.transform.localScale.x > 0.5f && this.Content.transform.localScale.y > 0.5f;

    private void Awake()
    {
        this.Content.SetActive(true);
        if (this.IsScaledDownOnAwake)
            this.HideImmediately();
    }

    private void Start()
    {
    // Content.SetActive(false);
    }

    public void Show()
    {
        this.Content.SetActive(true);
        if ((DOTween.TweensByTarget(this.Content.transform)?.Count ?? 0) > 0)
            DOTween.Kill(this.Content.transform, true);
        if (this.IsOnlyYScale)
            this.Content.transform.DOScaleY(1f, this.scaleDuration).SetEase(this.ease).OnComplete(() =>
            {
            });
        else
            this.Content.transform.DOScale(1f, this.scaleDuration).SetEase(this.ease).OnComplete(() =>
            {
            });
    }

    public void Hide()
    {
        if (this.Content.gameObject.activeSelf)
        {
            DOTween.Kill(this.Content.transform, true);
            if (this.IsOnlyYScale)
                this.Content.transform.DOScaleY(0f, this.scaleDuration).SetEase(this.ease).OnComplete(() =>
                {
                    this.Content.SetActive(false);
                });
            else
                this.Content.transform.DOScale(0f, this.scaleDuration).SetEase(this.ease).OnComplete(() =>
                {
                    this.Content.SetActive(false);
                });
        }
    }

    private void HideImmediately()
    {
        DOTween.Kill(this.Content.transform, true);
        if (this.IsOnlyYScale)
            this.Content.transform.DOScaleY(0f, 0.01f);
        else
            this.Content.transform.DOScale(0f, 0.01f);
        this.Content.SetActive(false);
    }

    public static void HideAllPops()
    {
        PopsController.Instance.HideAllPops();
    }
}