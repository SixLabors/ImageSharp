// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DoubleExtensions.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encapsulates a series of time saving extension methods to the <see cref="T:System.Double" /> class.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Common.Extensions
{
    using System;

    using ImageProcessor.Imaging.Helpers;

    /// <summary>
    /// Encapsulates a series of time saving extension methods to the <see cref="T:System.Double"/> class.
    /// </summary>
    public static class DoubleExtensions
    {
        /// <summary>
        /// Converts an <see cref="T:System.Double"/> value into a valid <see cref="T:System.Byte"/>.
        /// <remarks>
        /// If the value given is less than 0 or greater than 255, the value will be constrained into
        /// those restricted ranges.
        /// </remarks>
        /// </summary>
        /// <param name="value">
        /// The <see cref="T:System.Double"/> to convert.
        /// </param>
        /// <returns>
        /// The <see cref="T:System.Byte"/>.
        /// </returns>
        public static byte ToByte(this double value)
        {
            return Convert.ToByte(ImageMaths.Clamp(value, 0, 255));
        }
    }
}
