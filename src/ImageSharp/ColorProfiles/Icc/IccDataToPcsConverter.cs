// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal class IccDataToPcsConverter : IccConverterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccDataToPcsConverter"/> class.
    /// </summary>
    /// <param name="profile">The ICC profile to use for the conversions</param>
    /// <param name="renderingIntent">The rendering intent selected for the profile connection.</param>
    /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
    public IccDataToPcsConverter(IccProfile profile, IccRenderingIntent renderingIntent, IccInterpolationMethod interpolationMethod)
        : this(profile, renderingIntent, interpolationMethod, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccDataToPcsConverter"/> class with the specified curve settings.
    /// </summary>
    /// <param name="profile">The profile used for conversion.</param>
    /// <param name="renderingIntent">The rendering intent selected for the profile connection.</param>
    /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
    /// <param name="symmetricCurves">Whether gamma and parametric curves use the input magnitude and restore its negative sign.</param>
    public IccDataToPcsConverter(
        IccProfile profile,
        IccRenderingIntent renderingIntent,
        IccInterpolationMethod interpolationMethod,
        bool symmetricCurves)
        : base(profile, true, renderingIntent, interpolationMethod, symmetricCurves)
    {
    }
}
