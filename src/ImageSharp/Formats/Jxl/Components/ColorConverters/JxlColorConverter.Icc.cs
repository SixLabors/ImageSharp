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
    /// <summary>
    /// Converts an ICC device row to working sRGB and restores its separate image alpha.
    /// </summary>
    /// <param name="colors">The device components, replaced in place by working sRGB pixel vectors.</param>
    /// <param name="profileConverter">The paired source-profile to sRGB transform.</param>
    /// <param name="alpha">The unchanged image alpha row, or an empty span for opaque output.</param>
    public static void ApplyOutputConversion(Span<Vector4> colors, ColorProfileConverter profileConverter, ReadOnlySpan<float> alpha)
    {
        profileConverter.ConvertUsingIccProfile(colors, colors);

        // ICC's fourth slot is a device component, not alpha. Restore the separate opacity after conversion.
        if (alpha.IsEmpty)
        {
            Numerics.WithW(colors, 1F);
        }
        else
        {
            Numerics.WithW(colors, alpha);
        }
    }

    private sealed partial class JxlColorConverter<TOperator>
    {
        /// <inheritdoc/>
        public override void ConvertIccComponents<TSample, TSampleOperator>(
            in ComponentValues<TSample> source,
            Span<Vector4> destination,
            in TSampleOperator sampleOperator)
        {
            // Retain one local copy so constrained calls do not copy the readonly operation per block.
            TOperator operation = this.operation;
            TSampleOperator sampleOperation = sampleOperator;

            // Reconstructed planes stay fixed during conversion. Retain their spans locally so stores
            // and constrained calls cannot force the JIT to reload the borrowed descriptor in each block.
            ReadOnlySpan<TSample> component0 = source.Component0;
            ReadOnlySpan<TSample> component1 = source.Component1;
            ReadOnlySpan<TSample> component2 = source.Component2;
            int count = component0.Length;
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
                    Vector512<float> x = sampleOperation.Invoke(JxlSampleConverter.Load512(component0, i));
                    Vector512<float> y = sampleOperation.Invoke(JxlSampleConverter.Load512(component1, i));
                    Vector512<float> z = sampleOperation.Invoke(JxlSampleConverter.Load512(component2, i));
                    Vector512<float> k = TOperator.ComponentCount == 4 ? Vector512.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector256Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector256<float>.Count)
                {
                    Vector256<float> x = sampleOperation.Invoke(JxlSampleConverter.Load256(component0, i));
                    Vector256<float> y = sampleOperation.Invoke(JxlSampleConverter.Load256(component1, i));
                    Vector256<float> z = sampleOperation.Invoke(JxlSampleConverter.Load256(component2, i));
                    Vector256<float> k = TOperator.ComponentCount == 4 ? Vector256.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                nuint vectorCount = (count - i).Vector128Count<float>();
                for (nuint block = 0; block < vectorCount; block++, i += Vector128<float>.Count)
                {
                    Vector128<float> x = sampleOperation.Invoke(JxlSampleConverter.Load128(component0, i));
                    Vector128<float> y = sampleOperation.Invoke(JxlSampleConverter.Load128(component1, i));
                    Vector128<float> z = sampleOperation.Invoke(JxlSampleConverter.Load128(component2, i));
                    Vector128<float> k = TOperator.ComponentCount == 4 ? Vector128.LoadUnsafe(ref black, (nuint)i) : default;
                    operation.Invoke(ref x, ref y, ref z, ref k);
                    SimdUtils.Interleave4(x, y, z, k, ref Unsafe.Add(ref target, (nuint)i * 4));
                }
            }

            // The scalar remainder uses the same operator and sample normalization as every vector width.
            for (; i < count; i++)
            {
                float x = sampleOperation.Invoke(component0[i]);
                float y = sampleOperation.Invoke(component1[i]);
                float z = sampleOperation.Invoke(component2[i]);
                float k = TOperator.ComponentCount == 4 ? source.Black[i] : 0F;
                operation.Invoke(ref x, ref y, ref z, ref k);
                destination[i] = new Vector4(x, y, z, k);
            }
        }
    }
}
