// <copyright file="PointInfo.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing
{
    /// <summary>
    /// Returns details about how far away from the inside of a shape and the color the pixel could be.
    /// </summary>
    public struct PointInfo
    {
        /// <summary>
        /// The distance along path
        /// </summary>
        public float DistanceAlongPath;

        /// <summary>
        /// The distance from path
        /// </summary>
        public float DistanceFromPath;
    }
}
