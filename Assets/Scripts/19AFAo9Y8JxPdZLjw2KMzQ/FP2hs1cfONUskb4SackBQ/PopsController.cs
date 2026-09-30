using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using static SETTINGS;

public class PopsController : MonoBehaviour
{
    public static PopsController Instance;
    public int CurrentPopIndex;
    public List<Pop> Pops;
    public GameObject BlurBackground;
    public List<GameObject> GameObjectsToHide;
    public float ScaleDuration = 0.4f;
    public List<int> LastPopIndexes = new();
    private void Awake()
    {
        Instance = this.gameObject.GetComponent<PopsController>();
    }

    private void Start()
    {
        this.BackgroundHidden();
        foreach (Pop pop in this.Pops)
            if (pop != null)
                pop.gameObject.SetActive(true);
    }

    public void HideAllPops()
    {
        this.LastPopIndexes.Clear();
        this.HideAllPopsWithoutFadeBack();
        foreach (GameObject item in this.GameObjectsToHide)
            if (item != null)
                item.SetActive(true);
        this.BackgroundHide();
    }

    private void HideAllPopsWithoutFadeBack(bool IsShowPop = false)
    {
        for (int i = 0; i < this.Pops.Count; ++i)
            if (this.Pops[i] != null && !(i == this.CurrentPopIndex && IsShowPop))
                this.Pops[i].Hide();
    }

    public void ShowPop(int index)
    {
        this.CurrentPopIndex = index;
        this.LastPopIndexes.Add(this.CurrentPopIndex);
        this.HideAllPopsWithoutFadeBack(true);
        this.BackgroundShow();
        this.Pops[index].Show();
        foreach (GameObject item in this.GameObjectsToHide)
            item.SetActive(false);
    }

    public void ShowLastPop()
    {
        this.LastPopIndexes.RemoveAll(x => x == this.CurrentPopIndex);
        if (this.LastPopIndexes.Count <= 0)
            this.HideAllPops();
        else
            this.ShowPop(this.LastPopIndexes.Last());
    }

    public Pop GetPop(int index)
    {
        return this.Pops[index];
    }

    private void BackgroundShow()
    {
        this.BlurBackground.gameObject.SetActive(true);
    }

    private void BackgroundHide()
    {
        this.Invoke(nameof(this.BackgroundHidden), this.ScaleDuration);
    }

    private void BackgroundHidden()
    {
        this.BlurBackground.gameObject.SetActive(false);
    }
}