namespace ImageSharp.Formats.Jpeg.Port.Components
{
    using ImageSharp.Memory;

    /// <summary>
    /// Defines a pair of huffman tables
    /// </summary>
    internal class HuffmanTables
    {
        /// <summary>
        /// Gets or sets the quantization tables.
        /// </summary>
        public Fast2DArray<short> Tables { get; set; } = new Fast2DArray<short>(256, 2);
    }
}
