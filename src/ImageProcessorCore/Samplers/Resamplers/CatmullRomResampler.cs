// <copyright file="CatmullRomResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// The Catmull-Rom filter is a well known standard Cubic Filter often used as a interpolation function.
    /// This filter produces a reasonably sharp edge, but without a the pronounced gradient change on large 
    /// scale image enlargements that a 'Lagrange' filter can produce.
    /// <see href="http://www.imagemagick.org/Usage/filter/#cubic_bc"/>
    /// </summary>
    public class CatmullRomResampler : IResampler
    {
        /// <inheritdoc/>
        public double Radius => 2;

        /// <inheritdoc/>
        public double GetValue(double x)
        {
            const double B = 0;
            const double C = 0.5d;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
