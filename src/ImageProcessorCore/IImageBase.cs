namespace ImageProcessorCore
{
    public interface IImageBase<TPacked>
        where TPacked : IPackedVector
    {
        Rectangle Bounds { get; }
        int FrameDelay { get; set; }
        int Height { get; }
        double PixelRatio { get; }
        TPacked[] Pixels { get; }
        int Quality { get; set; }
        int Width { get; }

        void ClonePixels(int width, int height, TPacked[] pixels);
        IPixelAccessor Lock();
        void SetPixels(int width, int height, TPacked[] pixels);
    }
}