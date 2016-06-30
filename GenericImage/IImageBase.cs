namespace GenericImage
{
    public interface IImageBase<T>
        where T : struct
    {
        T[] Pixels { get; }

        int Width { get; }

        int Height { get; }

        IPixelAccessor Lock();
    }
}
