namespace SixLabors.ImageSharp.Formats.Jpeg.Common.Decoder
{
    /// <summary>
    /// Identifies the colorspace of a Jpeg image
    /// </summary>
    internal enum JpegColorSpace
    {
        Undefined = 0,

        GrayScale,

        Ycck,

        Cmyk,

        RGB,

        YCbCr
    }
}