// <copyright file="PointInfoExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    /// <summary>
    /// Extension methods for helping to bridge Shaper2D and ImageSharp primitives.
    /// </summary>
    internal static class PointInfoExtensions
    {
        /// <summary>
        /// Converts a <see cref="SixLabors.Shapes.PointInfo"/> to an ImageSharp <see cref="PointInfo"/>.
        /// </summary>
        /// <param name="source">The image this method extends.</param>
        /// <returns>A <see cref="PointInfo"/> representation of this <see cref="SixLabors.Shapes.PointInfo"/></returns>
        public static PointInfo Convert(this SixLabors.Shapes.PointInfo source)
        {
            return new PointInfo
            {
                DistanceAlongPath = source.DistanceAlongPath,
                DistanceFromPath = source.DistanceFromPath
            };
        }
    }
}