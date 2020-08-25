// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    public class MagickReferenceDecoder : IImageDecoder
    {
        public static MagickReferenceDecoder Instance { get; } = new MagickReferenceDecoder();

        private static void FromRgba32Bytes<TPixel>(Configuration configuration, Span<byte> rgbaBytes, IMemoryGroup<TPixel> destinationGroup)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            foreach (Memory<TPixel> m in destinationGroup)
            {
                Span<TPixel> destBuffer = m.Span;
                PixelOperations<TPixel>.Instance.FromRgba32Bytes(
                    configuration,
                    rgbaBytes,
                    destBuffer,
                    destBuffer.Length);
                rgbaBytes = rgbaBytes.Slice(destBuffer.Length * 4);
            }
        }

        private static void FromRgba64Bytes<TPixel>(Configuration configuration, Span<byte> rgbaBytes, IMemoryGroup<TPixel> destinationGroup)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            foreach (Memory<TPixel> m in destinationGroup)
            {
                Span<TPixel> destBuffer = m.Span;
                PixelOperations<TPixel>.Instance.FromRgba64Bytes(
                    configuration,
                    rgbaBytes,
                    destBuffer,
                    destBuffer.Length);
                rgbaBytes = rgbaBytes.Slice(destBuffer.Length * 8);
            }
        }

        public Task<Image<TPixel>> DecodeAsync<TPixel>(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
            => Task.FromResult(this.Decode<TPixel>(configuration, stream));

        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using var magickImageCollection = new MagickImageCollection(stream);
            var framesList = new List<ImageFrame<TPixel>>();
            foreach (IMagickImage magicFrame in magickImageCollection)
            {
                var frame = new ImageFrame<TPixel>(configuration, magicFrame.Width, magicFrame.Height);
                framesList.Add(frame);

                MemoryGroup<TPixel> framePixels = frame.PixelBuffer.FastMemoryGroup;
                using (IPixelCollection pixels = magicFrame.GetPixelsUnsafe())
                {
                    if (magicFrame.Depth == 8)
                    {
                        byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                        FromRgba32Bytes(configuration, data, framePixels);
                    }
                    else if (magicFrame.Depth == 16)
                    {
                        ushort[] data = pixels.ToShortArray(PixelMapping.RGBA);
                        Span<byte> bytes = MemoryMarshal.Cast<ushort, byte>(data.AsSpan());
                        FromRgba64Bytes(configuration, bytes, framePixels);
                    }
                    else
                    {
                        throw new InvalidOperationException();
                    }
                }
            }

            var result = new Image<TPixel>(configuration, new ImageMetadata(), framesList);

            return result;
        }

        public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken);
    }
}
