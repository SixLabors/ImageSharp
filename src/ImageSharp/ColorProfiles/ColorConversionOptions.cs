// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Provides options for color profile conversion.
/// </summary>
public class ColorConversionOptions
{
    private Matrix4x4 adaptationMatrix;
    private YCbCrTransform yCbCrTransform;
    private XybTransform xybTransform;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorConversionOptions"/> class.
    /// </summary>
    public ColorConversionOptions()
    {
        this.AdaptationMatrix = KnownChromaticAdaptationMatrices.Bradford;
        this.YCbCrTransform = KnownYCbCrMatrices.BT601;
        this.XybTransform = KnownXybTransforms.Iso18181;
    }

    /// <summary>
    /// Gets the memory allocator.
    /// </summary>
    public MemoryAllocator MemoryAllocator { get; init; } = MemoryAllocator.Default;

    /// <summary>
    /// Gets the source white point used for chromatic adaptation in conversions from/to XYZ color space.
    /// </summary>
    public CieXyz SourceWhitePoint { get; init; } = KnownIlluminants.D50;

    /// <summary>
    /// Gets the destination white point used for chromatic adaptation in conversions from/to XYZ color space.
    /// </summary>
    public CieXyz TargetWhitePoint { get; init; } = KnownIlluminants.D50;

    /// <summary>
    /// Gets the source working space used for companding in conversions from/to XYZ color space.
    /// </summary>
    public RgbWorkingSpace SourceRgbWorkingSpace { get; init; } = KnownRgbWorkingSpaces.SRgb;

    /// <summary>
    /// Gets the destination working space used for companding in conversions from/to XYZ color space.
    /// </summary>
    public RgbWorkingSpace TargetRgbWorkingSpace { get; init; } = KnownRgbWorkingSpaces.SRgb;

    /// <summary>
    /// Gets the transform used for conversions between XYB and RGB in both directions.
    /// The default is <see cref="KnownXybTransforms.Iso18181"/>, with the standard JPEG XL coefficients and an intensity target of 255 nits.
    /// </summary>
    /// <remarks>
    /// The matrices must match <see cref="SourceRgbWorkingSpace"/> when converting from XYB,
    /// or <see cref="TargetRgbWorkingSpace"/> when converting to XYB. The ISO coefficients require sRGB primaries and D65.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is an uninitialized transform with a zero intensity target.</exception>
    public XybTransform XybTransform
    {
        get => this.xybTransform;
        init
        {
            // A default struct bypasses the transform constructor. Reject its zero intensity before scaling matrices.
            Guard.MustBeGreaterThan(value.IntensityTarget, 0F, nameof(value));

            this.xybTransform = value;

            this.TransposedXybTransform = value.Transpose();
        }
    }

    /// <summary>
    /// Gets the YCbCr matrix to used to perform conversions from/to RGB.
    /// </summary>
    public YCbCrTransform YCbCrTransform
    {
        get => this.yCbCrTransform;
        init
        {
            this.yCbCrTransform = value;
            this.TransposedYCbCrTransform = value.Transpose();
        }
    }

    /// <summary>
    /// Gets the source ICC profile.
    /// </summary>
    public IccProfile? SourceIccProfile { get; init; }

    /// <summary>
    /// Gets the target ICC profile.
    /// </summary>
    public IccProfile? TargetIccProfile { get; init; }

    /// <summary>
    /// Gets the interpolation method used for ICC color lookup tables. Defaults to <see cref="IccInterpolationMethod.Auto"/>.
    /// </summary>
    public IccInterpolationMethod IccInterpolationMethod { get; init; }

    /// <summary>
    /// Gets the transformation matrix used in conversion to perform chromatic adaptation.
    /// <see cref="KnownChromaticAdaptationMatrices"/> for further information. Default is Bradford.
    /// </summary>
    public Matrix4x4 AdaptationMatrix
    {
        get => this.adaptationMatrix;
        init
        {
            this.adaptationMatrix = value;
            _ = Matrix4x4.Invert(value, out Matrix4x4 inverted);
            this.InverseAdaptationMatrix = inverted;
        }
    }

    internal YCbCrTransform TransposedYCbCrTransform { get; private set; }

    /// <summary>
    /// Gets the XYB matrices in the row-vector layout and intensity scale used by conversion.
    /// </summary>
    internal XybTransform TransposedXybTransform { get; private set; }

    internal Matrix4x4 InverseAdaptationMatrix { get; private set; }
}
