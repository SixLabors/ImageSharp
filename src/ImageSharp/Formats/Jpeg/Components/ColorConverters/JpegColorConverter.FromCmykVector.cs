// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal abstract partial class JpegColorConverterBase
    {
        internal sealed class FromCmykVector : JpegColorConverterVector
        {
            public FromCmykVector(int precision)
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
                 => FromCmykScalar.ConvertToRgbInplace(values, this.MaximumValue);

            protected override void ConvertCoreVectorizedInplaceFromRgb(in ComponentValues values)
            {
                ref Vector<float> c0Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component0));
                ref Vector<float> c1Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component1));
                ref Vector<float> c2Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component2));
                ref Vector<float> c3Base =
                    ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(values.Component3));

                // Used for the color conversion
                var scale = new Vector<float>(this.MaximumValue);
                var one = new Vector<float>(1f);

                nint n = values.Component0.Length / Vector<float>.Count;
                for (nint i = 0; i < n; i++)
                {
                    ref Vector<float> c0 = ref Unsafe.Add(ref c0Base, i);
                    ref Vector<float> c1 = ref Unsafe.Add(ref c1Base, i);
                    ref Vector<float> c2 = ref Unsafe.Add(ref c2Base, i);
                    ref Vector<float> c3 = ref Unsafe.Add(ref c3Base, i);

                    Vector<float> ctmp = one - c0;
                    Vector<float> mtmp = one - c1;
                    Vector<float> ytmp = one - c2;
                    Vector<float> ktmp = Vector.Min(ctmp, Vector.Min(mtmp, ytmp));

                    var kMask = Vector.Equals(ktmp, Vector<float>.One);
                    ctmp = Vector.AndNot((ctmp - ktmp) / (one - ktmp), kMask.As<int, float>());
                    mtmp = Vector.AndNot((mtmp - ktmp) / (one - ktmp), kMask.As<int, float>());
                    ytmp = Vector.AndNot((ytmp - ktmp) / (one - ktmp), kMask.As<int, float>());

                    c0 = scale - (ctmp * scale);
                    c1 = scale - (mtmp * scale);
                    c2 = scale - (ytmp * scale);
                    c3 = scale - (ktmp * scale);
                }
            }

            protected override void ConvertCoreInplaceFromRgb(in ComponentValues values)
                => FromCmykScalar.ConvertFromRgbInplace(values, this.MaximumValue);
        }
    }
}
