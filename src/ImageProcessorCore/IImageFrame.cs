namespace ImageProcessorCore
{
    public interface IImageFrame<TPacked> : IImageBase<TPacked>
         where TPacked : IPackedVector
    {
    }
}
