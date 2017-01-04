// <copyright file="QuantIndex.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>
namespace ImageSharp.Formats.Jpg
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