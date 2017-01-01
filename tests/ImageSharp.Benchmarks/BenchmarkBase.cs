namespace ImageSharp.Benchmarks
{
    using ImageSharp.Formats;

    /// <summary>
    /// The image benchmark base class.
    /// </summary>
    public abstract class BenchmarkBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BenchmarkBase"/> class.
        /// </summary>
        protected BenchmarkBase()
        {
            // Add Image Formats
            Configuration.Default.AddImageFormat(new JpegFormat());
            Configuration.Default.AddImageFormat(new PngFormat());
            Configuration.Default.AddImageFormat(new BmpFormat());
            Configuration.Default.AddImageFormat(new GifFormat());
        }
    }
}
