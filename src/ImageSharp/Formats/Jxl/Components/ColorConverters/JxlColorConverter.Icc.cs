// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

internal abstract partial class JxlColorConverterBase
{
    private sealed partial class JxlColorConverter<TOperator>
    {
        /// <inheritdoc/>
        public override void ConvertToRgb<TSample, TSampleOperator>(
            in ComponentValues<TSample> source,
            Span<Vector4> destination,
            ColorProfileConverter profileConverter,
            in TSampleOperator sampleOperator)
        {
            int count = source.Component0.Length;
            ref TSample c0 = ref MemoryMarshal.GetReference(source.Component0);
            ref TSample c1 = ref MemoryMarshal.GetReference(source.Component1);
            ref TSample c2 = ref MemoryMarshal.GetReference(source.Component2);
            ref float black = ref MemoryMarshal.GetReference(source.Black);
            ref float target = ref Unsafe.As<Vector4, float>(ref MemoryMarshal.GetReference(destination));
            int i = 0;

            // ICC device samples use four slots. W can hold black; image alpha stays separate until after ICC.
            // Counts use float output lanes. Short loads fill only the low half of their input register;
            // the sample operator widens those carriers before the device arithmetic.
            if (Vector512.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector512Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector512<float>.Count)
                {
                    Vector512<float> x = sampleOperator.Invoke(JxlSampleConverter.Load512(source.Component0, i));
                    Vector512<float> y = sampleOperator.Invoke(JxlSampleConverter.Load512(source.Component1, i));
                    Vector512<float> z = sampleOperator.Invoke(JxlSampleConverter.Load512(source.Component2, i));
                    Vector512<float> k = TOperator.ComponentCount == 4 ? Vector512.LoadUnsafe(ref black, (nuint)i) : default;
                    this.operation.Invoke(ref x, ref y, ref z, ref k);
                    SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector256Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x = sampleOperator.Invoke(JxlSampleConverter.Load256(source.Component0, i));
                    Vector256<float> y = sampleOperator.Invoke(JxlSampleConverter.Load256(source.Component1, i));
                    Vector256<float> z = sampleOperator.Invoke(JxlSampleConverter.Load256(source.Component2, i));
                    Vector256<float> k = TOperator.ComponentCount == 4 ? Vector256.LoadUnsafe(ref black, (nuint)i) : default;
                    this.operation.Invoke(ref x, ref y, ref z, ref k);
                    SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector128Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x = sampleOperator.Invoke(JxlSampleConverter.Load128(source.Component0, i));
                    Vector128<float> y = sampleOperator.Invoke(JxlSampleConverter.Load128(source.Component1, i));
                    Vector128<float> z = sampleOperator.Invoke(JxlSampleConverter.Load128(source.Component2, i));
                    Vector128<float> k = TOperator.ComponentCount == 4 ? Vector128.LoadUnsafe(ref black, (nuint)i) : default;
                    this.operation.Invoke(ref x, ref y, ref z, ref k);
                    SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            // The scalar remainder uses the same operator and sample normalization as every vector width.
            for (; i < count; i++)
            {
                float x = sampleOperator.Invoke(Unsafe.Add(ref c0, i));
                float y = sampleOperator.Invoke(Unsafe.Add(ref c1, i));
                float z = sampleOperator.Invoke(Unsafe.Add(ref c2, i));
                float k = TOperator.ComponentCount == 4 ? Unsafe.Add(ref black, i) : 0F;
                this.operation.Invoke(ref x, ref y, ref z, ref k);
                destination[i] = new Vector4(x, y, z, k);
            }

            profileConverter.ConvertUsingIccProfile(destination, destination);

            // ICC's fourth slot is a device component, not alpha. Restore the separate opacity after conversion.
            if (source.Alpha.IsEmpty)
            {
                Numerics.WithW(destination, 1F);
            }
            else
            {
                Numerics.WithW(destination, source.Alpha);
            }
        }
    }
}
