using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Encapsulates postprocessing data for one component for <see cref="JpegImagePostProcessor"/>.
    /// </summary>
    internal class JpegComponentPostProcessor : IDisposable
    {
        /// <summary>
        /// Points to the current row in <see cref="Component"/>.
        /// </summary>
        private int currentComponentRowInBlocks;

        /// <summary>
        /// The size of the area in <see cref="ColorBuffer"/> corrsponding to one 8x8 Jpeg block
        /// </summary>
        private readonly Size blockAreaSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="JpegComponentPostProcessor"/> class.
        /// </summary>
        public JpegComponentPostProcessor(JpegImagePostProcessor imagePostProcessor, IJpegComponent component)
        {
            this.Component = component;
            this.ImagePostProcessor = imagePostProcessor;
            this.ColorBuffer = new Buffer2D<float>(imagePostProcessor.PostProcessorBufferSize);

            this.BlockRowsPerStep = JpegImagePostProcessor.BlockRowsPerStep / this.Component.SubSamplingDivisors.Height;
            this.blockAreaSize = this.Component.SubSamplingDivisors * 8;
        }

        /// <summary>
        /// Gets the <see cref="JpegImagePostProcessor"/>
        /// </summary>
        public JpegImagePostProcessor ImagePostProcessor { get; }

        /// <summary>
        /// Gets the <see cref="Component"/>
        /// </summary>
        public IJpegComponent Component { get; }

        /// <summary>
        /// Gets the temporal working buffer of color values.
        /// </summary>
        public Buffer2D<float> ColorBuffer { get; }

        /// <summary>
        /// Gets <see cref="IJpegComponent.SizeInBlocks"/>
        /// </summary>
        public Size SizeInBlocks => this.Component.SizeInBlocks;

        /// <summary>
        /// Gets the maximal number of block rows being processed in one step.
        /// </summary>
        public int BlockRowsPerStep { get; }

        /// <inheritdoc />
        public void Dispose()
        {
            this.ColorBuffer.Dispose();
        }

        /// <summary>
        /// Invoke <see cref="JpegBlockPostProcessor"/> for <see cref="BlockRowsPerStep"/> block rows, copy the result into <see cref="ColorBuffer"/>.
        /// </summary>
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
                        this.blockAreaSize.Height);

                    blockPp.ProcessBlockColorsInto(this.ImagePostProcessor.RawJpeg, this.Component, ref block, destArea);
                }
            }

            this.currentComponentRowInBlocks += this.BlockRowsPerStep;
        }
    }
}