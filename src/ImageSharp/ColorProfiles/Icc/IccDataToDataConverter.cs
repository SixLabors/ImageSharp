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
    /// <param name="interpolationMethod">The interpolation method used for color lookup tables.</param>
    public IccDataToDataConverter(IccProfile profile, IccInterpolationMethod interpolationMethod)
        : base(profile, true, interpolationMethod) // toPCS is true because in this case the PCS space is also a data space
    {
    }
}
