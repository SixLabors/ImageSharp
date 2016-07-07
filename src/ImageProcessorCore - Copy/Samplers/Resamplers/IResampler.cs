// <copyright file="IResampler.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    /// <summary>
    /// Encapsulates an interpolation algorithm for resampling images.
    /// </summary>
    public interface IResampler
    {
        /// <summary>
        /// Gets the radius in which to sample pixels.
        /// </summary>
        float Radius { get; }

        /// <summary>
        /// Gets the result of the interpolation algorithm.
        /// </summary>
        /// <param name="x">The value to process.</param>
        /// <returns>
        /// The <see cref="float"/>
        /// </returns>
        float GetValue(float x);
    }
}
