// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides enumeration of available PNG optimization methods.
    /// </summary>
    [Flags]
    public enum PngChunkFilter
    {
        /// <summary>
        /// With the None filter, all chunks will be written.
        /// </summary>
        None = 0,

        /// <summary>
        /// Excludes the physical dimension information chunk from encoding.
        /// </summary>
        ExcludePhysicalChunk = 1 << 0,

        /// <summary>
        /// Excludes the gamma information chunk from encoding.
        /// </summary>
        ExcludeGammaChunk = 1 << 1,

        /// <summary>
        /// Excludes the eXIf chunk from encoding.
        /// </summary>
        ExcludeExifChunk = 1 << 2,

        /// <summary>
        /// Excludes the tTXt, iTXt or zTXt chunk from encoding.
        /// </summary>
        ExcludeTextChunks = 1 << 3,

        /// <summary>
        /// All ancillary chunks will be excluded.
        /// </summary>
        ExcludeAll = ~None
    }
}
