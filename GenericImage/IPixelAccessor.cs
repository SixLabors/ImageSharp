namespace GenericImage
{
    using System;

    public interface IPixelAccessor<TColor> : IDisposable
    {
        TColor this[int x, int y]
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the width.
        /// </summary>
        int Width { get; }

        /// <summary>
        /// Gets the height.
        /// </summary>
        int Height { get; }
    }
}
