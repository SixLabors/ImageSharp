namespace ImageProcessorCore
{
    public interface IImageBase<TPackedVector>
        where TPackedVector : IPackedVector
    {
        Rectangle Bounds { get; }
        int FrameDelay { get; set; }
        int Height { get; }
        double PixelRatio { get; }
        TPackedVector[] Pixels { get; }
        int Quality { get; set; }
        int Width { get; }

        void ClonePixels(int width, int height, TPackedVector[] pixels);
        IPixelAccessor Lock();
        void SetPixels(int width, int height, TPackedVector[] pixels);
    }
}