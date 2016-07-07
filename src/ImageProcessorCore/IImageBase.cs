using System.Collections.Generic;

namespace ImageProcessorCore
{
    public interface IImageBase<TPackedVector> : IImageBase
        where TPackedVector : IPackedVector, new()
    {
        TPackedVector[] Pixels { get; }
        void ClonePixels(int width, int height, IEnumerable<TPackedVector> pixels);
        IPixelAccessor<TPackedVector> Lock();
        void SetPixels(int width, int height, TPackedVector[] pixels);
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