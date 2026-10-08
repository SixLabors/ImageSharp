// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Normalizes interleaved raw bit-reader codes in bounded vector blocks.
/// </summary>
internal readonly struct TiffBitPackedColorOperator
{
    private readonly Vector4 divisor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffBitPackedColorOperator"/> struct.
    /// </summary>
    /// <param name="redBits">The red sample width.</param>
    /// <param name="greenBits">The green sample width.</param>
    /// <param name="blueBits">The blue sample width.</param>
    /// <param name="alphaBits">The alpha sample width, or zero for opaque RGB.</param>
    public TiffBitPackedColorOperator(int redBits, int greenBits, int blueBits, int alphaBits)
    {
        this.divisor = new Vector4((1 << redBits) - 1, (1 << greenBits) - 1, (1 << blueBits) - 1, alphaBits == 0 ? 1F : (1 << alphaBits) - 1);
    }

    /// <summary>
    /// Normalizes a bounded set of raw RGB or RGBA codes in place.
    /// </summary>
    /// <param name="samples">The raw codes, replaced by scaled components.</param>
    public void Convert(Span<Vector4> samples)
    {
        ReadOnlySpan<float> components = MemoryMarshal.Cast<Vector4, float>(samples);
        ref float words = ref MemoryMarshal.GetReference(MemoryMarshal.Cast<Vector4, float>(samples));
        int i = 0;

        // Each pixel occupies four adjacent float lanes. The divisor repeats in RGBA order within every register.
        // Complete registers contain four pixels at 512 bits, two at 256 bits, and one at 128 bits.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint groups = components.Vector512Count<float>();
            Vector512<float> factors = Vector512.Create(this.divisor.X, this.divisor.Y, this.divisor.Z, this.divisor.W, this.divisor.X, this.divisor.Y, this.divisor.Z, this.divisor.W, this.divisor.X, this.divisor.Y, this.divisor.Z, this.divisor.W, this.divisor.X, this.divisor.Y, this.divisor.Z, this.divisor.W);

            for (nuint group = 0; group < groups; group++, i += 4)
            {
                ref float first = ref Unsafe.Add(ref words, i * 4);
                (Vector512.LoadUnsafe(ref first) / factors).StoreUnsafe(ref first);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint groups = components[(i * 4)..].Vector256Count<float>();
            Vector256<float> factors = Vector256.Create(this.divisor.X, this.divisor.Y, this.divisor.Z, this.divisor.W, this.divisor.X, this.divisor.Y, this.divisor.Z, this.divisor.W);

            for (nuint group = 0; group < groups; group++, i += 2)
            {
                ref float first = ref Unsafe.Add(ref words, i * 4);
                (Vector256.LoadUnsafe(ref first) / factors).StoreUnsafe(ref first);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint groups = components[(i * 4)..].Vector128Count<float>();
            Vector128<float> factors = Vector128.Create(this.divisor.X, this.divisor.Y, this.divisor.Z, this.divisor.W);

            for (nuint group = 0; group < groups; group++, i++)
            {
                ref float first = ref Unsafe.Add(ref words, i * 4);
                (Vector128.LoadUnsafe(ref first) / factors).StoreUnsafe(ref first);
            }
        }

        // Smaller registers consume the wide-register remainder. Without SIMD, normalize every pixel here.
        for (; i < samples.Length; i++)
        {
            samples[i] /= this.divisor;
        }
    }
}
