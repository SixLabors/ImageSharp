// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the filter selection and coefficients decoded for one AV1 loop-restoration unit.
/// </summary>
internal struct Av1LoopRestorationUnit
{
    /// <summary>
    /// The three transmitted symmetric vertical Wiener coefficients.
    /// </summary>
    public InlineArray3<int> WienerVertical;

    /// <summary>
    /// The three transmitted symmetric horizontal Wiener coefficients.
    /// </summary>
    public InlineArray3<int> WienerHorizontal;

    /// <summary>
    /// The two self-guided projection coefficients.
    /// </summary>
    public InlineArray2<int> SgrProjectionCoefficients;

    /// <summary>
    /// Gets or sets the restoration filter selected for the unit.
    /// </summary>
    public Av1RestorationFilterType FilterType { get; set; }

    /// <summary>
    /// Gets or sets the self-guided filter parameter-set index.
    /// </summary>
    public int SgrParameterSet { get; set; }
}
