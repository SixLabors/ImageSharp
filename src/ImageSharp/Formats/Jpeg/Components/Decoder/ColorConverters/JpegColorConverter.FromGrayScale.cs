// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters
{
    internal abstract partial class JpegColorConverter
    {
        internal class FromGrayscale : JpegColorConverter
        {
            public FromGrayscale()
                : base(JpegColorSpace.Grayscale)
            {
            }

            public override void ConvertToRgba(in ComponentValues values, Span<Vector4> result)
            {
                var scale = new Vector4(1 / 255F, 1 / 255F, 1 / 255F, 1F);

                ref float sBase = ref MemoryMarshal.GetReference(values.Component0);
                ref Vector4 dBase = ref MemoryMarshal.GetReference(result);

                for (int i = 0; i < result.Length; i++)
                {
                    var v = new Vector4(Unsafe.Add(ref sBase, i));
                    v.W = 1f;
                    v *= scale;
                    Unsafe.Add(ref dBase, i) = v;
                }
            }
        }
    }
}