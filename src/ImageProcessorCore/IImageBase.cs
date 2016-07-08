using System.Collections.Generic;

namespace ImageProcessorCore
{
    public interface IImageBase<T> : IImageBase
        where T : IPackedVector, new()
    {
        T[] Pixels { get; }
        void ClonePixels(int width, int height, T[] pixels);
        IPixelAccessor<T> Lock();
        void SetPixels(int width, int height, T[] pixels);
    }

    public interface IImageBase
    {
        Rectangle Bounds { get; }
        int FrameDelay { get; set; }
        int Height { get; }
        double PixelRatio { get; }

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
    }
}