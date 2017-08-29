using System;
using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.PostProcessing
{
    internal class JpegComponentPostProcessor : IDisposable
    {
        private int currentComponentRowInBlocks;

        private readonly Size blockAreaSize;

        public JpegComponentPostProcessor(JpegImagePostProcessor imagePostProcessor, IJpegComponent component)
        {
            this.Component = component;
            this.ImagePostProcessor = imagePostProcessor;
            this.ColorBuffer = new Buffer2D<float>(imagePostProcessor.PostProcessorBufferSize);

            this.BlockRowsPerStep = JpegImagePostProcessor.BlockRowsPerStep / this.Component.SubSamplingDivisors.Height;
            this.blockAreaSize = this.Component.SubSamplingDivisors * 8;
        }

        public JpegImagePostProcessor ImagePostProcessor { get; }

        public IJpegComponent Component { get; }

        public Buffer2D<float> ColorBuffer { get; }

        public Size SizeInBlocks => this.Component.SizeInBlocks;

        public int BlockRowsPerStep { get; }

        public void Dispose()
        {
            this.ColorBuffer.Dispose();
        }

        public unsafe void CopyBlocksToColorBuffer()
        {
            var blockPp = default(JpegBlockPostProcessor);
            JpegBlockPostProcessor.Init(&blockPp);

            for (int y = 0; y < this.BlockRowsPerStep; y++)
            {
                int yBlock = this.currentComponentRowInBlocks + y;

                if (yBlock >= this.SizeInBlocks.Height)
                {
                    break;
                }

                int yBuffer = y * this.blockAreaSize.Height;

                for (int x = 0; x < this.SizeInBlocks.Width; x++)
                {
                    int xBlock = x;
                    int xBuffer = x * this.blockAreaSize.Width;

                    ref Block8x8 block = ref this.Component.GetBlockReference(xBlock, yBlock);

                    BufferArea<float> destArea = this.ColorBuffer.GetArea(
                        xBuffer,
                        yBuffer,
                        this.blockAreaSize.Width,
                        this.blockAreaSize.Height
                    );

                    blockPp.ProcessBlockColorsInto(this.ImagePostProcessor.RawJpeg, this.Component, ref block, destArea);
                }
            }

            this.currentComponentRowInBlocks += this.BlockRowsPerStep;
        }
    }
}