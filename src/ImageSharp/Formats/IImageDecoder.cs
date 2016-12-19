// <copyright file="IImageDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Encapsulates properties and methods required for decoding an image from a stream.
    /// </summary>
    public interface IImageDecoder
    {
        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase{TColor, TPacked}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <typeparam name="TPacked">The packed format. <example>uint, long, float.</example></typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor, TPacked}"/> to decode to.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        void Decode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream)
            where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct, IEquatable<TPacked>;
    }
}
