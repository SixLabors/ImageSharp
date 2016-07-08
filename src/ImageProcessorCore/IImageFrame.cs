namespace ImageProcessorCore
{
    public interface IImageFrame<T> : IImageBase<T>
         where T : IPackedVector, new()
    {
    }
}
