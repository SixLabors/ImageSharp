namespace ImageSharp.Tests
{
    using System;

    using ImageSharp.PixelFormats;

    public abstract class ImageComparer
    {
        public abstract void Verify<TPixelA, TPixelB>(Image<TPixelA> expected, Image<TPixelB> actual)
            where TPixelA : struct, IPixel<TPixelA> where TPixelB : struct, IPixel<TPixelB>;
    }

    public class ExactComparer : ImageComparer
    {
        public static ExactComparer Instance { get; } = new ExactComparer();

        public override void Verify<TPixelA, TPixelB>(Image<TPixelA> expected, Image<TPixelB> actual)
        {
            throw new NotImplementedException();
        }
    }
}