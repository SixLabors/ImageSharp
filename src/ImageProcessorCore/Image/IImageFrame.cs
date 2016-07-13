namespace ImageProcessorCore
{
    public interface IImageFrame<T,TP> : IImageBase<T,TP>
        where T : IPackedVector<T, TP>, new()
        where TP : struct
    {
    }
}
