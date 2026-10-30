// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

/// <content>
/// Applies native weighted addition and multiplication with the shared finite clamp.
/// </content>
internal static partial class JxlBlending
{
    // These native modes operate on independent component values. They do not perform Porter-Duff alpha composition.
    // Shared tensor arithmetic supplies the component operations; encoded clamps apply only to the named source inputs.

    /// <summary>
    /// Adds source samples weighted by the selected extra channel.
    /// </summary>
    /// <param name="background">The background samples.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="sourceWeight">The selected source weights.</param>
    /// <param name="destination">The output samples. All inputs must cover its length.</param>
    /// <param name="clampWeight">Whether to bound source weights to [0,1] with the shared finite clamp.</param>
    public static void AddWeighted(
        ReadOnlySpan<float> background,
        ReadOnlySpan<float> source,
        ReadOnlySpan<float> sourceWeight,
        Span<float> destination,
        bool clampWeight)
    {
        if (source == sourceWeight)
        {
            // The selected weight channel retains its background in this native mode. It must not be added to itself.
            background[..destination.Length].CopyTo(destination);
            return;
        }

        if (!clampWeight)
        {
            TensorPrimitives.MultiplyAdd(source[..destination.Length], sourceWeight[..destination.Length], background[..destination.Length], destination);
            return;
        }

        ref float backgroundRef = ref MemoryMarshal.GetReference(background);
        ref float sourceRef = ref MemoryMarshal.GetReference(source);
        ref float weightRef = ref MemoryMarshal.GetReference(sourceWeight);
        ref float destinationRef = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector512Count<float>();

            // Each lane is one independent sample. Clamp and multiply in registers without copying a weight row.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> weight = ColorNumerics.ClampToFinite(
                    Vector512.LoadUnsafe(ref weightRef, (uint)i),
                    Vector512<float>.Zero,
                    Vector512.Create(1F));

                Vector512<float> result = Vector512.LoadUnsafe(ref backgroundRef, (uint)i)
                    + (Vector512.LoadUnsafe(ref sourceRef, (uint)i) * weight);

                result.StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector256Count<float>();

            // Each lane is one independent sample. Clamp and multiply in registers without copying a weight row.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> weight = ColorNumerics.ClampToFinite(
                    Vector256.LoadUnsafe(ref weightRef, (uint)i),
                    Vector256<float>.Zero,
                    Vector256.Create(1F));

                Vector256<float> result = Vector256.LoadUnsafe(ref backgroundRef, (uint)i)
                    + (Vector256.LoadUnsafe(ref sourceRef, (uint)i) * weight);

                result.StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector128Count<float>();

            // Each lane is one independent sample. Clamp and multiply in registers without copying a weight row.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> weight = ColorNumerics.ClampToFinite(
                    Vector128.LoadUnsafe(ref weightRef, (uint)i),
                    Vector128<float>.Zero,
                    Vector128.Create(1F));

                Vector128<float> result = Vector128.LoadUnsafe(ref backgroundRef, (uint)i)
                    + (Vector128.LoadUnsafe(ref sourceRef, (uint)i) * weight);

                result.StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        // Complete-register reads precede each store. The scalar tail also permits exact in-place output.
        for (; i < destination.Length; i++)
        {
            float weight = ColorNumerics.ClampToFinite(Unsafe.Add(ref weightRef, (uint)i), 0F, 1F);
            Unsafe.Add(ref destinationRef, (uint)i) = Unsafe.Add(ref backgroundRef, (uint)i) + (Unsafe.Add(ref sourceRef, (uint)i) * weight);
        }
    }

    /// <summary>
    /// Multiplies native component rows with the format's optional clamp on source samples.
    /// </summary>
    /// <param name="background">The background samples.</param>
    /// <param name="source">The source samples.</param>
    /// <param name="destination">The output samples. Both inputs must cover its length.</param>
    /// <param name="clampSource">Whether to bound source samples to [0,1] with the shared finite clamp.</param>
    public static void Multiply(ReadOnlySpan<float> background, ReadOnlySpan<float> source, Span<float> destination, bool clampSource)
    {
        if (!clampSource)
        {
            TensorPrimitives.Multiply(background[..destination.Length], source[..destination.Length], destination);
            return;
        }

        ref float backgroundRef = ref MemoryMarshal.GetReference(background);
        ref float sourceRef = ref MemoryMarshal.GetReference(source);
        ref float destinationRef = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector512Count<float>();

            // Only source samples use the encoded clamp. Background samples and products retain their native range.
            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> value = ColorNumerics.ClampToFinite(
                    Vector512.LoadUnsafe(ref sourceRef, (uint)i),
                    Vector512<float>.Zero,
                    Vector512.Create(1F));

                Vector512<float> result = Vector512.LoadUnsafe(ref backgroundRef, (uint)i) * value;
                result.StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector256Count<float>();

            // Only source samples use the encoded clamp. Background samples and products retain their native range.
            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> value = ColorNumerics.ClampToFinite(
                    Vector256.LoadUnsafe(ref sourceRef, (uint)i),
                    Vector256<float>.Zero,
                    Vector256.Create(1F));

                Vector256<float> result = Vector256.LoadUnsafe(ref backgroundRef, (uint)i) * value;
                result.StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (destination.Length - i).Vector128Count<float>();

            // Only source samples use the encoded clamp. Background samples and products retain their native range.
            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> value = ColorNumerics.ClampToFinite(
                    Vector128.LoadUnsafe(ref sourceRef, (uint)i),
                    Vector128<float>.Zero,
                    Vector128.Create(1F));

                Vector128<float> result = Vector128.LoadUnsafe(ref backgroundRef, (uint)i) * value;
                result.StoreUnsafe(ref destinationRef, (uint)i);
            }
        }

        // The scalar remainder uses the same clamp. Both inputs are read before an exact in-place output write.
        for (; i < destination.Length; i++)
        {
            float value = ColorNumerics.ClampToFinite(Unsafe.Add(ref sourceRef, (uint)i), 0F, 1F);
            Unsafe.Add(ref destinationRef, (uint)i) = Unsafe.Add(ref backgroundRef, (uint)i) * value;
        }
    }
}
