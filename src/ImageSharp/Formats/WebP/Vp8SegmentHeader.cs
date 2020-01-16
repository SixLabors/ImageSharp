// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Segment features.
    /// </summary>
    internal class Vp8SegmentHeader
    {
        public bool UseSegment { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to update the segment map or not.
        /// </summary>
        public bool UpdateMap { get; set; }

        /// <summary>
        /// Gets or sets the absolute or delta values for quantizer and filter.
        /// </summary>
        public int AbsoluteOrDelta { get; set; }

        /// <summary>
        /// Gets or sets quantization changes.
        /// </summary>
        public byte[] Quantizer { get; set; }

        /// <summary>
        /// Gets or sets the filter strength for segments.
        /// </summary>
        public byte[] FilterStrength { get; set; }
    }
}
