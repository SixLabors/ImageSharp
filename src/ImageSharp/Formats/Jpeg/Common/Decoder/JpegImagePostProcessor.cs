using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.YCbCrColorSapce;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    internal class JpegImagePostProcessor : IDisposable
    {
        public const int BlockRowsPerStep = 4;

        public const int PixelRowsPerStep = 4 * 8;

        private readonly Buffer<Vector4> rgbaBuffer;

        private JpegColorConverter colorConverter;

        public JpegImagePostProcessor(IRawJpegData rawJpeg)
        {
            this.RawJpeg = rawJpeg;
            IJpegComponent c0 = rawJpeg.Components.First();
            this.NumberOfPostProcessorSteps = c0.SizeInBlocks.Height / BlockRowsPerStep;
            this.PostProcessorBufferSize = new Size(c0.SizeInBlocks.Width * 8, PixelRowsPerStep);

            this.ComponentProcessors = rawJpeg.Components.Select(c => new JpegComponentPostProcessor(this, c)).ToArray();
            this.rgbaBuffer = new Buffer<Vector4>(rawJpeg.ImageSizeInPixels.Width);
            this.colorConverter = JpegColorConverter.GetConverter(rawJpeg.ColorSpace);
        }

        public JpegComponentPostProcessor[] ComponentProcessors { get; }

        public IRawJpegData RawJpeg { get; }

        public int NumberOfPostProcessorSteps { get; }

        public Size PostProcessorBufferSize { get; }

        public int CurrentImageRowInPixels { get; private set; }

        public void Dispose()
        {
            foreach (JpegComponentPostProcessor cpp in this.ComponentProcessors)
            {
                cpp.Dispose();
            }

            this.rgbaBuffer.Dispose();
        }

        public bool DoPostProcessorStep<TPixel>(Image<TPixel> destination)
            where TPixel : struct, IPixel<TPixel>
        {
            foreach (JpegComponentPostProcessor cpp in this.ComponentProcessors)
            {
                cpp.CopyBlocksToColorBuffer();
            }

            this.ConvertColors(destination);

            this.CurrentImageRowInPixels += PixelRowsPerStep;
            return this.CurrentImageRowInPixels < this.RawJpeg.ImageSizeInPixels.Height;
        }

        public void PostProcess<TPixel>(Image<TPixel> destination)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.RawJpeg.ImageSizeInPixels != destination.Size())
            {
                throw new ArgumentException("Input image is not of the size of the processed one!");
            }

            while (this.DoPostProcessorStep(destination))
            {
            }
        }

        private void ConvertColors<TPixel>(Image<TPixel> destination)
            where TPixel : struct, IPixel<TPixel>
        {
            int maxY = Math.Min(destination.Height, this.CurrentImageRowInPixels + PixelRowsPerStep);

            Buffer2D<float>[] buffers = this.ComponentProcessors.Select(cp => cp.ColorBuffer).ToArray();

            for (int yy = this.CurrentImageRowInPixels; yy < maxY; yy++)
            {
                int y = yy - this.CurrentImageRowInPixels;

                var values = new JpegColorConverter.ComponentValues(buffers, y);
                this.colorConverter.ConvertToRGBA(values, this.rgbaBuffer);

                Span<TPixel> destRow = destination.GetRowSpan(yy);

                PixelOperations<TPixel>.Instance.PackFromVector4(this.rgbaBuffer, destRow, destination.Width);
            }
        }
    }
}