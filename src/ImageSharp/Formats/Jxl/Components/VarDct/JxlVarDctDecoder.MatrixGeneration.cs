// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal sealed partial class JxlVarDctDecoder
{
    /// <summary>
    /// Generates one component's positive frequency weights from a seed and multiplicative radial band deltas.
    /// </summary>
    /// <param name="parameters">One to sixteen parameters. The seed includes the factor of sixty-four; deltas are finite half-precision fields.</param>
    /// <param name="rows">The coefficient height, from four through 256.</param>
    /// <param name="columns">The coefficient width, from four through 256.</param>
    /// <param name="destination">The complete row-major weight plane.</param>
    /// <remarks>
    /// A positive delta multiplies the preceding band by one plus that delta.
    /// A nonpositive delta divides the preceding band by one minus that delta.
    /// The matrix parser applies the final permitted weight range and reciprocal after all special-mode adjustments.
    /// </remarks>
    public static void GenerateRadialQuantization(ReadOnlySpan<float> parameters, int rows, int columns, Span<float> destination)
    {
        InlineArray16<float> bandStorage = default;
        InlineArray16<float> logarithmStorage = default;
        Span<float> bands = bandStorage;
        Span<float> logarithms = logarithmStorage;
        PrepareDistanceBands(parameters, bands, logarithms);
        if (parameters.Length == 1)
        {
            // A single encoded band has no interpolation interval. Fill uses the runtime's shared bulk implementation.
            destination.Fill(bands[0]);
            return;
        }

        // Both axes span [0,1]. The diagonal spans sqrt(2); the format's epsilon keeps the last lookup below the final band.
        // Prepare log2(next / current) once per interval. SIMD lanes then need only a gather and a bounded exp2 interpolation.
        float scale = (parameters.Length - 1) / (MathF.Sqrt(2F) + 1e-6F);
        float xScale = scale / (columns - 1);
        float yScale = scale / (rows - 1);
        RadialQuantizationOperator operation = new(bands, logarithms, xScale);
        ref float output = ref MemoryMarshal.GetReference(destination);
        for (int y = 0; y < rows; y++)
        {
            float dy = y * yScale;
            float dySquared = dy * dy;
            int x = 0;

            // Each lane is one horizontal coefficient. Column indices generate the positions without source loads.
            // Descending widths retain complete stores; the scalar tail follows the same formula.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<float> indices = Vector512<float>.Indices;
                nuint count = (columns - x).Vector512Count<float>();
                for (nuint block = 0; block < count; block++, x += Vector512<float>.Count)
                {
                    Vector512<float> value = operation.Invoke(indices + Vector512.Create((float)x), dySquared);
                    value.StoreUnsafe(ref output, (nuint)((y * columns) + x));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<float> indices = Vector256<float>.Indices;
                nuint count = (columns - x).Vector256Count<float>();
                for (nuint block = 0; block < count; block++, x += Vector256<float>.Count)
                {
                    operation.Invoke(indices + Vector256.Create((float)x), dySquared).StoreUnsafe(ref output, (nuint)((y * columns) + x));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<float> indices = Vector128<float>.Indices;
                nuint count = (columns - x).Vector128Count<float>();
                for (nuint block = 0; block < count; block++, x += Vector128<float>.Count)
                {
                    operation.Invoke(indices + Vector128.Create((float)x), dySquared).StoreUnsafe(ref output, (nuint)((y * columns) + x));
                }
            }

            // The same operator handles a short row or a scalar remainder without a different interpolation formula.
            for (; x < columns; x++)
            {
                Unsafe.Add(ref output, (y * columns) + x) = operation.Invoke(x, dySquared);
            }
        }
    }

    /// <summary>
    /// Expands multiplicative band deltas and prepares the logarithm of each adjacent ratio once.
    /// </summary>
    /// <param name="parameters">The seed followed by encoded band deltas.</param>
    /// <param name="bands">Reusable absolute-weight slots for the supplied parameter count.</param>
    /// <param name="logarithms">Reusable adjacent-ratio slots for the supplied parameter count.</param>
    private static void PrepareDistanceBands(ReadOnlySpan<float> parameters, Span<float> bands, Span<float> logarithms)
    {
        bands[0] = parameters[0];
        for (int i = 1; i < parameters.Length; i++)
        {
            float delta = parameters[i];
            bands[i] = bands[i - 1] * (delta > 0F ? 1F + delta : 1F / (1F - delta));
        }

        // A finite half-precision delta has magnitude at most 65504. Each adjacent factor is therefore between
        // 1/65505 and 65505. Positive finite bands retain that ratio in float, so interpolation exponents stay within (-16,16).
        for (int i = 0; i < parameters.Length; i++)
        {
            if (bands[i] < MinimumQuantizationWeight || !float.IsFinite(bands[i]))
            {
                throw new InvalidImageContentException("The JPEG XL quantization distance bands are invalid.");
            }

            if (i + 1 < parameters.Length)
            {
                logarithms[i] = MathF.Log2(bands[i + 1] / bands[i]);
            }
        }
    }

    /// <summary>
    /// Expands a resolved matrix description into its three component weight planes before reciprocal conversion.
    /// </summary>
    /// <param name="description">The resolved non-RAW description.</param>
    /// <param name="parameters">The current primary bands, secondary bands and special weights.</param>
    /// <param name="width">The family's first coefficient dimension.</param>
    /// <param name="height">The family's second coefficient dimension.</param>
    /// <param name="destination">Three contiguous coefficient planes.</param>
    /// <param name="cancellationToken">The token checked between component planes.</param>
    private static void GenerateMatrix(
        MatrixDescription description, ReadOnlySpan<float> parameters, int width, int height, Span<float> destination, CancellationToken cancellationToken)
    {
        int area = width * height;
        InlineArray64<float> smallStorage = default;
        Span<float> small = smallStorage;
        for (int component = 0; component < MatrixComponentCount; component++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Span<float> output = destination.Slice(component * area, area);
            ReadOnlySpan<float> special = parameters.Slice(SpecialWeightsOffset + (component * SpecialWeightStride), SpecialWeightStride);
            ReadOnlySpan<float> primary = parameters.Slice(component * MaximumDistanceBands, description.BandCount);
            switch (description.Mode)
            {
                case IdentityMatrixMode:
                    output.Fill(special[0]);
                    output[1] = output[BlockSize] = special[1];
                    output[BlockSize + 1] = special[2];
                    break;
                case Dct2MatrixMode:
                    // DCT2 combines nested two-by-two transforms. Its six weights cover the expanding frequency quadrants.
                    // The DC slot is unused by AC reconstruction; the format-defined library uses the finite sentinel 0xBAD.
                    output[0] = 0xBAD;
                    output[1] = output[BlockSize] = special[0];
                    output[BlockSize + 1] = special[1];
                    for (int y = 0; y < BlockSize; y++)
                    {
                        for (int x = 0; x < BlockSize; x++)
                        {
                            if (x >= 4 || y >= 4)
                            {
                                output[(y * BlockSize) + x] = special[x >= 4 && y >= 4 ? 5 : 4];
                            }
                            else if (x >= 2 || y >= 2)
                            {
                                output[(y * BlockSize) + x] = special[x >= 2 && y >= 2 ? 3 : 2];
                            }
                        }
                    }

                    break;
                case Dct4MatrixMode:
                    GenerateRadialQuantization(primary, 4, 4, small[..16]);
                    for (int y = 0; y < BlockSize; y++)
                    {
                        for (int x = 0; x < BlockSize; x++)
                        {
                            output[(y * BlockSize) + x] = small[((y >> 1) * 4) + (x >> 1)];
                        }
                    }

                    output[1] /= special[0];
                    output[BlockSize] /= special[0];
                    output[BlockSize + 1] /= special[1];
                    break;
                case Dct4X8MatrixMode:
                    GenerateRadialQuantization(primary, 4, BlockSize, small[..32]);
                    for (int y = 0; y < BlockSize; y++)
                    {
                        small.Slice((y >> 1) * BlockSize, BlockSize).CopyTo(output.Slice(y * BlockSize, BlockSize));
                    }

                    output[BlockSize] /= special[0];
                    break;
                case AfvMatrixMode:
                    ReadOnlySpan<float> secondary = parameters.Slice(SecondaryBandsOffset + (component * MaximumDistanceBands), description.SecondaryBandCount);
                    GenerateRadialQuantization(primary, 4, BlockSize, small[..32]);
                    GenerateRadialQuantization(secondary, 4, 4, small.Slice(32, 16));
                    GenerateAfvWeights(special, small[..32], small.Slice(32, 16), output);
                    break;
                default:
                    // Coefficient layout stores a rectangular transform with its longer axis contiguous.
                    // RAW descriptions preserve their encoded flattened order instead and do not enter this radial path.
                    GenerateRadialQuantization(primary, width, height, output);
                    break;
            }
        }
    }

    /// <summary>
    /// Places the sparse AFV corner weights and the interleaved four-by-eight and four-by-four frequency weights.
    /// </summary>
    /// <param name="special">Nine special AFV parameters, with the first six already expanded to weight units.</param>
    /// <param name="weights4X8">The primary four-by-eight radial plane.</param>
    /// <param name="weights4X4">The secondary four-by-four radial plane.</param>
    /// <param name="destination">The eight-by-eight component weight plane.</param>
    private static void GenerateAfvWeights(ReadOnlySpan<float> special, ReadOnlySpan<float> weights4X8, ReadOnlySpan<float> weights4X4, Span<float> destination)
    {
        ReadOnlySpan<float> frequencies =
        [
            0F, 0F, 0.8517778890324296F, 5.37778436506804F,
            0F, 0F, 4.734747904497923F, 5.449245381693219F,
            1.6598270267479331F, 4F, 7.275749096817861F, 10.423227632456525F,
            2.662932286148962F, 7.630657783650829F, 8.962388608184032F, 12.97166202570235F
        ];

        InlineArray4<float> bandStorage = default;
        InlineArray4<float> logarithmStorage = default;
        Span<float> bands = bandStorage;
        Span<float> logarithms = logarithmStorage;
        PrepareDistanceBands(special[5..], bands, logarithms);
        const float lowFrequency = 0.8517778890324296F;
        const float frequencyExtent = 12.97166202570235F - lowFrequency + 1e-6F;
        RadialQuantizationOperator operation = new(bands, logarithms, 1F);

        // AFV's corner has five explicitly weighted coefficients. Twelve other even/even coefficients use the
        // format-defined sparse frequency table; the remaining positions interleave the two ordinary DCT planes.
        destination[0] = 1F;
        destination[BlockSize] = special[0];
        destination[1] = special[1];
        destination[2 * BlockSize] = special[2];
        destination[2] = special[3];
        destination[(2 * BlockSize) + 2] = special[4];
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                if (x >= 2 || y >= 2)
                {
                    float position = (frequencies[(y * 4) + x] - lowFrequency) * 3F / frequencyExtent;
                    destination[(2 * y * BlockSize) + (2 * x)] = operation.Interpolate(position);
                }

                if (x != 0 || y != 0)
                {
                    destination[(2 * y * BlockSize) + (2 * x) + 1] = weights4X4[(y * 4) + x];
                }
            }

            for (int x = y == 0 ? 1 : 0; x < BlockSize; x++)
            {
                destination[(((2 * y) + 1) * BlockSize) + x] = weights4X8[(y * BlockSize) + x];
            }
        }
    }
}
