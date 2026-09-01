// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the filter selection and coefficients decoded for one AV1 loop-restoration unit.
/// </summary>
internal struct Av1LoopRestorationUnit
{
    /// <summary>
    /// The three transmitted symmetric vertical Wiener coefficients.
    /// </summary>
    public WienerCoefficientBuffer WienerVertical;

    /// <summary>
    /// The three transmitted symmetric horizontal Wiener coefficients.
    /// </summary>
    public WienerCoefficientBuffer WienerHorizontal;

    /// <summary>
    /// The two self-guided projection coefficients.
    /// </summary>
    public SgrProjectionCoefficientBuffer SgrProjectionCoefficients;

    /// <summary>
    /// Gets or sets the restoration filter selected for the unit.
    /// </summary>
    public Av1RestorationFilterType FilterType { get; set; }

    /// <summary>
    /// Gets or sets the self-guided filter parameter-set index.
    /// </summary>
    public int SgrParameterSet { get; set; }

    /// <summary>
    /// Stores the transmitted coefficients inline with the restoration unit.
    /// </summary>
    [InlineArray(Av1Constants.WienerCoefficientCount)]
    public struct WienerCoefficientBuffer
    {
        private int element0;
    }

    /// <summary>
    /// Stores the projection coefficients inline with the restoration unit.
    /// </summary>
    [InlineArray(2)]
    public struct SgrProjectionCoefficientBuffer
    {
        private int element0;
    }
}
