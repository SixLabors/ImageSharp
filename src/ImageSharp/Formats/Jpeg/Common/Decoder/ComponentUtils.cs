namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Various utilities for <see cref="IJpegComponent"/>.
    /// </summary>
    internal static class ComponentUtils
    {
        public static ref Block8x8 GetBlockReference(this IJpegComponent component, int bx, int by)
        {
            return ref component.SpectralBlocks[bx, by];
        }
    }
}