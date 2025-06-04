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

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorConversionOptions"/> class.
    /// </summary>
    public ColorConversionOptions()
    {
        this.AdaptationMatrix = KnownChromaticAdaptationMatrices.Bradford;
        this.YCbCrTransform = KnownYCbCrMatrices.BT601;
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

    internal Matrix4x4 InverseAdaptationMatrix { get; private set; }
}
