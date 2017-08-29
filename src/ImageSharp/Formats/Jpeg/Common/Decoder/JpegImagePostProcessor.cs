using System;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.YCbCrColorSapce;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    internal class JpegImagePostProcessor : IDisposable
    {
        public const int BlockRowsPerStep = 4;

        public const int PixelRowsPerStep = 4 * 8;

        public JpegImagePostProcessor(IRawJpegData rawJpeg)
        {
            this.RawJpeg = rawJpeg;
            IJpegComponent c0 = rawJpeg.Components.First();
            this.NumberOfPostProcessorSteps = c0.SizeInBlocks.Height / BlockRowsPerStep;
            this.PostProcessorBufferSize = new Size(c0.SizeInBlocks.Width * 8, PixelRowsPerStep);

            this.ComponentProcessors = rawJpeg.Components.Select(c => new JpegComponentPostProcessor(this, c)).ToArray();
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
        }

        public bool DoPostProcessorStep<TPixel>(Image<TPixel> destination)
            where TPixel : struct, IPixel<TPixel>
        {
            if (this.RawJpeg.ComponentCount != 3)
            {
                throw new NotImplementedException();
            }

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
            while (this.DoPostProcessorStep(destination))
            {
            }
        }

        private void ConvertColors<TPixel>(Image<TPixel> destination)
            where TPixel : struct, IPixel<TPixel>
        {
            int maxY = Math.Min(destination.Height, this.CurrentImageRowInPixels + PixelRowsPerStep);

            JpegComponentPostProcessor[] cp = this.ComponentProcessors;

            YCbCrAndRgbConverter converter = new YCbCrAndRgbConverter(); 

            Vector4 rgbaVector = new Vector4(0, 0, 0, 1);

            for (int yy = this.CurrentImageRowInPixels; yy < maxY; yy++)
            {
                int y = yy - this.CurrentImageRowInPixels;

                Span<TPixel> destRow = destination.GetRowSpan(yy);

                for (int x = 0; x < destination.Width; x++)
                {
                    float colY = cp[0].ColorBuffer[x, y];
                    float colCb = cp[1].ColorBuffer[x, y];
                    float colCr = cp[2].ColorBuffer[x, y];

                    YCbCr yCbCr = new YCbCr(colY, colCb, colCr);
                    Rgb rgb = converter.Convert(yCbCr);

                    Unsafe.As<Vector4, Vector3>(ref rgbaVector) = rgb.Vector;

                    destRow[x].PackFromVector4(rgbaVector);
                }
            }
        }
    }
}