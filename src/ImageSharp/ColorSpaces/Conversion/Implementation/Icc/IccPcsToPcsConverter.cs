// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Icc
{
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
}
