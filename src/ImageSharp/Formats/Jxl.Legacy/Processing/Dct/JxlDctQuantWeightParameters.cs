// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;

/// <summary>
/// Stores one DCT weight seed and its distance-band deltas for each XYB channel.
/// </summary>
/// <remarks>
/// The fixed rows contain 17 entries. Only NumDistanceBands entries participate in table generation or serialization.
/// Values are copied into descriptors during preparation. Parsers update this value by reference.
/// </remarks>
internal struct JxlDctQuantWeightParameters
{
    /// <summary>The bit count used to store the distance-band count minus one.</summary>
    public const int Log2MaxDistanceBands = 4;

    /// <summary>The three fixed channel rows. Entries after NumDistanceBands do not participate in interpolation.</summary>
    [SuppressMessage(
        "StyleCop.CSharp.MaintainabilityRules",
        "SA1401:Fields should be private",
        Justification = "Fixed parameter storage must support reference parsing without value copies.")]
    public InlineArray3<InlineArray17<float>> DistanceBands;

    /// <summary>Copies three packed channel rows into their fixed parameter storage.</summary>
    /// <param name="distanceBands">Three consecutive rows, each containing numDistanceBands values.</param>
    /// <param name="numDistanceBands">The number of values in each channel row.</param>
    public JxlDctQuantWeightParameters(ReadOnlySpan<float> distanceBands, int numDistanceBands)
    {
        this = default;
        this.NumDistanceBands = numDistanceBands;
        for (int c = 0; c < 3; c++)
        {
            Span<float> destination = this.DistanceBands[c];
            distanceBands.Slice(c * numDistanceBands, numDistanceBands).CopyTo(destination);
        }
    }

    /// <summary>Gets or sets the number of active seed and delta values in each channel row.</summary>
    public int NumDistanceBands { get; set; }
}
