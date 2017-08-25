namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    internal interface IJpegComponent
    {
        int WidthInBlocks { get; }
        int HeightInBlocks { get; }
    }
}