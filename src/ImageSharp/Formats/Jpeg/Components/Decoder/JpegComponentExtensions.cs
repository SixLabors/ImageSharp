// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Extension methods for <see cref="IJpegComponent"/>
    /// </summary>
    internal static class JpegComponentExtensions
    {
        /// <summary>
        /// Gets a reference to the <see cref="Block8x8"/> at the given row and column index from <see cref="IJpegComponent.SpectralBlocks"/>
        /// </summary>
        /// <param name="component">The <see cref="IJpegComponent"/></param>
        /// <param name="column">The column</param>
        /// <param name="row">The row</param>
        /// <returns>The <see cref="Block8x8"/></returns>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static ref Block8x8 GetBlockReference(this IJpegComponent component, int column, int row)
        {
            return ref component.SpectralBlocks.GetRowSpan(row)[column];
        }

        /// <summary>
        /// Gets a reference to the first item in a block
        /// at the given row and column index from <see cref="IJpegComponent.SpectralBlocks"/>
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public static ref short GetBlockDataReference(this IJpegComponent component, int column, int row)
        {
            ref Block8x8 blockRef = ref component.GetBlockReference(column, row);
            return ref Unsafe.As<Block8x8, short>(ref blockRef);
        }
    }
}