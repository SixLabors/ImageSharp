// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes one layer of a layered image. A layered image stores the same picture several times, from the lowest
/// quality to the highest. A viewer can show the first layer as soon as it arrives. Then it replaces the picture
/// with each later layer, so the image appears quickly and then gets sharper.
/// </summary>
public sealed class HeifLayer
{
    /// <summary>
    /// Backing field for <see cref="Quality"/>.
    /// </summary>
    private int? quality;

    /// <summary>
    /// Backing field for <see cref="AlphaQuality"/>.
    /// </summary>
    private int? alphaQuality;

    /// <summary>
    /// Backing field for <see cref="Scale"/>.
    /// </summary>
    private HeifLayerScale scale;

    /// <summary>
    /// Gets the lossy compression quality of the layer, or <see langword="null"/> to use the quality of the encoder.
    /// Valid values range from 0 for the lowest quality to 100 for the highest quality. Defaults to
    /// <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The quality is outside the range 0 to 100.</exception>
    public int? Quality
    {
        get => this.quality;
        init
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentException("Quality must be in the range [0..100].");
            }

            this.quality = value;
        }
    }

    /// <summary>
    /// Gets the lossy compression quality of the alpha in the layer, or <see langword="null"/> to use the alpha quality
    /// of the encoder. Valid values range from 0 for the lowest quality to 100 for the highest quality. This option has
    /// no effect when the image has no alpha. Defaults to <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The alpha quality is outside the range 0 to 100.</exception>
    public int? AlphaQuality
    {
        get => this.alphaQuality;
        init
        {
            if (value is < 0 or > 100)
            {
                throw new ArgumentException("Alpha quality must be in the range [0..100].");
            }

            this.alphaQuality = value;
        }
    }

    /// <summary>
    /// Gets the width and height of the layer as a fraction of the image size. A smaller layer makes a smaller file.
    /// The layer width and height round up to whole pixels. A layer must be at least half the width and height of the
    /// layer before it. The last layer must have the size of the image. Defaults to
    /// <see cref="HeifLayerScale.Full"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The value is not a defined <see cref="HeifLayerScale"/>.</exception>
    public HeifLayerScale Scale
    {
        get => this.scale;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentException("Scale must be a defined HeifLayerScale value.");
            }

            this.scale = value;
        }
    }

    /// <summary>
    /// Returns the numerator and denominator of a layer scale.
    /// </summary>
    /// <param name="scale">The layer scale.</param>
    /// <returns>The fraction of the image size.</returns>
    internal static (int Numerator, int Denominator) GetFraction(HeifLayerScale scale) => scale switch
    {
        HeifLayerScale.FourFifths => (4, 5),
        HeifLayerScale.ThreeQuarters => (3, 4),
        HeifLayerScale.ThreeFifths => (3, 5),
        HeifLayerScale.Half => (1, 2),
        HeifLayerScale.Quarter => (1, 4),
        HeifLayerScale.Eighth => (1, 8),
        HeifLayerScale.Full => (1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(scale))
    };
}
