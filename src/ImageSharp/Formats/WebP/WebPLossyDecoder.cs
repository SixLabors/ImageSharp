// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal sealed class WebPLossyDecoder : WebPDecoderBase
    {
        private readonly Configuration configuration;

        private readonly Stream currentStream;

        private readonly MemoryAllocator memoryAllocator;

        public WebPLossyDecoder(Configuration configuration, Stream currentStream)
        {
            this.configuration = configuration;
            this.currentStream = currentStream;
            this.memoryAllocator = configuration.MemoryAllocator;
        }

        public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height, uint imageDataSize, int vp8Version)
            where TPixel : struct, IPixel<TPixel>
        {
            // we need buffers for Y U and V in size of the image
            // TODO: increase size to enable using all prediction blocks? (see https://tools.ietf.org/html/rfc6386#page-9 )
            Buffer2D<YUVPixel> yuvBufferCurrentFrame = this.memoryAllocator.Allocate2D<YUVPixel>(width, height);

            // TODO: var predictionBuffer - macro-block-sized with approximation of the portion of the image being reconstructed.
            //  those prediction values are the base, the values from DCT processing are added to that

            // TODO residue signal from DCT: 4x4 blocks of DCT transforms, 16Y, 4U, 4V
            var bitReader = new Vp8BitReader(this.currentStream, imageDataSize, this.memoryAllocator);
            Vp8Profile vp8Profile = this.DecodeProfile(vp8Version);
        }

        private Vp8Profile DecodeProfile(int version)
        {
            switch (version)
            {
                case 0:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.Bicubic, LoopFilter = LoopFilter.Normal };
                case 1:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.Bilinear, LoopFilter = LoopFilter.Simple };
                case 2:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.Bilinear, LoopFilter = LoopFilter.None };
                case 3:
                    return new Vp8Profile { ReconstructionFilter = ReconstructionFilter.None, LoopFilter = LoopFilter.None };
                default:
                    // Reserved for future use in Spec.
                    // https://tools.ietf.org/html/rfc6386#page-30
                    WebPThrowHelper.ThrowNotSupportedException($"unsupported VP8 version {version} found");
                    return new Vp8Profile();
            }
        }
    }

    enum LoopFilter
    {
        Normal,
        Simple,
        None
    }

    struct YUVPixel
    {
        public byte Y { get; }

        public byte U { get; }

        public byte V { get; }
    }
}
