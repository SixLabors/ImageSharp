// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Describes the type, precision, name and alpha representation of a JPEG XL extra channel.
/// </summary>
public sealed class JxlExtraChannelMetadata
{
    private string name = string.Empty;
    private byte dimensionShift;
    private ushort colorFilterArrayChannel;
    private Vector4 spotColor;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlExtraChannelMetadata"/> class.
    /// </summary>
    /// <param name="type">The extra channel type.</param>
    /// <param name="bitDepth">The channel sample precision.</param>
    /// <param name="alphaRepresentation">The alpha representation, or <see cref="PixelAlphaRepresentation.None"/> for other channel types.</param>
    /// <exception cref="ArgumentOutOfRangeException">The sample layout is uninitialized.</exception>
    /// <exception cref="ArgumentException">The channel type or alpha representation is invalid.</exception>
    public JxlExtraChannelMetadata(JxlExtraChannelType type, JxlBitDepth bitDepth, PixelAlphaRepresentation alphaRepresentation)
    {
        // Values 7 to 14 are reserved. Value 15 is an unknown required channel and cannot be decoded.
        bool validType = type is (>= JxlExtraChannelType.Alpha and <= JxlExtraChannelType.Thermal)
            or JxlExtraChannelType.Optional;

        Guard.IsTrue(validType, nameof(type), "The extra channel type is not supported by JPEG XL.");

        Guard.MustBeGreaterThan(bitDepth.BitsPerSample, (byte)0, nameof(bitDepth));

        bool validAlpha = type == JxlExtraChannelType.Alpha
            ? alphaRepresentation is PixelAlphaRepresentation.Associated or PixelAlphaRepresentation.Unassociated
            : alphaRepresentation == PixelAlphaRepresentation.None;

        Guard.IsTrue(validAlpha, nameof(alphaRepresentation), "Only an alpha channel can specify an alpha representation.");

        this.Type = type;
        this.BitDepth = bitDepth;
        this.AlphaRepresentation = alphaRepresentation;
        this.colorFilterArrayChannel = type == JxlExtraChannelType.ColorFilterArray ? (ushort)1 : (ushort)0;
    }

    /// <summary>
    /// Gets the extra channel type.
    /// </summary>
    public JxlExtraChannelType Type { get; }

    /// <summary>
    /// Gets the channel sample precision.
    /// </summary>
    public JxlBitDepth BitDepth { get; }

    /// <summary>
    /// Gets the optional channel label stored in the file. The default is an empty string.
    /// Its UTF-8 encoding cannot exceed 1071 bytes. The <see cref="Type"/> property identifies the channel behavior.
    /// </summary>
    /// <exception cref="ArgumentNullException">The label is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The encoded label exceeds 1071 bytes.</exception>
    public string Name
    {
        get => this.name;
        init
        {
            Guard.NotNull(value, nameof(value));

            // The format bounds UTF-8 bytes, not UTF-16 characters. Count bytes without allocating an encoded copy.
            Guard.MustBeLessThanOrEqualTo(Encoding.UTF8.GetByteCount(value), JxlConstants.MaximumNameByteLength, nameof(value));

            this.name = value;
        }
    }

    /// <summary>
    /// Gets the channel alpha representation. Non-alpha channels return <see cref="PixelAlphaRepresentation.None"/>.
    /// </summary>
    public PixelAlphaRepresentation AlphaRepresentation { get; }

    /// <summary>
    /// Gets the base-two reduction in channel dimensions relative to the color image. The range is zero to three.
    /// </summary>
    public byte DimensionShift
    {
        get => this.dimensionShift;
        init
        {
            Guard.MustBeLessThanOrEqualTo(value, (byte)3, nameof(value));

            this.dimensionShift = value;
        }
    }

    /// <summary>
    /// Gets the RGB components and mixing weight used to render a spot-color channel.
    /// Each component must be finite and within half-precision storage limits.
    /// Other channel types ignore this value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A component is not finite or exceeds half-precision storage limits.</exception>
    public Vector4 SpotColor
    {
        get => this.spotColor;
        init
        {
            // An ordered magnitude comparison rejects NaN and infinity as well as finite overflow.
            // It validates storage limits without rounding, clamping or changing any component.
            if (!Vector128.LessThanOrEqualAll(Vector128.Abs(value.AsVector128()), Vector128.Create((float)Half.MaxValue)))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Spot-color components must be finite and within half-precision storage limits.");
            }

            this.spotColor = value;
        }
    }

    /// <summary>
    /// Gets the color-filter-array channel index, from zero to 274. Other channel types ignore this value.
    /// </summary>
    public ushort ColorFilterArrayChannel
    {
        get => this.colorFilterArrayChannel;
        init
        {
            Guard.MustBeLessThanOrEqualTo(value, (ushort)274, nameof(value));

            this.colorFilterArrayChannel = value;
        }
    }
}
