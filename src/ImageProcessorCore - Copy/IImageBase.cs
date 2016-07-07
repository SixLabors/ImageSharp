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

        /// <summary>
        /// Gets or sets the maximum allowable width in pixels.
        /// </summary>
        int MaxWidth { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowable height in pixels.
        /// </summary>
        int MaxHeight { get; set; }

        int Width { get; }

        void ClonePixels(int width, int height, TPackedVector[] pixels);
        IPixelAccessor Lock();
        void SetPixels(int width, int height, TPackedVector[] pixels);
    }
}