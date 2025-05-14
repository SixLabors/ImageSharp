// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal class IccPcsToPcsConverter : IccConverterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccPcsToPcsConverter"/> class.
    /// </summary>
    /// <param name="profile">The ICC profile to use for the conversions</param>
    public IccPcsToPcsConverter(IccProfile profile)
        : base(profile, true)
    {
    }
}
