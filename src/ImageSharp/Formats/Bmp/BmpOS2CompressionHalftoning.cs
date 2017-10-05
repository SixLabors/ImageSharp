// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// This is the IBM OS/2 BMP v2 (and above) halftoning type used.
    /// <para>Supported since IBM OS/2 2.0.</para>
    /// <seealso href="http://www.fileformat.info/format/os2bmp/egff.htm">See this FileFormat link for more information.</seealso>
    /// <seealso href="https://en.wikipedia.org/wiki/BMP_file_format">See this Wikipedia link for more information.</seealso>
    /// </summary>
    internal enum BmpOS2CompressionHalftoning : ushort
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
