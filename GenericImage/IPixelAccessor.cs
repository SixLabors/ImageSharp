namespace GenericImage
{
    using System;

    using GenericImage.PackedVectors;

    public interface IPixelAccessor : IDisposable
    {
        IPackedVector this[int x, int y]
        {
            get;
            set;
        }
    }
}
