namespace ImageProcessor.Encoders
{
    public interface IImageFormat
    {
        IImageEncoder Encoder { get; }

        IImageDecoder Decoder { get; }
    }
}
