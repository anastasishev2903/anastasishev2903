using System.Collections.Generic;
using TMPro;
using UnityEngine;

// TmpContrastGuard.cs — staged into every Unity app by approve-pipeline-unity.sh
// (stage 5c3, rule C.14 in CLAUDE-unity.md). Do not edit the copy inside a project;
// edit scripts/lib/unity/TmpContrastGuard.cs.
//
// WHY: every TMP label gets an outline (C.10), and by default that outline is dark.
// A dark face colour on a dark outline merges into a smudge — the label is not
// readable on any backing (ANDROID-3627: PLAY drawn Deep #12151E on the #12151E
// outline read as a black blob). enforce-text-contrast.sh fixes colours SERIALISED
// in scenes/prefabs, but labels built at runtime from C# (UiKit.Cta, VaultUi.Caption,
// label.color = Palette.X ...) never reach a scene file, so that pass cannot see them.
//
// WHAT: after any TMP text is regenerated, compare its face colour with the outline
// colour of the material it actually renders with. Below WCAG 4.5:1 the face is
// blended toward white (dark outline) or black (light outline) until it reaches 7:1.
// Hue is kept; alpha is kept. A label whose outline was deliberately switched to a
// light colour (TextReadability-style per-label material) is measured against THAT
// outline, so intentionally dark text on a light rim is left alone. Labels without
// an outline are left alone too.
public sealed class TmpContrastGuard : MonoBehaviour
{
    private const float MinRatio = 4.5f;
    private const float TargetRatio = 7f;
    private const float MinOutlineWidth = 0.01f;
    private static TmpContrastGuard s_instance;
    private readonly HashSet<TMP_Text> _pending = new HashSet<TMP_Text>();
    private readonly List<TMP_Text> _batch = new List<TMP_Text>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (s_instance != null)
            return;
        GameObject host = new GameObject("TmpContrastGuard");
        host.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(host);
        s_instance = host.AddComponent<TmpContrastGuard>();
    }

    // A lambda held in a field, never the bare method group: Plana renames the method
    // declaration but not a method-group reference (verify-unity-buttons.sh, CS0103).
    // The field keeps Add and Remove on the same delegate instance.
    private System.Action<Object> _onTextChanged;
    private void OnEnable()
    {
        if (this._onTextChanged == null)
            this._onTextChanged = obj => this.OnTextChanged(obj);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(this._onTextChanged);
    }

    private void OnDisable()
    {
        if (this._onTextChanged != null)
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(this._onTextChanged);
    }

    // The event fires from inside the canvas rebuild. Changing the colour right there
    // would re-dirty the graphic mid-rebuild, which Unity rejects — so queue it and
    // apply in LateUpdate, which runs before the next frame's rebuild.
    private void OnTextChanged(Object obj)
    {
        TMP_Text label = obj as TMP_Text;
        if (label != null)
            this._pending.Add(label);
    }

    private void LateUpdate()
    {
        if (this._pending.Count == 0)
            return;
        this._batch.Clear();
        this._batch.AddRange(this._pending);
        this._pending.Clear();
        for (int i = 0; i < this._batch.Count; i++)
            Fix(this._batch[i]);
    }

    private static void Fix(TMP_Text label)
    {
        if (label == null || !label.isActiveAndEnabled)
            return;
        Material material = label.fontSharedMaterial;
        if (material == null || !material.HasProperty(ShaderUtilities.ID_OutlineColor) || !material.HasProperty(ShaderUtilities.ID_OutlineWidth))
            return;
        if (material.GetFloat(ShaderUtilities.ID_OutlineWidth) < MinOutlineWidth)
            return;
        Color face = label.color;
        if (face.a <= 0f)
            return;
        Color rim = material.GetColor(ShaderUtilities.ID_OutlineColor);
        if (Ratio(face, rim) >= MinRatio)
            return;
        Color toward = Luminance(rim) < 0.5f ? Color.white : Color.black;
        Color fixedFace;
        if (Ratio(toward, rim) < TargetRatio)
        {
            fixedFace = toward;
        }
        else
        {
            // Smallest blend that reaches the target: contrast grows monotonically
            // with t, so a short bisection keeps as much of the hue as possible.
            float lo = 0f;
            float hi = 1f;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Ratio(Color.Lerp(face, toward, mid), rim) >= TargetRatio)
                    hi = mid;
                else
                    lo = mid;
            }

            fixedFace = Color.Lerp(face, toward, hi);
        }

        fixedFace.a = face.a;
        label.color = fixedFace;
    }

    // WCAG relative luminance of an sRGB colour, and the contrast ratio of two.
    private static float Luminance(Color c)
    {
        return 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
    }

    private static float Linear(float v)
    {
        v = Mathf.Clamp01(v);
        return v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
    }

    private static float Ratio(Color a, Color b)
    {
        float la = Luminance(a);
        float lb = Luminance(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }
}