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
    public IccPcsToDataConverter(IccProfile profile, IccRenderingIntent renderingIntent)
        : base(profile, false, renderingIntent)
    {
    }
}
