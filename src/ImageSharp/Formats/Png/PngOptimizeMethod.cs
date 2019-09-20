// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Provides enumeration of available PNG optimization methods.
    /// </summary>
    [Flags]
    public enum PngOptimizeMethod
    {
        /// <summary>
        /// With the None filter, the scanline is transmitted unmodified.
        /// </summary>
        None = 0,

        /// <summary>
        /// Suppress the physical dimension information chunk.
        /// </summary>
        SuppressPhysicalChunk = 1,

        /// <summary>
        /// Suppress the gamma information chunk.
        /// </summary>
        SuppressGammaChunk = 2,

        /// <summary>
        /// Suppress the eXIf chunk.
        /// </summary>
        SuppressExifChunk = 4,

        /// <summary>
        /// Suppress the tTXt, iTXt or zTXt chunk.
        /// </summary>
        SuppressTextChunks = 8,

        /// <summary>
        /// Make funlly transparent pixels black.
        /// </summary>
        MakeTransparentBlack = 16,

        /// <summary>
        /// All possible optimizations.
        /// </summary>
        All = 31,
    }
}
