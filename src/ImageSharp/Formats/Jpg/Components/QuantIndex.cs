namespace ImageSharp.Formats.Jpg.Components
{
    /// <summary>
    /// Enumerates the quantization tables
    /// </summary>
    internal enum QuantIndex
    {
        /// <summary>
        /// The luminance quantization table index
        /// </summary>
        Luminance = 0,

        /// <summary>
        /// The chrominance quantization table index
        /// </summary>
        Chrominance = 1,
    }
}