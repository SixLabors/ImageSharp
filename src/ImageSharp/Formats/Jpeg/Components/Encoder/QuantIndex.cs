// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    ///     Enumerates the quantization tables
    /// </summary>
    internal enum QuantIndex
    {
        /// <summary>
        ///     The luminance quantization table index
        /// </summary>
        Luminance = 0,

        /// <summary>
        ///     The chrominance quantization table index
        /// </summary>
        Chrominance = 1,
    }
}