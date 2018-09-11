// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using SixLabors.ImageSharp.Memory;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal partial struct Block8x8F
    {
        /// <summary>
        /// Copy block data into the destination color buffer pixel area with the provided horizontal and vertical.
        /// </summary>
        public void CopyTo(in BufferArea<float> area, int horizontalScale, int verticalScale)
        {
            if (horizontalScale == 1 && verticalScale == 1)
            {
                this.CopyTo(area);
                return;
            }
            else if (horizontalScale == 2 && verticalScale == 2)
            {
                this.CopyTo2x2(area);
                return;
            }

            ref float destBase = ref area.GetReferenceToOrigin();

            // TODO: Optimize: implement all the cases with loopless special code! (T4?)
            for (int y = 0; y < 8; y++)
            {
                int yy = y * verticalScale;
                int y8 = y * 8;

                for (int x = 0; x < 8; x++)
                {
                    int xx = x * horizontalScale;

                    float value = this[y8 + x];

                    for (int i = 0; i < verticalScale; i++)
                    {
                        int baseIdx = ((yy + i) * area.Stride) + xx;

                        for (int j = 0; j < horizontalScale; j++)
                        {
                            // area[xx + j, yy + i] = value;
                            Unsafe.Add(ref destBase, baseIdx + j) = value;
                        }
                    }
                }
            }
        }

        // [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(in BufferArea<float> area)
        {
            ref byte selfBase = ref Unsafe.As<Block8x8F, byte>(ref this);
            ref byte destBase = ref Unsafe.As<float, byte>(ref area.GetReferenceToOrigin());
            int destStride = area.Stride * sizeof(float);

            CopyRowImpl(ref selfBase, ref destBase, destStride, 0);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 1);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 2);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 3);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 4);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 5);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 6);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyRowImpl(ref byte selfBase, ref byte destBase, int destStride, int row)
        {
            ref byte s = ref Unsafe.Add(ref selfBase, row * 8 * sizeof(float));
            ref byte d = ref Unsafe.Add(ref destBase, row * destStride);
            Unsafe.CopyBlock(ref d, ref s, 8 * sizeof(float));
        }

        private void CopyTo2x2(in BufferArea<float> area)
        {
            ref float destBase = ref area.GetReferenceToOrigin();
            int destStride = area.Stride;

            this.WidenCopyImpl2x2(ref destBase, 0, destStride);
            this.WidenCopyImpl2x2(ref destBase, 1, destStride);
            this.WidenCopyImpl2x2(ref destBase, 2, destStride);
            this.WidenCopyImpl2x2(ref destBase, 3, destStride);
            this.WidenCopyImpl2x2(ref destBase, 4, destStride);
            this.WidenCopyImpl2x2(ref destBase, 5, destStride);
            this.WidenCopyImpl2x2(ref destBase, 6, destStride);
            this.WidenCopyImpl2x2(ref destBase, 7, destStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WidenCopyImpl2x2(ref float destBase, int row, int destStride)
        {
            ref Vector4 selfLeft = ref Unsafe.Add(ref this.V0L, 2 * row);
            ref Vector4 selfRight = ref Unsafe.Add(ref selfLeft, 1);
            ref float destLocalOrigo = ref Unsafe.Add(ref destBase, row * 2 * destStride);

            Unsafe.Add(ref destLocalOrigo, 0) = selfLeft.X;
            Unsafe.Add(ref destLocalOrigo, 1) = selfLeft.X;
            Unsafe.Add(ref destLocalOrigo, 2) = selfLeft.Y;
            Unsafe.Add(ref destLocalOrigo, 3) = selfLeft.Y;
            Unsafe.Add(ref destLocalOrigo, 4) = selfLeft.Z;
            Unsafe.Add(ref destLocalOrigo, 5) = selfLeft.Z;
            Unsafe.Add(ref destLocalOrigo, 6) = selfLeft.W;
            Unsafe.Add(ref destLocalOrigo, 7) = selfLeft.W;

            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 0) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 1) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 2) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 3) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 4) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 5) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 6) = selfRight.W;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 7) = selfRight.W;

            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 0) = selfLeft.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 1) = selfLeft.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 2) = selfLeft.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 3) = selfLeft.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 4) = selfLeft.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 5) = selfLeft.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 6) = selfLeft.W;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 7) = selfLeft.W;

            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 0) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 1) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 2) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 3) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 4) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 5) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 6) = selfRight.W;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 7) = selfRight.W;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WidenCopyImpl(ref Vector4 s, ref float destBase)
        {
            Unsafe.Add(ref destBase, 0) = s.X;
            Unsafe.Add(ref destBase, 1) = s.X;
            Unsafe.Add(ref destBase, 2) = s.Y;
            Unsafe.Add(ref destBase, 3) = s.Y;
            Unsafe.Add(ref destBase, 4) = s.Z;
            Unsafe.Add(ref destBase, 5) = s.Z;
            Unsafe.Add(ref destBase, 6) = s.W;
            Unsafe.Add(ref destBase, 7) = s.W;
        }
    }
}