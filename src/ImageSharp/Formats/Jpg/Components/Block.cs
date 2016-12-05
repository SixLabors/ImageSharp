// <copyright file="Block.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Buffers;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents an 8x8 block of coefficients to transform and encode.
    /// </summary>
    internal struct Block : IDisposable
    {
        /// <summary>
        /// Gets the size of the block.
        /// </summary>
        public const int BlockSize = 64;

        /// <summary>
        /// Gets the array of block data.
        /// </summary>
        public int[] Data;

        /// <summary>
        /// A pool of reusable buffers.
        /// </summary>
        private static readonly ArrayPool<int> ArrayPool = ArrayPool<int>.Create(BlockSize, 50);

        /// <summary>
        /// Gets a value indicating whether the block is initialized
        /// </summary>
        public bool IsInitialized => this.Data != null;

        /// <summary>
        /// Gets the pixel data at the given block index.
        /// </summary>
        /// <param name="index">The index of the data to return.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public int this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.Data[index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.Data[index] = value;
            }
        }

        /// <summary>
        /// Creates a new block
        /// </summary>
        /// <returns>The <see cref="Block"/></returns>
        public static Block Create()
        {
            Block block = default(Block);
            block.Init();
            return block;
        }

        /// <summary>
        /// Returns an array of blocks of the given length.
        /// </summary>
        /// <param name="count">The number to create.</param>
        /// <returns>The <see cref="T:Block[]"/></returns>
        public static Block[] CreateArray(int count)
        {
            Block[] result = new Block[count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i].Init();
            }

            return result;
        }

        /// <summary>
        /// Disposes of the collection of blocks
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        public static void DisposeAll(Block[] blocks)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].Dispose();
            }
        }

        /// <summary>
        /// Initializes the new block.
        /// </summary>
        public void Init()
        {
            this.Data = ArrayPool.Rent(BlockSize);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // TODO: Refactor Block.Dispose() callers to always use 'using' or 'finally' statement!
            if (this.Data != null)
            {
                ArrayPool.Return(this.Data, true);
                this.Data = null;
            }
        }

        /// <summary>
        /// Clears the block data
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < this.Data.Length; i++)
            {
                this.Data[i] = 0;
            }
        }

        /// <summary>
        /// Clones the current block
        /// </summary>
        /// <returns>The <see cref="Block"/></returns>
        public Block Clone()
        {
            Block clone = Create();
            Array.Copy(this.Data, clone.Data, BlockSize);
            return clone;
        }
    }

    /// <summary>
    /// TODO: Should be removed, when JpegEncoderCore is refactored to use Block8x8F
    /// Temporal class to make refactoring easier.
    /// 1. Refactor Block -> BlockF
    /// 2. Test
    /// 3. Refactor BlockF -> Block8x8F
    /// </summary>
    internal struct BlockF : IDisposable
    {
        /// <summary>
        /// Size of the block.
        /// </summary>
        public const int BlockSize = 64;

        /// <summary>
        /// The array of block data.
        /// </summary>
        public float[] Data;

        /// <summary>
        /// A pool of reusable buffers.
        /// </summary>
        private static readonly ArrayPool<float> ArrayPool = ArrayPool<float>.Create(BlockSize, 50);

        /// <summary>
        /// Gets a value indicating whether the block is initialized
        /// </summary>
        public bool IsInitialized => this.Data != null;

        /// <summary>
        /// Gets the pixel data at the given block index.
        /// </summary>
        /// <param name="index">The index of the data to return.</param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public float this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return this.Data[index];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                this.Data[index] = value;
            }
        }

        /// <summary>
        /// Creates a new block
        /// </summary>
        /// <returns>The <see cref="BlockF"/></returns>
        public static BlockF Create()
        {
            var block = default(BlockF);
            block.Init();
            return block;
        }

        /// <summary>
        /// Returns an array of blocks of the given length.
        /// </summary>
        /// <param name="count">The number to create.</param>
        /// <returns>The <see cref="T:BlockF[]"/></returns>
        public static BlockF[] CreateArray(int count)
        {
            BlockF[] result = new BlockF[count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i].Init();
            }

            return result;
        }

        /// <summary>
        /// Disposes of the collection of blocks
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        public static void DisposeAll(BlockF[] blocks)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].Dispose();
            }
        }

        /// <summary>
        /// Clears the block data
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < this.Data.Length; i++)
            {
                this.Data[i] = 0;
            }
        }

        /// <summary>
        /// Clones the current block
        /// </summary>
        /// <returns>The <see cref="Block"/></returns>
        public BlockF Clone()
        {
            BlockF clone = Create();
            Array.Copy(this.Data, clone.Data, BlockSize);
            return clone;
        }

        /// <summary>
        /// Initializes the new block.
        /// </summary>
        public void Init()
        {
            // this.Data = new int[BlockSize];
            this.Data = ArrayPool.Rent(BlockSize);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // TODO: Refactor Block.Dispose() callers to always use 'using' or 'finally' statement!
            if (this.Data != null)
            {
                ArrayPool.Return(this.Data, true);
                this.Data = null;
            }
        }
    }
}