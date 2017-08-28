using System.Collections.Generic;
using SixLabors.Primitives;

namespace SixLabors.ImageSharp.Formats.Jpeg.Common
{
    internal interface IRawJpegData
    {
        Size ImageSize { get; }

        Size ImageSizeInBlocks { get; }

        int ComponentCount { get; }

        IEnumerable<IJpegComponent> Components { get; }
    }
}