// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

//https://github.com/toji/texture-
//https://docs.microsoft.com/en-us/windows/win32/direct3ddds/dx-graphics-dds-pguide
//https://docs.microsoft.com/en-us/windows/uwp/gaming/complete-code-for-ddstextureloader

using System;
using System.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Dds
{
    /// <summary>
    /// Image decoder for DDS images.
    /// </summary>
    public sealed class DdsDecoder : IImageDecoder, IDdsDecoderOptions, IImageInfoDetector
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public Image Decode(Configuration configuration, Stream stream)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public Texture<TPixel> DecodeTexture<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            return new DdsDecoderCore(configuration, this).DecodeTexture<TPixel>(stream);
        }

        /// <inheritdoc />
        public Texture DecodeTexture(Configuration configuration, Stream stream)
        {
            return this.DecodeTexture<Rgba32>(configuration, stream);
        }

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            return new DdsDecoderCore(configuration, this).Identify(stream);
        }
    }
}
