// <copyright file="IccDataToDataConverter.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Colors.Spaces.Conversion.Implementation.Icc
{
    /// <summary>
    /// Color converter for ICC profiles
    /// </summary>
    internal class IccDataToDataConverter : IccDataToPcsConverter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IccDataToDataConverter"/> class.
        /// </summary>
        /// <param name="profile">The ICC profile to use for the conversions</param>
        public IccDataToDataConverter(IccProfile profile)
            : base(profile)
        {
        }
    }
}
