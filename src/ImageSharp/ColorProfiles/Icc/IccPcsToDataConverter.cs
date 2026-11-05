// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal class IccPcsToDataConverter : IccConverterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccPcsToDataConverter"/> class.
    /// </summary>
    /// <param name="profile">The ICC profile to use for the conversions</param>
    /// <param name="renderingIntent">The rendering intent selected for the profile connection.</param>
    /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
    public IccPcsToDataConverter(IccProfile profile, IccRenderingIntent renderingIntent, IccInterpolationMethod interpolationMethod)
        : this(profile, renderingIntent, interpolationMethod, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IccPcsToDataConverter"/> class with a channel-curve policy.
    /// </summary>
    /// <param name="profile">The profile used for conversion.</param>
    /// <param name="renderingIntent">The rendering intent selected for the profile connection.</param>
    /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
    /// <param name="symmetricCurves">Whether analytic curves convert magnitudes and retain their signs.</param>
    public IccPcsToDataConverter(
        IccProfile profile,
        IccRenderingIntent renderingIntent,
        IccInterpolationMethod interpolationMethod,
        bool symmetricCurves)
        : base(profile, false, renderingIntent, interpolationMethod, symmetricCurves)
    {
    }
}
