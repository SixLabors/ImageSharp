namespace GenericImage
{
    using GenericImage.PackedVectors;

    public interface IImageBase<TPacked>
        where TPacked : IPackedVector
    {
        TPacked[] Pixels { get; }

        int Width { get; }

        int Height { get; }

        IPixelAccessor Lock();
    }
}
