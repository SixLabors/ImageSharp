// <copyright file="DecodedBlockMemento.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System.Buffers;

    internal struct DecodedBlockMemento
    {
        /// <summary>
        /// The <see cref="ArrayPool{T}"/> used to pool data in <see cref="JpegDecoderCore.DecodedBlocks"/>.
        /// Should always clean arrays when returning!
        /// </summary>
        public static readonly ArrayPool<DecodedBlockMemento> ArrayPool = ArrayPool<DecodedBlockMemento>.Create();

        public int Bx;

        public int By;

        public Block8x8F Block;

        public static void Store(DecodedBlockMemento[] blockArray, int index, int bx, int by, ref Block8x8F block)
        {
            blockArray[index].Bx = bx;
            blockArray[index].By = by;
            blockArray[index].Block = block;
        }
    }
}