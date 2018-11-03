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
        /// Copy block data into the destination color buffer pixel area with the provided horizontal and vertical scale factors.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void CopyTo(in BufferArea<float> area, int horizontalScale, int verticalScale)
        {
            if (horizontalScale == 1 && verticalScale == 1)
            {
                this.Copy1x1Scale(area);
                return;
            }

            if (horizontalScale == 2 && verticalScale == 2)
            {
                this.Copy2x2Scale(area);
                return;
            }

            // TODO: Optimize: implement all the cases with scale-specific, loopless code!
            this.CopyArbitraryScale(area, horizontalScale, verticalScale);
        }

        public void Copy1x1Scale(in BufferArea<float> destination)
        {
            ref byte selfBase = ref Unsafe.As<Block8x8F, byte>(ref this);
            ref byte destBase = ref Unsafe.As<float, byte>(ref destination.GetReferenceToOrigin());
            int destStride = destination.Stride * sizeof(float);

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

        private void Copy2x2Scale(in BufferArea<float> area)
        {
            ref Vector2 destBase = ref Unsafe.As<float, Vector2>(ref area.GetReferenceToOrigin());
            int destStride = area.Stride / 2;

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
        private void WidenCopyImpl2x2(ref Vector2 destBase, int row, int destStride)
        {
            ref Vector4 sLeft = ref Unsafe.Add(ref this.V0L, 2 * row);
            ref Vector4 sRight = ref Unsafe.Add(ref sLeft, 1);

            ref Vector2 dTopLeft = ref Unsafe.Add(ref destBase, 2 * row * destStride);
            ref Vector2 dTopRight = ref Unsafe.Add(ref dTopLeft, 4);
            ref Vector2 dBottomLeft = ref Unsafe.Add(ref dTopLeft, destStride);
            ref Vector2 dBottomRight = ref Unsafe.Add(ref dBottomLeft, 4);

            var xLeft = new Vector4(sLeft.X);
            var yLeft = new Vector4(sLeft.Y);
            var zLeft = new Vector4(sLeft.Z);
            var wLeft = new Vector4(sLeft.W);

            var xRight = new Vector4(sRight.X);
            var yRight = new Vector4(sRight.Y);
            var zRight = new Vector4(sRight.Z);
            var wRight = new Vector4(sRight.W);

            Unsafe.As<Vector2, Vector4>(ref dTopLeft) = xLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopLeft, 1)) = yLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopLeft, 2)) = zLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopLeft, 3)) = wLeft;

            Unsafe.As<Vector2, Vector4>(ref dTopRight) = xRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopRight, 1)) = yRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopRight, 2)) = zRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopRight, 3)) = wRight;

            Unsafe.As<Vector2, Vector4>(ref dBottomLeft) = xLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomLeft, 1)) = yLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomLeft, 2)) = zLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomLeft, 3)) = wLeft;

            Unsafe.As<Vector2, Vector4>(ref dBottomRight) = xRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomRight, 1)) = yRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomRight, 2)) = zRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomRight, 3)) = wRight;
        }

        [MethodImpl(InliningOptions.ColdPath)]
        private void CopyArbitraryScale(BufferArea<float> area, int horizontalScale, int verticalScale)
        {
            ref float destBase = ref area.GetReferenceToOrigin();

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
    }
}