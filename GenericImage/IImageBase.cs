namespace GenericImage
{
    using GenericImage.PackedVectors;

    public interface IImageBase<TColor, TDepth>
        where TColor : IColor<TDepth>
        where TDepth : struct
    {
        TColor[] Pixels { get; }

        int Width { get; }

        int Height { get; }

        IPixelAccessor<TColor> Lock();
    }
}
