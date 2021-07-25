// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ImageMagick.Formats;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs
{
    public class MagickReferenceDecoder : IImageDecoder
    {
        private readonly bool validate;

        public MagickReferenceDecoder()
            : this(true)
        {
        }

        public MagickReferenceDecoder(bool validate) => this.validate = validate;

        public static MagickReferenceDecoder Instance { get; } = new MagickReferenceDecoder();

        private static void FromRgba32Bytes<TPixel>(Configuration configuration, Span<byte> rgbaBytes, IMemoryGroup<TPixel> destinationGroup)
            where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
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
            where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
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
            where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
            => Task.FromResult(this.Decode<TPixel>(configuration, stream));

        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : unmanaged, ImageSharp.PixelFormats.IPixel<TPixel>
        {
            var bmpReadDefines = new BmpReadDefines
            {
                IgnoreFileSize = !this.validate
            };

            var settings = new MagickReadSettings();
            settings.SetDefines(bmpReadDefines);

            using var magickImageCollection = new MagickImageCollection(stream, settings);
            var framesList = new List<ImageFrame<TPixel>>();
            foreach (IMagickImage<ushort> magicFrame in magickImageCollection)
            {
                var frame = new ImageFrame<TPixel>(configuration, magicFrame.Width, magicFrame.Height);
                framesList.Add(frame);

                MemoryGroup<TPixel> framePixels = frame.PixelBuffer.FastMemoryGroup;

                using IUnsafePixelCollection<ushort> pixels = magicFrame.GetPixelsUnsafe();
                if (magicFrame.Depth == 8 || magicFrame.Depth == 6 || magicFrame.Depth == 4 || magicFrame.Depth == 2 || magicFrame.Depth == 1 || magicFrame.Depth == 10 || magicFrame.Depth == 12)
                {
                    byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                    FromRgba32Bytes(configuration, data, framePixels);
                }
                else if (magicFrame.Depth == 16 || magicFrame.Depth == 14)
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

            return new Image<TPixel>(configuration, new ImageMetadata(), framesList);
        }

        public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken);
    }
}
