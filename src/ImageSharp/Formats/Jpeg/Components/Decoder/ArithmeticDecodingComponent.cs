// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    internal class ArithmeticDecodingComponent : JpegComponent
    {
        public ArithmeticDecodingComponent(MemoryAllocator memoryAllocator, JpegFrame frame, byte id, int horizontalFactor, int verticalFactor, byte quantizationTableIndex, int index)
            : base(memoryAllocator, frame, id, horizontalFactor, verticalFactor, quantizationTableIndex, index)
        {
        }

        /// <summary>
        /// Gets or sets the dc context.
        /// </summary>
        public int DcContext { get; set; }

        /// <summary>
        /// Gets or sets the dc statistics.
        /// </summary>
        public ArithmeticStatistics DcStatistics { get; set; }

        /// <summary>
        /// Gets or sets the ac statistics.
        /// </summary>
        public ArithmeticStatistics AcStatistics { get; set; }
    }
}
