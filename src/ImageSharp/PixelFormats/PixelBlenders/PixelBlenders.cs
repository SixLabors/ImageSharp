// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.PixelFormats.PixelBlenders;

/// <summary>
/// Applies existing blend operators to vector rows without pixel storage conversion.
/// </summary>
internal static class PixelBlenders
{
    // PixelBlender<TPixel,TOperator> owns pixel conversion. These loops need only the operator,
    // so codec-owned vectors can use the same SIMD traversal without selecting an unrelated pixel format.

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vectors. The row must cover the output length.</param>
    /// <param name="amount">The source opacity, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void Blend<TOperator>(Span<Vector4> destination, ReadOnlySpan<Vector4> background, ReadOnlySpan<Vector4> source, float amount)
        where TOperator : struct, IPixelBlenderOperator
    {
        // Public entry points validate the row lengths, so all three references can advance in lockstep.
        int scalarStart = 0;
        amount = ColorNumerics.ClampToFinite(amount, 0, 1F);

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);
        ref Vector4 sourceRef = ref MemoryMarshal.GetReference(source);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            // A 512-bit register holds four complete four-component vectors in [X,Y,Z,W] groups.
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            ref Vector512<float> sourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref sourceRef);
            int vectorCount = destination.Length / 4;
            Vector512<float> amountVector = Vector512.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), Unsafe.Add(ref sourceBase, i), amountVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            // A 256-bit register holds two complete four-component vectors in [X,Y,Z,W] groups.
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            ref Vector256<float> sourceBase = ref Unsafe.As<Vector4, Vector256<float>>(ref sourceRef);
            int vectorCount = destination.Length / 2;
            Vector256<float> amountVector = Vector256.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), Unsafe.Add(ref sourceBase, i), amountVector);
            }

            scalarStart = vectorCount * 2;
        }

        // Vector4 is both the scalar pixel representation and the portable SIMD fallback.
        for (int i = scalarStart; i < destination.Length; i++)
        {
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.Invoke(Unsafe.Add(ref backgroundRef, (uint)i), Unsafe.Add(ref sourceRef, (uint)i), amount);
        }
    }

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vector used for every output sample.</param>
    /// <param name="amount">The source opacity, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void Blend<TOperator>(Span<Vector4> destination, ReadOnlySpan<Vector4> background, Vector4 source, float amount)
        where TOperator : struct, IPixelBlenderOperator
    {
        // Public entry points validate the row lengths, so the destination and background advance together.
        int scalarStart = 0;
        amount = ColorNumerics.ClampToFinite(amount, 0, 1F);

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            // Repeat one [X,Y,Z,W] source group four times to match the four background pixels.
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 4;
            Vector512<float> sourceVector = CreateVector512(source);
            Vector512<float> amountVector = Vector512.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), sourceVector, amountVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            // Repeat one [X,Y,Z,W] source group twice to match the two background pixels.
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 2;
            Vector256<float> sourceVector = CreateVector256(source);
            Vector256<float> amountVector = Vector256.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), sourceVector, amountVector);
            }

            scalarStart = vectorCount * 2;
        }

        // The remaining pixel count is at most three after AVX-512 or one after AVX2.
        for (int i = scalarStart; i < destination.Length; i++)
        {
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.Invoke(Unsafe.Add(ref backgroundRef, (uint)i), source, amount);
        }
    }

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vectors. The row must cover the output length.</param>
    /// <param name="amount">The source opacity for each output sample, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void Blend<TOperator>(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        ReadOnlySpan<Vector4> source,
        ReadOnlySpan<float> amount)
        where TOperator : struct, IPixelBlenderOperator
    {
        // Each amount belongs to one pixel and must be repeated across that pixel's four components.
        int scalarStart = 0;

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);
        ref Vector4 sourceRef = ref MemoryMarshal.GetReference(source);
        ref float amountRef = ref MemoryMarshal.GetReference(amount);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            ref Vector512<float> sourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref sourceRef);
            int vectorCount = destination.Length / 4;

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                // Four scalar amounts become four contiguous [a,a,a,a] groups before clamping.
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 4);
                Vector512<float> amountVector = CreateClampedVector512(ref amountBase);

                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), Unsafe.Add(ref sourceBase, i), amountVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            ref Vector256<float> sourceBase = ref Unsafe.As<Vector4, Vector256<float>>(ref sourceRef);
            int vectorCount = destination.Length / 2;

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                // Two scalar amounts become two contiguous [a,a,a,a] groups before clamping.
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 2);
                Vector256<float> amountVector = CreateClampedVector256(ref amountBase);

                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), Unsafe.Add(ref sourceBase, i), amountVector);
            }

            scalarStart = vectorCount * 2;
        }

        for (int i = scalarStart; i < destination.Length; i++)
        {
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.Invoke(
                Unsafe.Add(ref backgroundRef, (uint)i),
                Unsafe.Add(ref sourceRef, (uint)i),
                ColorNumerics.ClampToFinite(Unsafe.Add(ref amountRef, (uint)i), 0, 1F));
        }
    }

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vector used for every output sample.</param>
    /// <param name="amount">The source opacity for each output sample, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void Blend<TOperator>(Span<Vector4> destination, ReadOnlySpan<Vector4> background, Vector4 source, ReadOnlySpan<float> amount)
        where TOperator : struct, IPixelBlenderOperator
    {
        // The source is invariant, while each background pixel has its own independently clamped amount.
        int scalarStart = 0;

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);
        ref float amountRef = ref MemoryMarshal.GetReference(amount);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 4;
            Vector512<float> sourceVector = CreateVector512(source);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 4);
                Vector512<float> amountVector = CreateClampedVector512(ref amountBase);

                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), sourceVector, amountVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 2;
            Vector256<float> sourceVector = CreateVector256(source);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 2);
                Vector256<float> amountVector = CreateClampedVector256(ref amountBase);

                Unsafe.Add(ref destinationBase, i) = TOperator.Invoke(Unsafe.Add(ref backgroundBase, i), sourceVector, amountVector);
            }

            scalarStart = vectorCount * 2;
        }

        for (int i = scalarStart; i < destination.Length; i++)
        {
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.Invoke(
                Unsafe.Add(ref backgroundRef, (uint)i),
                source,
                ColorNumerics.ClampToFinite(Unsafe.Add(ref amountRef, (uint)i), 0, 1F));
        }
    }

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vectors. The row must cover the output length.</param>
    /// <param name="amount">The source opacity, clamped to finite values in [0,1].</param>
    /// <param name="coverage">The coverage row, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void BlendWithCoverage<TOperator>(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        ReadOnlySpan<Vector4> source,
        float amount,
        ReadOnlySpan<float> coverage)
        where TOperator : struct, IPixelBlenderOperator
    {
        // Coverage mixes the composed result back toward the original background, so it is fused into this pass.
        int scalarStart = 0;
        amount = ColorNumerics.ClampToFinite(amount, 0, 1F);

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);
        ref Vector4 sourceRef = ref MemoryMarshal.GetReference(source);
        ref float coverageRef = ref MemoryMarshal.GetReference(coverage);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            ref Vector512<float> sourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref sourceRef);
            int vectorCount = destination.Length / 4;
            Vector512<float> amountVector = Vector512.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector512<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 4);
                Vector512<float> coverageVector = CreateClampedVector512(ref coverageBase);
                Vector512<float> blended = TOperator.Invoke(backgroundVector, Unsafe.Add(ref sourceBase, i), amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            ref Vector256<float> sourceBase = ref Unsafe.As<Vector4, Vector256<float>>(ref sourceRef);
            int vectorCount = destination.Length / 2;
            Vector256<float> amountVector = Vector256.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector256<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 2);
                Vector256<float> coverageVector = CreateClampedVector256(ref coverageBase);
                Vector256<float> blended = TOperator.Invoke(backgroundVector, Unsafe.Add(ref sourceBase, i), amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 2;
        }

        for (int i = scalarStart; i < destination.Length; i++)
        {
            Vector4 backgroundPixel = Unsafe.Add(ref backgroundRef, (uint)i);
            Vector4 blended = TOperator.Invoke(backgroundPixel, Unsafe.Add(ref sourceRef, (uint)i), amount);

            float clampedCoverage = ColorNumerics.ClampToFinite(Unsafe.Add(ref coverageRef, (uint)i), 0, 1F);
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.IsAssociatedAlpha
                ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage)
                : PorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage);
        }
    }

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vector used for every output sample.</param>
    /// <param name="amount">The source opacity, clamped to finite values in [0,1].</param>
    /// <param name="coverage">The coverage row, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void BlendWithCoverage<TOperator>(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        Vector4 source,
        float amount,
        ReadOnlySpan<float> coverage)
        where TOperator : struct, IPixelBlenderOperator
    {
        // The constant source is expanded once per selected width and reused for the complete row.
        int scalarStart = 0;
        amount = ColorNumerics.ClampToFinite(amount, 0, 1F);

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);
        ref float coverageRef = ref MemoryMarshal.GetReference(coverage);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 4;
            Vector512<float> sourceVector = CreateVector512(source);
            Vector512<float> amountVector = Vector512.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector512<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 4);
                Vector512<float> coverageVector = CreateClampedVector512(ref coverageBase);
                Vector512<float> blended = TOperator.Invoke(backgroundVector, sourceVector, amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 2;
            Vector256<float> sourceVector = CreateVector256(source);
            Vector256<float> amountVector = Vector256.Create(amount);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector256<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 2);
                Vector256<float> coverageVector = CreateClampedVector256(ref coverageBase);
                Vector256<float> blended = TOperator.Invoke(backgroundVector, sourceVector, amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 2;
        }

        for (int i = scalarStart; i < destination.Length; i++)
        {
            Vector4 backgroundPixel = Unsafe.Add(ref backgroundRef, (uint)i);
            Vector4 blended = TOperator.Invoke(backgroundPixel, source, amount);

            float clampedCoverage = ColorNumerics.ClampToFinite(Unsafe.Add(ref coverageRef, (uint)i), 0, 1F);
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.IsAssociatedAlpha
                ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage)
                : PorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage);
        }
    }

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vectors. The row must cover the output length.</param>
    /// <param name="amount">The source opacity for each output sample, clamped to finite values in [0,1].</param>
    /// <param name="coverage">The coverage row, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void BlendWithCoverage<TOperator>(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        ReadOnlySpan<Vector4> source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage)
        where TOperator : struct, IPixelBlenderOperator
    {
        // Amount controls composition while coverage controls the final mix with the untouched background.
        int scalarStart = 0;

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);
        ref Vector4 sourceRef = ref MemoryMarshal.GetReference(source);
        ref float amountRef = ref MemoryMarshal.GetReference(amount);
        ref float coverageRef = ref MemoryMarshal.GetReference(coverage);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            ref Vector512<float> sourceBase = ref Unsafe.As<Vector4, Vector512<float>>(ref sourceRef);
            int vectorCount = destination.Length / 4;

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector512<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 4);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 4);
                Vector512<float> amountVector = CreateClampedVector512(ref amountBase);
                Vector512<float> coverageVector = CreateClampedVector512(ref coverageBase);
                Vector512<float> blended = TOperator.Invoke(backgroundVector, Unsafe.Add(ref sourceBase, i), amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            ref Vector256<float> sourceBase = ref Unsafe.As<Vector4, Vector256<float>>(ref sourceRef);
            int vectorCount = destination.Length / 2;

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector256<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 2);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 2);
                Vector256<float> amountVector = CreateClampedVector256(ref amountBase);
                Vector256<float> coverageVector = CreateClampedVector256(ref coverageBase);
                Vector256<float> blended = TOperator.Invoke(backgroundVector, Unsafe.Add(ref sourceBase, i), amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 2;
        }

        for (int i = scalarStart; i < destination.Length; i++)
        {
            Vector4 backgroundPixel = Unsafe.Add(ref backgroundRef, (uint)i);

            Vector4 blended = TOperator.Invoke(
                backgroundPixel,
                Unsafe.Add(ref sourceRef, (uint)i),
                ColorNumerics.ClampToFinite(Unsafe.Add(ref amountRef, (uint)i), 0, 1F));

            float clampedCoverage = ColorNumerics.ClampToFinite(Unsafe.Add(ref coverageRef, (uint)i), 0, 1F);
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.IsAssociatedAlpha
                ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage)
                : PorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage);
        }
    }

    /// <summary>
    /// Applies the selected blend operator to vector rows without pixel storage conversion.
    /// </summary>
    /// <typeparam name="TOperator">The existing color and alpha composition operator.</typeparam>
    /// <param name="destination">The output vectors. Its length selects the number of samples.</param>
    /// <param name="background">The background vectors. The row must cover the output length.</param>
    /// <param name="source">The source vector used for every output sample.</param>
    /// <param name="amount">The source opacity for each output sample, clamped to finite values in [0,1].</param>
    /// <param name="coverage">The coverage row, clamped to finite values in [0,1].</param>
    /// <remarks>Inputs must use the alpha representation required by the operator.</remarks>
    public static void BlendWithCoverage<TOperator>(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        Vector4 source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage)
        where TOperator : struct, IPixelBlenderOperator
    {
        // The invariant source is expanded once; only amount and coverage are gathered for each vector batch.
        int scalarStart = 0;

        ref Vector4 destinationRef = ref MemoryMarshal.GetReference(destination);
        ref Vector4 backgroundRef = ref MemoryMarshal.GetReference(background);
        ref float amountRef = ref MemoryMarshal.GetReference(amount);
        ref float coverageRef = ref MemoryMarshal.GetReference(coverage);

        if (Vector512.IsHardwareAccelerated && destination.Length >= 4)
        {
            ref Vector512<float> destinationBase = ref Unsafe.As<Vector4, Vector512<float>>(ref destinationRef);
            ref Vector512<float> backgroundBase = ref Unsafe.As<Vector4, Vector512<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 4;
            Vector512<float> sourceVector = CreateVector512(source);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector512<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 4);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 4);
                Vector512<float> amountVector = CreateClampedVector512(ref amountBase);
                Vector512<float> coverageVector = CreateClampedVector512(ref coverageBase);
                Vector512<float> blended = TOperator.Invoke(backgroundVector, sourceVector, amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 4;
        }
        else if (Vector256.IsHardwareAccelerated && destination.Length >= 2)
        {
            ref Vector256<float> destinationBase = ref Unsafe.As<Vector4, Vector256<float>>(ref destinationRef);
            ref Vector256<float> backgroundBase = ref Unsafe.As<Vector4, Vector256<float>>(ref backgroundRef);
            int vectorCount = destination.Length / 2;
            Vector256<float> sourceVector = CreateVector256(source);

            for (nuint i = 0; i < (uint)vectorCount; i++)
            {
                ref Vector256<float> backgroundVector = ref Unsafe.Add(ref backgroundBase, i);
                ref float amountBase = ref Unsafe.Add(ref amountRef, i * 2);
                ref float coverageBase = ref Unsafe.Add(ref coverageRef, i * 2);
                Vector256<float> amountVector = CreateClampedVector256(ref amountBase);
                Vector256<float> coverageVector = CreateClampedVector256(ref coverageBase);
                Vector256<float> blended = TOperator.Invoke(backgroundVector, sourceVector, amountVector);

                Unsafe.Add(ref destinationBase, i) = TOperator.IsAssociatedAlpha
                    ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector)
                    : PorterDuffFunctions.BlendWithCoverage(backgroundVector, blended, coverageVector);
            }

            scalarStart = vectorCount * 2;
        }

        for (int i = scalarStart; i < destination.Length; i++)
        {
            Vector4 backgroundPixel = Unsafe.Add(ref backgroundRef, (uint)i);
            Vector4 blended = TOperator.Invoke(backgroundPixel, source, ColorNumerics.ClampToFinite(Unsafe.Add(ref amountRef, (uint)i), 0, 1F));

            float clampedCoverage = ColorNumerics.ClampToFinite(Unsafe.Add(ref coverageRef, (uint)i), 0, 1F);
            Unsafe.Add(ref destinationRef, (uint)i) = TOperator.IsAssociatedAlpha
                ? AssociatedAlphaPorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage)
                : PorterDuffFunctions.BlendWithCoverage(backgroundPixel, blended, clampedCoverage);
        }
    }

    /// <summary>
    /// Repeats one pixel vector twice to fill a 256-bit register.
    /// </summary>
    /// <param name="pixel">The pixel vector.</param>
    /// <returns>Two consecutive copies of the pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> CreateVector256(Vector4 pixel)
        => Vector256.Create(pixel.X, pixel.Y, pixel.Z, pixel.W, pixel.X, pixel.Y, pixel.Z, pixel.W);

    /// <summary>
    /// Repeats one pixel vector four times to fill a 512-bit register.
    /// </summary>
    /// <param name="pixel">The pixel vector.</param>
    /// <returns>Four consecutive copies of the pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> CreateVector512(Vector4 pixel)
        => Vector512.Create(
            pixel.X,
            pixel.Y,
            pixel.Z,
            pixel.W,
            pixel.X,
            pixel.Y,
            pixel.Z,
            pixel.W,
            pixel.X,
            pixel.Y,
            pixel.Z,
            pixel.W,
            pixel.X,
            pixel.Y,
            pixel.Z,
            pixel.W);

    /// <summary>
    /// Expands and clamps two per-pixel scalar values for two packed RGBA pixels.
    /// </summary>
    /// <param name="values">The first of two contiguous scalar values.</param>
    /// <returns>Two consecutive groups containing one clamped value in each group's four lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> CreateClampedVector256(ref float values)
    {
        Vector256<float> result = Vector256.Create(Vector128.Create(values), Vector128.Create(Unsafe.Add(ref values, 1)));

        // Use the scalar tail's rule: NaN becomes zero, and infinities become the corresponding opacity endpoints.
        return ColorNumerics.ClampToFinite(result, Vector256<float>.Zero, Vector256<float>.One);
    }

    /// <summary>
    /// Expands and clamps four per-pixel scalar values for four packed RGBA pixels.
    /// </summary>
    /// <param name="values">The first of four contiguous scalar values.</param>
    /// <returns>Four consecutive groups containing one clamped value in each group's four lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> CreateClampedVector512(ref float values)
    {
        Vector512<float> result = Vector512.Create(
            values,
            values,
            values,
            values,
            Unsafe.Add(ref values, 1),
            Unsafe.Add(ref values, 1),
            Unsafe.Add(ref values, 1),
            Unsafe.Add(ref values, 1),
            Unsafe.Add(ref values, 2),
            Unsafe.Add(ref values, 2),
            Unsafe.Add(ref values, 2),
            Unsafe.Add(ref values, 2),
            Unsafe.Add(ref values, 3),
            Unsafe.Add(ref values, 3),
            Unsafe.Add(ref values, 3),
            Unsafe.Add(ref values, 3));

        // Use the scalar tail's rule: NaN becomes zero, and infinities become the corresponding opacity endpoints.
        return ColorNumerics.ClampToFinite(result, Vector512<float>.Zero, Vector512<float>.One);
    }
}
