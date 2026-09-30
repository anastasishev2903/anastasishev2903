using UnityEngine;

/// <summary>
/// The single palette the whole delivery run is painted from: an almost black
/// graphite night, gold as the primary signal, and three louder voices for the
/// parcel colours. Alarm red is used for graphics and for large headings only -
/// the shared font outline is near black, so small red lettering would sit right
/// on the contrast floor (rule C.14).
/// </summary>
public static class CourierPalette
{
    /// <summary>#171A22 - the graphite everything else sits on.</summary>
    public static readonly Color Base = new Color(0.09019608f, 0.10196079f, 0.13333334f, 1f);
    /// <summary>#0E1017 - the deepest layer: shades, bar tracks, the splash wash.</summary>
    public static readonly Color Deep = new Color(0.05490196f, 0.0627451f, 0.09019608f, 1f);
    /// <summary>#202634 - every card, chip and plate body.</summary>
    public static readonly Color Surface = new Color(0.1254902f, 0.14901961f, 0.20392157f, 1f);
    /// <summary>#2B3345 - a surface that is selected or switched on.</summary>
    public static readonly Color SurfaceLit = new Color(0.16862746f, 0.2f, 0.27058825f, 1f);
    /// <summary>#F5C442 - the primary accent: PLAY, frames, the route clock.</summary>
    public static readonly Color Gold = new Color(0.9607843f, 0.76862746f, 0.25882354f, 1f);
    /// <summary>#E94B3D - hazards, spent feathers, the stopped run. Rarely a letter.</summary>
    public static readonly Color Alarm = new Color(0.9137255f, 0.29411766f, 0.23921569f, 1f);
    /// <summary>#37B8CC - the second voice: the live lane, the head of the order.</summary>
    public static readonly Color Cyan = new Color(0.21568628f, 0.72156864f, 0.8f, 1f);
    /// <summary>#7AC84F - delivered, intact, cleared.</summary>
    public static readonly Color Lime = new Color(0.47843137f, 0.78431374f, 0.30980393f, 1f);
    /// <summary>#FFF1D6 - the default text colour, far lighter than the outline.</summary>
    public static readonly Color Cream = new Color(1f, 0.94509804f, 0.8392157f, 1f);
    /// <summary>The same cream at two thirds, for captions under a value.</summary>
    public static readonly Color CreamDim = new Color(1f, 0.94509804f, 0.8392157f, 0.66f);
    /// <summary>#101219 - the font material outline, and the contour of every plate.</summary>
    public static readonly Color Ink = new Color(0.0627451f, 0.07058824f, 0.09803922f, 1f);
    /// <summary>The four parcel colours, indexed by parcel id: gold, red, cyan, lime.</summary>
    public static readonly Color[] Parcels =
    {
        Gold,
        Alarm,
        Cyan,
        Lime
    };
    public static Color Fade(Color source, float alpha)
    {
        return new Color(source.r, source.g, source.b, alpha);
    }

    /// <summary>The tint for a parcel id, clamped so a short array never throws.</summary>
    public static Color Parcel(int kind)
    {
        return Parcels[Mathf.Clamp(kind, 0, Parcels.Length - 1)];
    }
}