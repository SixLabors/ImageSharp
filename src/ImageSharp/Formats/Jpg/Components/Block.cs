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
        private static readonly ArrayPool<int> ArrayPool = ArrayPool<int>.Create(BlockSize, 50);

        /// <summary>
        /// Gets the size of the block.
        /// </summary>
        public const int BlockSize = 64;

        /// <summary>
        /// The array of block data.
        /// </summary>
        public int[] Data;

        public void Init()
        {
            // this.Data = new int[BlockSize];
            this.Data = ArrayPool.Rent(BlockSize);
        }

        public static Block Create()
        {
            var block = new Block();
            block.Init();
            return block;
        }

        public static Block[] CreateArray(int size)
        {
            Block[] result = new Block[size];
            for (int i = 0; i < result.Length; i++)
            {
                result[i].Init();
            }

            return result;
        }

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

        // TODO: Refactor Block.Dispose() callers to always use 'using' or 'finally' statement!
        public void Dispose()
        {
            if (this.Data != null)
            {
                ArrayPool.Return(this.Data, true);
                this.Data = null;
            }
        }

        public static void DisposeAll(Block[] blocks)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].Dispose();
            }
        }

        public void Clear()
        {
            for (int i = 0; i < this.Data.Length; i++)
            {
                this.Data[i] = 0;
            }
        }

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
        private static readonly ArrayPool<float> ArrayPool = ArrayPool<float>.Create(BlockSize, 50);

        /// <summary>
        /// Size of the block.
        /// </summary>
        public const int BlockSize = 64;

        /// <summary>
        /// The array of block data.
        /// </summary>
        public float[] Data;

        public void Init()
        {
            // this.Data = new int[BlockSize];
            this.Data = ArrayPool.Rent(BlockSize);
        }

        public static BlockF Create()
        {
            var block = new BlockF();
            block.Init();
            return block;
        }

        public static BlockF[] CreateArray(int size)
        {
            BlockF[] result = new BlockF[size];
            for (int i = 0; i < result.Length; i++)
            {
                result[i].Init();
            }

            return result;
        }

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

        // TODO: Refactor Block.Dispose() callers to always use 'using' or 'finally' statement!
        public void Dispose()
        {
            if (this.Data != null)
            {
                ArrayPool.Return(this.Data, true);
                this.Data = null;
            }
        }

        public static void DisposeAll(BlockF[] blocks)
        {
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i].Dispose();
            }
        }

        public void Clear()
        {
            for (int i = 0; i < this.Data.Length; i++)
            {
                this.Data[i] = 0;
            }
        }

        public BlockF Clone()
        {
            BlockF clone = Create();
            Array.Copy(this.Data, clone.Data, BlockSize);
            return clone;
        }
    }
}