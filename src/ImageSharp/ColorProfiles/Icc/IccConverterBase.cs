// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal abstract partial class IccConverterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccConverterBase"/> class.
    /// </summary>
    /// <param name="profile">The ICC profile to use for the conversions</param>
    /// <param name="toPcs">True if the conversion is to the profile connection space (PCS); False if the conversion is to the data space</param>
    /// <param name="renderingIntent">The rendering intent selected for the profile connection.</param>
    /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
    protected IccConverterBase(IccProfile profile, bool toPcs, IccRenderingIntent renderingIntent, IccInterpolationMethod interpolationMethod)
    {
        Guard.NotNull(profile, nameof(profile));
        this.Init(profile, toPcs, renderingIntent, interpolationMethod);
    }

    /// <summary>
    /// Converts one color using the configured profile transform.
    /// </summary>
    /// <param name="value">The profile input components.</param>
    /// <returns>The profile output components.</returns>
    public Vector4 Convert(Vector4 value) => this.calculator.Convert(value);

    /// <summary>
    /// Converts colors through the configured profile's SIMD traversal and scalar remainder.
    /// </summary>
    /// <param name="source">The source components.</param>
    /// <param name="destination">The destination components.</param>
    public void Convert(ReadOnlySpan<Vector4> source, Span<Vector4> destination) => this.calculator.Convert(source, destination);
}
