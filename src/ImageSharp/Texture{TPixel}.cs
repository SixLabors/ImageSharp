using System;
using System.Collections.Generic;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Texture
    /// </summary>
    /// <typeparam name="TPixel">The pixel format.</typeparam>
    public sealed class Texture<TPixel> : Texture
        where TPixel : struct, IPixel<TPixel>
    {
    }
}
