using System.Collections.Generic;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    internal interface IRawJpegData
    {
        Size ImageSizeInPixels { get; }

        int ComponentCount { get; }

        IEnumerable<IJpegComponent> Components { get; }

        /// <summary>
        /// Gets the quantization tables, in zigzag order.
        /// </summary>
        Block8x8F[] QuantizationTables { get; }
    }
}