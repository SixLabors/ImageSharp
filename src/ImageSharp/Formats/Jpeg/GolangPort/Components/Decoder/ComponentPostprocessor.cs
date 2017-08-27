using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    internal class ComponentPostProcessor : IDisposable
    {
        public Size ImageSizeInBlocks { get; }

        public int NumberOfRowGroupScans
        {
            get;
            
        }

        class RowGroupProcessor : IDisposable
        {
            public Buffer2D<float> ColorBuffer { get; }

            public void Dispose()
            {
            }
        }



        public void Dispose()
        {
            
        }
    }
}