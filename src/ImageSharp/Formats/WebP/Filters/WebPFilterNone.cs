// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Formats.WebP.Filters
{
    // TODO: check if this is a filter or just a placeholder from the C implementation details
    class WebPFilterNone : WebPFilterBase
    {
        public override void Unfilter(
            Span<byte> prevLine,
            int? prevLineOffset,
            Span<byte> input,
            int inputOffset,
            Span<byte> output,
            int outputOffset,
            int width)
        {
        }

        public override void Filter(
            Span<byte> input,
            int inputOffset,
            int width,
            int height,
            int stride,
            Span<byte> output,
            int outputOffset)
        {
        }
    }
}
