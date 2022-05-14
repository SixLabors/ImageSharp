// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class CmykVector : JpegColorConverterVector
        {
            public CmykVector(int precision)
                : base(JpegColorSpace.Cmyk, precision)
            {
            }

            protected override void ConvertCoreVectorizedInplaceToRgb(in ComponentValues values)
            {
                ref Vector<float> cBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> mBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> yBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> kBase =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                var scale = new Vector<float>(1 / (this.MaximumValue * this.MaximumValue));

                nint n = values.Component0.Length / Vector<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector<float> c = ref Unsafe.Add(ref cBase, i);
                    ref Vector<float> m = ref Unsafe.Add(ref mBase, i);
                    ref Vector<float> y = ref Unsafe.Add(ref yBase, i);
                    Vector<float> k = Unsafe.Add(ref kBase, i);

                    k *= scale;
                    c *= k;
                    m *= k;
                    y *= k;
                }
            }

            protected override void ConvertCoreInplaceToRgb(in ComponentValues values)
                 => CmykScalar.ConvertToRgbInplace(values, this.MaximumValue);

            protected override void ConvertCoreVectorizedInplaceFromRgb(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
            {
                ref Vector<float> destC =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> destM =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> destY =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> destK =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                ref Vector<float> srcR =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(r));
                ref Vector<float> srcG =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(g));
                ref Vector<float> srcB =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(b));

                // Used for the color conversion
                var scale = new Vector<float>(this.MaximumValue);

                nint n = values.Component0.Length / Vector<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    Vector<float> ctmp = scale - Unsafe.Add(ref srcR, i);
                    Vector<float> mtmp = scale - Unsafe.Add(ref srcG, i);
                    Vector<float> ytmp = scale - Unsafe.Add(ref srcB, i);
                    Vector<float> ktmp = Vector.Min(ctmp, Vector.Min(mtmp, ytmp));

                    var kMask = Vector.Equals(ktmp, scale);
                    ctmp = Vector.AndNot((ctmp - ktmp) / (scale - ktmp), kMask.As<int, float>());
                    mtmp = Vector.AndNot((mtmp - ktmp) / (scale - ktmp), kMask.As<int, float>());
                    ytmp = Vector.AndNot((ytmp - ktmp) / (scale - ktmp), kMask.As<int, float>());

                    Unsafe.Add(ref destC, i) = scale - (ctmp * scale);
                    Unsafe.Add(ref destM, i) = scale - (mtmp * scale);
                    Unsafe.Add(ref destY, i) = scale - (ytmp * scale);
                    Unsafe.Add(ref destK, i) = scale - ktmp;
                }
            }

            protected override void ConvertCoreInplaceFromRgb(in ComponentValues values, Span<float> r, Span<float> g, Span<float> b)
                => CmykScalar.ConvertFromRgbInplace(values, this.MaximumValue, r, g, b);
        }
    }
}
