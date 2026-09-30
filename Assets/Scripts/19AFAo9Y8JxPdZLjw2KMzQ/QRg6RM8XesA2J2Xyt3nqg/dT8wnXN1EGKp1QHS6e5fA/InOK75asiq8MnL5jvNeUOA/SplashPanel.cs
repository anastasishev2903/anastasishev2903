using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SplashPanel : MonoBehaviour
{
    public static SplashPanel Instance;
    private static bool IsSceneLoadSecondPass = false;
    public Slider AnimationSlider;
    public float FirstAnimationTime = 10.0f;
    public float DefaultAnimationTime = 0.4f;
    public float SecondPassSliderValue = 0.5f;
    private Sequence AnimSliderSequence;
    public GameObject Content;
    public GameObject Background;
    public GameObject Error;
    private void Awake()
    {
        Instance = this.gameObject.GetComponent<SplashPanel>();
    }

    private void Start()
    {
        if (SceneManager.GetActiveScene().buildIndex == SETTINGS.SCENES.SCENE_0 && !IsSceneLoadSecondPass)
        {
            this.AnimateInitial();
        }
        else
        {
            this.Animate();
        }
    }

    public void ResetAnimation()
    {
        this.AnimSliderSequence?.Kill();
        this.AnimationSlider.value = IsSceneLoadSecondPass ? this.SecondPassSliderValue : 0.05f;
    }

    public void Animate()
    {
        this.ResetAnimation();
        bool tmpIsSceneLoadSecondPass = IsSceneLoadSecondPass;
        this.AnimSliderSequence = DOTween.Sequence().Append(DOTween.To(() => this.AnimationSlider.value, x => this.AnimationSlider.value = x, tmpIsSceneLoadSecondPass ? 1f : this.SecondPassSliderValue, this.DefaultAnimationTime)).SetEase(Ease.Linear);
        IsSceneLoadSecondPass = !IsSceneLoadSecondPass;
    }

    public void PauseAnimation()
    {
        this.AnimSliderSequence?.Pause();
    }

    public void ResumeAnimation()
    {
        this.AnimSliderSequence?.Play();
    }

    public void AnimateForce()
    {
        {
#if B_LOGS
            {
                Debug.Log($"[Test] Animate Force");
            }
#endif
        }

        this.AnimSliderSequence?.Kill();
        if (AnimationSlider != null)
            this.AnimationSlider.value = 1f;
        IsSceneLoadSecondPass = false;
    }

    private void AnimateInitial()
    {
        this.AnimationSlider.value = 0.05f;
        IsSceneLoadSecondPass = !IsSceneLoadSecondPass;
        this.AnimSliderSequence = DOTween.Sequence().Append(DOTween.To(() => this.AnimationSlider.value, x => this.AnimationSlider.value = x, 1f, this.FirstAnimationTime)).SetEase(Ease.Linear).OnComplete(() =>
        {
            MainThreadUtil.Instance?.OnSplashInitialTimeout();
        });
    }
}