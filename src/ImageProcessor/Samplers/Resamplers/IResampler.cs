namespace ImageProcessor.Samplers
{
    /// <summary>
    /// Encasulates an interpolation algorithm for resampling images.
    /// </summary>
    public interface IResampler
    {
        /// <summary>
        /// Gets the radius in which to sample pixels.
        /// </summary>
        double Radius { get; }

        /// <summary>
        /// Gets the result of the interpolation algorithm.
        /// </summary>
        /// <param name="x">The value to process.</param>
        /// <returns>
        /// The <see cref="double"/>
        /// </returns>
        double GetValue(double x);
    }
}
