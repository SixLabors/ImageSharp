// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

/// <summary>
/// Borrows native component planes and their selected reconstruction-weight plane.
/// </summary>
internal readonly ref struct JxlBlendOutput
{
    /// <summary>
    /// Initializes a new instance of the JxlBlendOutput structure.
    /// </summary>
    /// <param name="component0">The first output component row. Its length determines the sample count.</param>
    /// <param name="component1">The second output component row, or an empty span to discard it.</param>
    /// <param name="component2">The third output component row, or an empty span to discard it.</param>
    /// <param name="weight">The output reconstruction weights, or an empty span to discard them.</param>
    public JxlBlendOutput(Span<float> component0, Span<float> component1, Span<float> component2, Span<float> weight)
    {
        this.Component0 = component0;
        this.Component1 = component1;
        this.Component2 = component2;
        this.Weight = weight;
    }

    /// <summary>
    /// Gets native component row 0.
    /// </summary>
    public Span<float> Component0 { get; }

    /// <summary>
    /// Gets native component row 1.
    /// </summary>
    public Span<float> Component1 { get; }

    /// <summary>
    /// Gets native component row 2.
    /// </summary>
    public Span<float> Component2 { get; }

    /// <summary>
    /// Gets the selected native reconstruction-weight row.
    /// </summary>
    public Span<float> Weight { get; }
}
