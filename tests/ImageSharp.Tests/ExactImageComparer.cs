namespace ImageSharp.Tests
{
    using System;
    using System.Collections.Generic;

    using ImageSharp.PixelFormats;

    using SixLabors.Primitives;

    public class ExactImageComparer : ImageComparer
    {
        public static ExactImageComparer Instance { get; } = new ExactImageComparer();

        public override void Verify<TPixelA, TPixelB>(Image<TPixelA> expected, Image<TPixelB> actual)
        {
            if (expected.Size() != actual.Size())
            {
                throw new ImageDimensionsMismatchException(expected.Size(), actual.Size());
            }

            int width = actual.Width;

            // TODO: Comparing through Rgba32 is not robust enough because of the existance of super high precision pixel types.

            Rgba32[] aBuffer = new Rgba32[width];
            Rgba32[] bBuffer = new Rgba32[width];

            var differences = new List<Point>();

            for (int y = 0; y < actual.Height; y++)
            {
                Span<TPixelA> aSpan = expected.GetRowSpan(y);
                Span<TPixelB> bSpan = actual.GetRowSpan(y);

                PixelOperations<TPixelA>.Instance.ToRgba32(aSpan, aBuffer, width);
                PixelOperations<TPixelB>.Instance.ToRgba32(bSpan, bBuffer, width);

                for (int x = 0; x < width; x++)
                {
                    if (aBuffer[x] != bBuffer[x])
                    {
                        differences.Add(new Point(x, y));
                    }
                }
            }

            if (differences.Count > 0)
            {
                throw new ImagesAreNotEqualException(differences.ToArray());
            }
        }
    }
}