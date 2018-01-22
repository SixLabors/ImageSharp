// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData.Profiles.Icc;

namespace SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.Icc
{
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
            : base(profile, true) // toPCS is true because in this case the PCS space is also a data space
        {
        }
    }
}
