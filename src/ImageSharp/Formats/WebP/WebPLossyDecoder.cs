using System;
using System.IO;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

namespace SixLabors.ImageSharp.Formats.WebP
{
    internal sealed class WebPLossyDecoder
    {
        private readonly Configuration configuration;

        private readonly Stream currentStream;

        private MemoryAllocator memoryAllocator;

        public WebPLossyDecoder(
            Configuration configuration,
            Stream currentStream)
        {
            this.configuration = configuration;
            this.currentStream = currentStream;
            this.memoryAllocator = configuration.MemoryAllocator;
        }

        public void Decode<TPixel>(Buffer2D<TPixel> pixels, int width, int height, int imageDataSize)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: implement decoding. For simulating the decoding: skipping the chunk size bytes.
            this.currentStream.Skip(imageDataSize - 10); // TODO: Not sure why we need to skip 10 bytes less here

            // we need buffers for Y U and V in size of the image
            // TODO: increase size to enable using all prediction blocks? (see https://tools.ietf.org/html/rfc6386#page-9 )
            Buffer2D<YUVPixel> yuvBufferCurrentFrame =
                this.memoryAllocator
                    .Allocate2D<YUVPixel>(width, height);

            // TODO: var predictionBuffer - macro-block-sized with approximation of the portion of the image being reconstructed.
            //  those prediction values are the base, the values from DCT processing are added to that

            // TODO residue signal from DCT: 4x4 blocks of DCT transforms, 16Y, 4U, 4V
            // TODO weiter bei  S.11

            // bit STREAM: See https://tools.ietf.org/html/rfc6386#page-29 ("Frame Header")
            Vp8LBitReader bitReader = new Vp8LBitReader(this.currentStream);
            bool isInterframe = bitReader.ReadBit();
            if (isInterframe)
            {
                throw new NotImplementedException("only key frames supported yet");
            }

            byte version = (byte)((bitReader.ReadBit() ? 2 : 0) | (bitReader.ReadBit() ? 1 : 0));
            (ReconstructionFilter rec, LoopFilter loop) = this.DecodeVersion(version);

            bool isShowFrame = bitReader.ReadBit();

            uint firstPartitionSize = (bitReader.ReadBits(16) << 3) | bitReader.ReadBits(3);
        }

        private (ReconstructionFilter, LoopFilter) DecodeVersion(byte version)
        {
            var rec = ReconstructionFilter.None;
            var loop = LoopFilter.None;

            switch (version)
            {
                case 0:
                    return (ReconstructionFilter.Bicubic, LoopFilter.Normal);
                case 1:
                    return (ReconstructionFilter.Bilinear, LoopFilter.Simple);
                case 2:
                    return (ReconstructionFilter.Bilinear, LoopFilter.None);
                case 3:
                    return (ReconstructionFilter.None, LoopFilter.None);
                default:
                    // https://tools.ietf.org/html/rfc6386#page-30
                    throw new NotSupportedException("reserved for future use in Spec");
            }
        }
    }

    enum ReconstructionFilter
    {
        None,
        Bicubic,
        Bilinear
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
