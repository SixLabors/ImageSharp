namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Various utilities for <see cref="IJpegComponent"/>.
    /// </summary>
    internal static class ComponentUtils
    {
        /// <summary>
        /// Gets a reference to the <see cref="Block8x8"/> at the given row and column index from <see cref="IJpegComponent.SpectralBlocks"/>
        /// </summary>
        public static ref Block8x8 GetBlockReference(this IJpegComponent component, int bx, int by)
        {
            return ref component.SpectralBlocks[bx, by];
        }
    }
}