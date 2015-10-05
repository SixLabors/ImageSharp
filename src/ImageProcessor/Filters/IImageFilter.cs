namespace ImageProcessor.Filters
{
    /// <summary>
    /// Image processing filter interface.
    /// </summary>
    /// <remarks>
    /// The interface defines the set of methods, which should be
    /// provided by all image processing filters. Methods of this interface
    /// manipulate the original image.
    /// </remarks>
    public interface IImageFilter
    {
        /// <summary>
        /// Apply filter to an image at the area of the specified rectangle.
        /// </summary>
        /// <param name="target">Target image to apply filter to.</param>
        /// <param name="source">The source image. Cannot be null.</param>
        /// <param name="rectangle">The rectangle, which defines the area of the
        /// image where the filter should be applied to.</param>
        /// <remarks>The method keeps the source image unchanged and returns the
        /// the result of image processing filter as new image.</remarks>
        /// <exception cref="System.ArgumentNullException">
        ///     <paramref name="target"/>
        ///     is null.
        ///     - or -
        ///     <paramref name="source"/>
        ///     is null.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// <paramref name="rectangle"/> doesnt fit the dimension of the image.
        /// </exception>
        void Apply(ImageBase target, ImageBase source, Rectangle rectangle);
    }
}
