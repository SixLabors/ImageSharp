// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ImageMagick.Formats.Bmp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Memory;
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

        public MagickReferenceDecoder(bool validate)
        {
            this.validate = validate;
        }

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

            using var magickImage = new MagickImage(stream, settings);
            var result = new Image<TPixel>(configuration, magickImage.Width, magickImage.Height);
            MemoryGroup<TPixel> resultPixels = result.GetRootFramePixelBuffer().FastMemoryGroup;

            using (IUnsafePixelCollection<ushort> pixels = magickImage.GetPixelsUnsafe())
            {
                if (magickImage.Depth == 8)
                {
                    byte[] data = pixels.ToByteArray(PixelMapping.RGBA);

                    FromRgba32Bytes(configuration, data, resultPixels);
                }
                else if (magickImage.Depth == 16)
                {
                    ushort[] data = pixels.ToShortArray(PixelMapping.RGBA);
                    Span<byte> bytes = MemoryMarshal.Cast<ushort, byte>(data.AsSpan());
                    FromRgba64Bytes(configuration, bytes, resultPixels);
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }

            return result;
        }

        public Image Decode(Configuration configuration, Stream stream) => this.Decode<Rgba32>(configuration, stream);

        public async Task<Image> DecodeAsync(Configuration configuration, Stream stream, CancellationToken cancellationToken)
            => await this.DecodeAsync<Rgba32>(configuration, stream, cancellationToken);
    }
}
