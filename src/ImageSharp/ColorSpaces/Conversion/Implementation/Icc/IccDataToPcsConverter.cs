// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal class IccDataToPcsConverter : IccConverterBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataToPcsConverter"/> class.
        /// </summary>
        /// <param name="profile">The ICC profile to use for the conversions</param>
        public IccDataToPcsConverter(IccProfile profile)
            : base(profile, true)
        {
        }
    }
}
