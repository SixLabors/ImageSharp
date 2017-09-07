// <copyright file="BmpOS2CompressionHalftoning.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// This is the IBM OS/2 BMP v2 (and above) halftoning type used.
    /// <para>Supported by OS/2 2.0x or later.</para>
    /// </summary>
    internal enum BmpOS2CompressionHalftoning
    {
        /// <summary>
        /// No halftoning.
        /// </summary>
        NoHalftoning = 0,

        /// <summary>
        /// Error-diffusion halftoning.
        /// </summary>
        ErrorDiffusion = 1,

        /// <summary>
        /// Processing Algorithm for Noncoded Document Acquisition (PANDA) halftoning.
        /// </summary>
        Panda = 2,

        /// <summary>
        /// Super-circle halftoning.
        /// </summary>
        SuperCircle = 3
    }
}
