// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc
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
