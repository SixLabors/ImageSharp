// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Collections.Generic;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Processing.Processors.Quantization
{
    /// <summary>
    /// A pixel sampling strategy that enumerates all pixels.
    /// </summary>
    public class ExtensivePixelSamplingStrategy : IPixelSamplingStrategy
    {
        /// <inheritdoc />
        public IEnumerable<Buffer2DRegion<TPixel>> EnumeratePixelRegions<TPixel>(Image<TPixel> image)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            foreach (ImageFrame<TPixel> frame in image.Frames)
            {
                yield return frame.PixelBuffer.GetRegion();
            }
        }
    }
}
