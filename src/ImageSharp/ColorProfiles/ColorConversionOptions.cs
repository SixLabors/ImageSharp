// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.WorkingSpaces;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.ColorProfiles;

/// <summary>
/// Provides options for color profile conversion.
/// </summary>
public class ColorConversionOptions
{
    private Matrix4x4 adaptationMatrix;

    /// <summary>
    /// Initializes a new instance of the <see cref="ColorConversionOptions"/> class.
    /// </summary>
    public ColorConversionOptions() => this.AdaptationMatrix = KnownChromaticAdaptationMatrices.Bradford;

    /// <summary>
    /// Gets the memory allocator.
    /// </summary>
    public MemoryAllocator MemoryAllocator { get; init; } = MemoryAllocator.Default;

    /// <summary>
    /// Gets the source white point used for chromatic adaptation in conversions from/to XYZ color space.
    /// </summary>
    public CieXyz WhitePoint { get; init; } = KnownIlluminants.D50;

    /// <summary>
    /// Gets the destination white point used for chromatic adaptation in conversions from/to XYZ color space.
    /// </summary>
    public CieXyz TargetWhitePoint { get; init; } = KnownIlluminants.D50;

    /// <summary>
    /// Gets the source working space used for companding in conversions from/to XYZ color space.
    /// </summary>
    public RgbWorkingSpace RgbWorkingSpace { get; init; } = KnownRgbWorkingSpaces.SRgb;

    /// <summary>
    /// Gets the destination working space used for companding in conversions from/to XYZ color space.
    /// </summary>
    public RgbWorkingSpace TargetRgbWorkingSpace { get; init; } = KnownRgbWorkingSpaces.SRgb;

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
            Matrix4x4.Invert(value, out Matrix4x4 inverted);
            this.InverseAdaptationMatrix = inverted;
        }
    }

    internal Matrix4x4 InverseAdaptationMatrix { get; private set; }
}
