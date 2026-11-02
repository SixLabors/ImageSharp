// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

/// <summary>
/// Borrows native component planes and their selected reconstruction-weight plane.
/// </summary>
internal readonly ref struct JxlBlendInput
{
    /// <summary>
    /// Initializes a new instance of the JxlBlendInput structure.
    /// </summary>
    /// <param name="component0">The first native component row. Its length determines the sample count.</param>
    /// <param name="component1">The second native component row, or an empty span to supply zero.</param>
    /// <param name="component2">The third native component row, or an empty span to supply zero.</param>
    /// <param name="weight">The selected reconstruction weights. These are not pixel-format alpha values.</param>
    public JxlBlendInput(ReadOnlySpan<float> component0, ReadOnlySpan<float> component1, ReadOnlySpan<float> component2, ReadOnlySpan<float> weight)
    {
        this.Component0 = component0;
        this.Component1 = component1;
        this.Component2 = component2;
        this.Weight = weight;
    }

    /// <summary>
    /// Gets native component row 0.
    /// </summary>
    public ReadOnlySpan<float> Component0 { get; }

    /// <summary>
    /// Gets native component row 1.
    /// </summary>
    public ReadOnlySpan<float> Component1 { get; }

    /// <summary>
    /// Gets native component row 2.
    /// </summary>
    public ReadOnlySpan<float> Component2 { get; }

    /// <summary>
    /// Gets the selected native reconstruction-weight row.
    /// </summary>
    public ReadOnlySpan<float> Weight { get; }
}
