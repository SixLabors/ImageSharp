﻿






// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

// <auto-generated />

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
	internal partial struct Block8x8F
    {
		/// <summary>
        /// Transpose the block into the destination block.
        /// </summary>
        /// <param name="d">The destination block</param>
		[MethodImpl(InliningOptions.ShortMethod)]
        public void TransposeInto(ref Block8x8F d)
        {
            d.V0L.X = V0L.X;
            d.V1L.X = V0L.Y;
            d.V2L.X = V0L.Z;
            d.V3L.X = V0L.W;
            d.V4L.X = V0R.X;
            d.V5L.X = V0R.Y;
            d.V6L.X = V0R.Z;
            d.V7L.X = V0R.W;

            d.V0L.Y = V1L.X;
            d.V1L.Y = V1L.Y;
            d.V2L.Y = V1L.Z;
            d.V3L.Y = V1L.W;
            d.V4L.Y = V1R.X;
            d.V5L.Y = V1R.Y;
            d.V6L.Y = V1R.Z;
            d.V7L.Y = V1R.W;

            d.V0L.Z = V2L.X;
            d.V1L.Z = V2L.Y;
            d.V2L.Z = V2L.Z;
            d.V3L.Z = V2L.W;
            d.V4L.Z = V2R.X;
            d.V5L.Z = V2R.Y;
            d.V6L.Z = V2R.Z;
            d.V7L.Z = V2R.W;

            d.V0L.W = V3L.X;
            d.V1L.W = V3L.Y;
            d.V2L.W = V3L.Z;
            d.V3L.W = V3L.W;
            d.V4L.W = V3R.X;
            d.V5L.W = V3R.Y;
            d.V6L.W = V3R.Z;
            d.V7L.W = V3R.W;

            d.V0R.X = V4L.X;
            d.V1R.X = V4L.Y;
            d.V2R.X = V4L.Z;
            d.V3R.X = V4L.W;
            d.V4R.X = V4R.X;
            d.V5R.X = V4R.Y;
            d.V6R.X = V4R.Z;
            d.V7R.X = V4R.W;

            d.V0R.Y = V5L.X;
            d.V1R.Y = V5L.Y;
            d.V2R.Y = V5L.Z;
            d.V3R.Y = V5L.W;
            d.V4R.Y = V5R.X;
            d.V5R.Y = V5R.Y;
            d.V6R.Y = V5R.Z;
            d.V7R.Y = V5R.W;

            d.V0R.Z = V6L.X;
            d.V1R.Z = V6L.Y;
            d.V2R.Z = V6L.Z;
            d.V3R.Z = V6L.W;
            d.V4R.Z = V6R.X;
            d.V5R.Z = V6R.Y;
            d.V6R.Z = V6R.Z;
            d.V7R.Z = V6R.W;

            d.V0R.W = V7L.X;
            d.V1R.W = V7L.Y;
            d.V2R.W = V7L.Z;
            d.V3R.W = V7L.W;
            d.V4R.W = V7R.X;
            d.V5R.W = V7R.Y;
            d.V6R.W = V7R.Z;
            d.V7R.W = V7R.W;

        }

		/// <summary>
        /// Level shift by +maximum/2, clip to [0, maximum]
        /// </summary>
        public void NormalizeColorsInplace(float maximum)
        {
            Vector4 CMin4 = new Vector4(0F);
            Vector4 CMax4 = new Vector4(maximum);
            Vector4 COff4 = new Vector4(MathF.Ceiling(maximum / 2));

            this.V0L = Vector4.Clamp(this.V0L + COff4, CMin4, CMax4);
            this.V0R = Vector4.Clamp(this.V0R + COff4, CMin4, CMax4);
            this.V1L = Vector4.Clamp(this.V1L + COff4, CMin4, CMax4);
            this.V1R = Vector4.Clamp(this.V1R + COff4, CMin4, CMax4);
            this.V2L = Vector4.Clamp(this.V2L + COff4, CMin4, CMax4);
            this.V2R = Vector4.Clamp(this.V2R + COff4, CMin4, CMax4);
            this.V3L = Vector4.Clamp(this.V3L + COff4, CMin4, CMax4);
            this.V3R = Vector4.Clamp(this.V3R + COff4, CMin4, CMax4);
            this.V4L = Vector4.Clamp(this.V4L + COff4, CMin4, CMax4);
            this.V4R = Vector4.Clamp(this.V4R + COff4, CMin4, CMax4);
            this.V5L = Vector4.Clamp(this.V5L + COff4, CMin4, CMax4);
            this.V5R = Vector4.Clamp(this.V5R + COff4, CMin4, CMax4);
            this.V6L = Vector4.Clamp(this.V6L + COff4, CMin4, CMax4);
            this.V6R = Vector4.Clamp(this.V6R + COff4, CMin4, CMax4);
            this.V7L = Vector4.Clamp(this.V7L + COff4, CMin4, CMax4);
            this.V7R = Vector4.Clamp(this.V7R + COff4, CMin4, CMax4);

        }

        /// <summary>
        /// AVX2-only variant for executing <see cref="NormalizeColorsInplace"/> and <see cref="RoundInplace"/> in one step.
        /// </summary>
        [MethodImpl(InliningOptions.ShortMethod)]
        public void NormalizeColorsAndRoundInplaceAvx2(float maximum)
        {
            Vector<float> off = new Vector<float>(MathF.Ceiling(maximum / 2));
            Vector<float> max = new Vector<float>(maximum);
            

            ref Vector<float> row0 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V0L);
            row0 = NormalizeAndRound(row0, off, max);
                

            ref Vector<float> row1 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V1L);
            row1 = NormalizeAndRound(row1, off, max);
                

            ref Vector<float> row2 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V2L);
            row2 = NormalizeAndRound(row2, off, max);
                

            ref Vector<float> row3 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V3L);
            row3 = NormalizeAndRound(row3, off, max);
                

            ref Vector<float> row4 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V4L);
            row4 = NormalizeAndRound(row4, off, max);
                

            ref Vector<float> row5 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V5L);
            row5 = NormalizeAndRound(row5, off, max);
                

            ref Vector<float> row6 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V6L);
            row6 = NormalizeAndRound(row6, off, max);
                

            ref Vector<float> row7 = ref Unsafe.As<Vector4, Vector<float>>(ref this.V7L);
            row7 = NormalizeAndRound(row7, off, max);
                

        }

        /// <summary>
        /// Fill the block from 'source' doing short -> float conversion.
        /// </summary>
        public void LoadFromInt16Scalar(ref Block8x8 source)
        {
            ref short selfRef = ref Unsafe.As<Block8x8, short>(ref source);

            this.V0L.X =  Unsafe.Add(ref selfRef, 0);
            this.V0L.Y =  Unsafe.Add(ref selfRef, 1);
            this.V0L.Z =  Unsafe.Add(ref selfRef, 2);
            this.V0L.W =  Unsafe.Add(ref selfRef, 3);
            this.V0R.X =  Unsafe.Add(ref selfRef, 4);
            this.V0R.Y =  Unsafe.Add(ref selfRef, 5);
            this.V0R.Z =  Unsafe.Add(ref selfRef, 6);
            this.V0R.W =  Unsafe.Add(ref selfRef, 7);

            this.V1L.X =  Unsafe.Add(ref selfRef, 8);
            this.V1L.Y =  Unsafe.Add(ref selfRef, 9);
            this.V1L.Z =  Unsafe.Add(ref selfRef, 10);
            this.V1L.W =  Unsafe.Add(ref selfRef, 11);
            this.V1R.X =  Unsafe.Add(ref selfRef, 12);
            this.V1R.Y =  Unsafe.Add(ref selfRef, 13);
            this.V1R.Z =  Unsafe.Add(ref selfRef, 14);
            this.V1R.W =  Unsafe.Add(ref selfRef, 15);

            this.V2L.X =  Unsafe.Add(ref selfRef, 16);
            this.V2L.Y =  Unsafe.Add(ref selfRef, 17);
            this.V2L.Z =  Unsafe.Add(ref selfRef, 18);
            this.V2L.W =  Unsafe.Add(ref selfRef, 19);
            this.V2R.X =  Unsafe.Add(ref selfRef, 20);
            this.V2R.Y =  Unsafe.Add(ref selfRef, 21);
            this.V2R.Z =  Unsafe.Add(ref selfRef, 22);
            this.V2R.W =  Unsafe.Add(ref selfRef, 23);

            this.V3L.X =  Unsafe.Add(ref selfRef, 24);
            this.V3L.Y =  Unsafe.Add(ref selfRef, 25);
            this.V3L.Z =  Unsafe.Add(ref selfRef, 26);
            this.V3L.W =  Unsafe.Add(ref selfRef, 27);
            this.V3R.X =  Unsafe.Add(ref selfRef, 28);
            this.V3R.Y =  Unsafe.Add(ref selfRef, 29);
            this.V3R.Z =  Unsafe.Add(ref selfRef, 30);
            this.V3R.W =  Unsafe.Add(ref selfRef, 31);

            this.V4L.X =  Unsafe.Add(ref selfRef, 32);
            this.V4L.Y =  Unsafe.Add(ref selfRef, 33);
            this.V4L.Z =  Unsafe.Add(ref selfRef, 34);
            this.V4L.W =  Unsafe.Add(ref selfRef, 35);
            this.V4R.X =  Unsafe.Add(ref selfRef, 36);
            this.V4R.Y =  Unsafe.Add(ref selfRef, 37);
            this.V4R.Z =  Unsafe.Add(ref selfRef, 38);
            this.V4R.W =  Unsafe.Add(ref selfRef, 39);

            this.V5L.X =  Unsafe.Add(ref selfRef, 40);
            this.V5L.Y =  Unsafe.Add(ref selfRef, 41);
            this.V5L.Z =  Unsafe.Add(ref selfRef, 42);
            this.V5L.W =  Unsafe.Add(ref selfRef, 43);
            this.V5R.X =  Unsafe.Add(ref selfRef, 44);
            this.V5R.Y =  Unsafe.Add(ref selfRef, 45);
            this.V5R.Z =  Unsafe.Add(ref selfRef, 46);
            this.V5R.W =  Unsafe.Add(ref selfRef, 47);

            this.V6L.X =  Unsafe.Add(ref selfRef, 48);
            this.V6L.Y =  Unsafe.Add(ref selfRef, 49);
            this.V6L.Z =  Unsafe.Add(ref selfRef, 50);
            this.V6L.W =  Unsafe.Add(ref selfRef, 51);
            this.V6R.X =  Unsafe.Add(ref selfRef, 52);
            this.V6R.Y =  Unsafe.Add(ref selfRef, 53);
            this.V6R.Z =  Unsafe.Add(ref selfRef, 54);
            this.V6R.W =  Unsafe.Add(ref selfRef, 55);

            this.V7L.X =  Unsafe.Add(ref selfRef, 56);
            this.V7L.Y =  Unsafe.Add(ref selfRef, 57);
            this.V7L.Z =  Unsafe.Add(ref selfRef, 58);
            this.V7L.W =  Unsafe.Add(ref selfRef, 59);
            this.V7R.X =  Unsafe.Add(ref selfRef, 60);
            this.V7R.Y =  Unsafe.Add(ref selfRef, 61);
            this.V7R.Z =  Unsafe.Add(ref selfRef, 62);
            this.V7R.W =  Unsafe.Add(ref selfRef, 63);

        }
	}
}
