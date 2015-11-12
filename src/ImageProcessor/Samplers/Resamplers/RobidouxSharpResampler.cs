// <copyright file="RobidouxSharpResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Samplers
{
    /// <summary>
    /// The function implements the Robidoux Sharp algorithm.
    /// <see href="http://www.imagemagick.org/Usage/filter/#robidoux"/>
    /// </summary>
    public class RobidouxSharpResampler : IResampler
    {
        /// <inheritdoc/>
        public float Radius => 2;

        /// <inheritdoc/>
        public float GetValue(float x)
        {
            const float B = 0.2620f;
            const float C = 0.3690f;

            return ImageMaths.GetBcValue(x, B, C);
        }
    }
}
