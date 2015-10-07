// <copyright file="ImageMaths.cs" company="James South">
// Copyright © James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor
{
    using System;

    /// <summary>
    /// Provides common mathematical methods.
    /// </summary>
    internal static class ImageMaths
    {
        /// <summary>
        /// Gets the result of a sine cardinal function for the given value.
        /// </summary>
        /// <param name="x">
        /// The value to calculate the result for.
        /// </param>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public static double SinC(double x)
        {
            const double Epsilon = .0001;

            if (Math.Abs(x) > Epsilon)
            {
                x *= Math.PI;
                return Clean(Math.Sin(x) / x);
            }

            return 1.0;
        }

        /// <summary>
        /// Ensures that any passed double is correctly rounded to zero
        /// </summary>
        /// <param name="x">The value to clean.</param>
        /// <returns>
        /// The <see cref="double"/>
        /// </returns>.
        private static double Clean(double x)
        {
            const double Epsilon = .0001;

            if (Math.Abs(x) < Epsilon)
            {
                return 0.0;
            }

            return x;
        }
    }
}
