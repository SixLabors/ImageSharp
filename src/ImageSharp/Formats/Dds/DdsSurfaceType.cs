// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Dds
{
    /// <summary>
    /// Shows what kind of surface we're dealing with.
    /// </summary>
    public enum DdsSurfaceType
    {
        /// <summary>
        /// Default value.
        /// </summary>
        Unknown,

        /// <summary>
        /// Positive X cube map face.
        /// </summary>
        CubemapPositiveX,

        /// <summary>
        /// Negative X cube map face.
        /// </summary>
        CubemapNegativeX,

        /// <summary>
        /// Positive Y cube map face.
        /// </summary>
        CubemapPositiveY,

        /// <summary>
        /// Negative Y cube map face.
        /// </summary>
        CubemapNegativeY,

        /// <summary>
        /// Positive Z cube map face.
        /// </summary>
        CubemapPositiveZ,

        /// <summary>
        /// Negative Z cube map face.
        /// </summary>
        CubemapNegativeZ,

        /// <summary>
        /// Represents a one-dimensional texture (height == 1).
        /// </summary>
        Texture1D,

        /// <summary>
        /// Represents a usual two-dimensional texture.
        /// </summary>
        Texture2D,

        /// <summary>
        /// Represents slices of a volume texture for a one mip-map level.
        /// </summary>
        Texture3D
    }
}
