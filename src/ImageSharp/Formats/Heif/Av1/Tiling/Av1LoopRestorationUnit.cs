// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Stores the filter selection and coefficients decoded for one AV1 loop-restoration unit.
/// </summary>
internal class Av1LoopRestorationUnit
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1LoopRestorationUnit"/> class.
    /// </summary>
    public Av1LoopRestorationUnit()
    {
        this.WienerVertical = new int[Av1Constants.WienerCoefficientCount];
        this.WienerHorizontal = new int[Av1Constants.WienerCoefficientCount];
        this.SgrProjectionCoefficients = new int[2];
    }

    /// <summary>
    /// Gets or sets the restoration filter selected for the unit.
    /// </summary>
    public Av1RestorationFilterType FilterType { get; set; }

    /// <summary>
    /// Gets the three transmitted symmetric vertical Wiener coefficients.
    /// </summary>
    public int[] WienerVertical { get; }

    /// <summary>
    /// Gets the three transmitted symmetric horizontal Wiener coefficients.
    /// </summary>
    public int[] WienerHorizontal { get; }

    /// <summary>
    /// Gets or sets the self-guided filter parameter-set index.
    /// </summary>
    public int SgrParameterSet { get; set; }

    /// <summary>
    /// Gets the two self-guided projection coefficients.
    /// </summary>
    public int[] SgrProjectionCoefficients { get; }
}
