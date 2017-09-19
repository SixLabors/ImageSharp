namespace SixLabors.ImageSharp.Benchmarks.Image.Jpeg
{
    using System;

    using SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder;
    using SixLabors.ImageSharp.Memory;

    using JpegColorConverter = SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder.ColorConverters.JpegColorConverter;

    public class YCbCrColorConversion
    {
        private static JpegColorConverter.ComponentValues CreateRandomValues(
            int componentCount,
            int inputBufferLength,
            float minVal = 0f,
            float maxVal = 255f)
        {
            var rnd = new Random(42);
            Buffer2D<float>[] buffers = new Buffer2D<float>[componentCount];
            for (int i = 0; i < componentCount; i++)
            {
                float[] values = new float[inputBufferLength];

                for (int j = 0; j < inputBufferLength; j++)
                {
                    values[j] = (float)rnd.NextDouble() * (maxVal - minVal) + minVal;
                }

                // no need to dispose when buffer is not array owner
                buffers[i] = new Buffer2D<float>(values, values.Length, 1);
            }
            return new JpegColorConverter.ComponentValues(buffers, 0);
        }

    }
}