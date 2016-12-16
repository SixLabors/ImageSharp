// <copyright file="PointInfo.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Drawing.Paths
{
    using System.Numerics;

    /// <summary>
    /// Returns meta data about the nearest point on a path from a vector
    /// </summary>
    public struct PointInfo
    {
        /// <summary>
        /// The search point
        /// </summary>
        public Vector2 SearchPoint;

        /// <summary>
        /// The distance along path <see cref="ClosestPointOnPath"/> is away from the start of the path
        /// </summary>
        public float DistanceAlongPath;

        /// <summary>
        /// The distance <see cref="SearchPoint"/> is away from <see cref="ClosestPointOnPath"/>.
        /// </summary>
        public float DistanceFromPath;

        /// <summary>
        /// The closest point to <see cref="SearchPoint"/> that lies on the path.
        /// </summary>
        public Vector2 ClosestPointOnPath;
    }
}
