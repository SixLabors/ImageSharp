// <copyright file="IColorReader.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    /// <summary>
    /// Encapsulates methods for color readers, which are responsible for reading
    /// different color formats from a png file.
    /// </summary>
    public interface IColorReader
    {
        /// <summary>
        /// Reads the specified scanline.
        /// </summary>
        /// <typeparam name="T">The pixel format.</typeparam>
        /// <typeparam name="TP">The packed format. <example>long, float.</example></typeparam>
        /// <param name="scanline">The scanline.</param>
        /// <param name="pixels">The pixels to read the image row to.</param>
        /// <param name="header">
        /// The header, which contains information about the png file, like
        /// the width of the image and the height.
        /// </param>
        void ReadScanline<T, TP>(byte[] scanline, T[] pixels, PngHeader header)
            where T : IPackedVector<TP>, new()
            where TP : struct;
    }
}
