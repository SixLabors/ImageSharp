// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorProfiles.Conversion.Icc;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorProfiles.Icc;

/// <summary>
/// Color converter for ICC profiles
/// </summary>
internal class IccDataToDataConverter : IccConverterBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IccDataToDataConverter"/> class.
    /// </summary>
    /// <param name="profile">The ICC profile to use for the conversions</param>
    public IccDataToDataConverter(IccProfile profile)

        // toPCS is true because the PCS space is also a data space for a DeviceLink profile.
        // The shared base constructor requires an intent. Pass the profile's header value;
        // DeviceLink transform selection uses CheckMethod2 and ignores rendering intent.
        : base(profile, true, profile.Header.RenderingIntent)
    {
    }
}
