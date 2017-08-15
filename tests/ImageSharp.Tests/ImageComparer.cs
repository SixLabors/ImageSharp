namespace ImageSharp.Tests
{
    using ImageSharp.PixelFormats;

    public abstract class ImageComparer
    {
        public abstract void Verify<TPixelA, TPixelB>(Image<TPixelA> expected, Image<TPixelB> actual)
            where TPixelA : struct, IPixel<TPixelA> where TPixelB : struct, IPixel<TPixelB>;
    }
}