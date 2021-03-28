// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder
{
    /// <summary>
    /// On-stack worker struct to efficiently encapsulate the TPixel -> L8 -> Y conversion chain of 8x8 pixel blocks.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type to work on</typeparam>
    internal ref struct LuminanceForwardConverter<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// The Y component
        /// </summary>
        public Block8x8F Y;

        /// <summary>
        /// The converter
        /// </summary>
        private L8ToYConverter converter;

        /// <summary>
        /// Temporal 8x8 block to hold TPixel data
        /// </summary>
        private GenericBlock8x8<TPixel> pixelBlock;

        /// <summary>
        /// Temporal RGB block
        /// </summary>
        private GenericBlock8x8<L8> l8Block;

        public static LuminanceForwardConverter<TPixel> Create()
        {
            var result = default(LuminanceForwardConverter<TPixel>);
            result.converter = L8ToYConverter.Create();
            return result;
        }

        /// <summary>
        /// Converts a 8x8 image area inside 'pixels' at position (x,y) placing the result members of the structure (<see cref="Y"/>)
        /// </summary>
        public void Convert(ImageFrame<TPixel> frame, int x, int y, ref RowOctet<TPixel> currentRows)
        {
            this.pixelBlock.LoadAndStretchEdges(frame.PixelBuffer, x, y, ref currentRows);

            Span<L8> l8Span = this.l8Block.AsSpanUnsafe();
            PixelOperations<TPixel>.Instance.ToL8(frame.GetConfiguration(), this.pixelBlock.AsSpanUnsafe(), l8Span);

            ref Block8x8F yBlock = ref this.Y;

            this.converter.Convert(l8Span, ref yBlock);
        }
    }
}
