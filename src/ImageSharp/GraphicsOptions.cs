// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides configuration for controlling how graphics operations are rendered,
/// including antialiasing, pixel blending, and alpha composition.
/// </summary>
public class GraphicsOptions : IDeepCloneable<GraphicsOptions>
{
    private float blendPercentage = 1F;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsOptions"/> class.
    /// </summary>
    public GraphicsOptions()
    {
    }

    private GraphicsOptions(GraphicsOptions source)
    {
        this.AlphaCompositionMode = source.AlphaCompositionMode;
        this.Antialias = source.Antialias;
        this.BlendPercentage = source.BlendPercentage;
        this.ColorBlendingMode = source.ColorBlendingMode;
    }

    /// <summary>
    /// Gets or sets a value indicating whether antialiasing should be applied.
    /// When <see langword="true"/>, edges are rendered with smooth sub-pixel coverage.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool Antialias { get; set; } = true;

    /// <summary>
    /// Gets or sets the blending percentage applied to the drawing operation.
    /// A value of <c>1.0</c> applies the operation at full strength; <c>0.0</c> makes it invisible.
    /// Valid range is 0 to 1. Defaults to <c>1.0F</c>.
    /// </summary>
    public float BlendPercentage
    {
        get => this.blendPercentage;

        set
        {
            Guard.MustBeBetweenOrEqualTo(value, 0F, 1F, nameof(this.BlendPercentage));
            this.blendPercentage = value;
        }
    }

    /// <summary>
    /// Gets or sets the color blending mode used to combine source and destination pixel colors.
    /// Defaults to <see cref="PixelColorBlendingMode.Normal"/>.
    /// </summary>
    public PixelColorBlendingMode ColorBlendingMode { get; set; } = PixelColorBlendingMode.Normal;

    /// <summary>
    /// Gets or sets the alpha composition mode that determines how source and destination alpha
    /// channels are combined using Porter-Duff operators.
    /// Defaults to <see cref="PixelAlphaCompositionMode.SrcOver"/>.
    /// </summary>
    public PixelAlphaCompositionMode AlphaCompositionMode { get; set; } = PixelAlphaCompositionMode.SrcOver;

    /// <inheritdoc/>
    public GraphicsOptions DeepClone() => new(this);
}
