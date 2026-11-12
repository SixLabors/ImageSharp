// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

/// <summary>
/// Generates shared VarDCT quantization weights and applies coefficient bias.
/// </summary>
/// <remarks>
/// Callers supply parameter and output storage. This type does not allocate or retain frame buffers.
/// Matrix generation writes relative weights. Reciprocal conversion prepares the factors used for reconstruction.
/// </remarks>
internal static partial class JxlQuantization
{
    /// <summary>
    /// The seventeen decoder quantization families in libjxl quant_weights.h QuantTable. Transposed strategies can share a family.
    /// </summary>
    public const int QuantizationFamilyCount = 17;

    /// <summary>
    /// The X, Y and B component tables stored for each used quantization family.
    /// </summary>
    public const int MatrixComponentCount = 3;

    /// <summary>
    /// The sixteen radial bands permitted by the four-bit count-minus-one field in native DecodeDctParams.
    /// </summary>
    public const int MaximumDistanceBands = 16;

    /// <summary>
    /// The first secondary-band slot, after three sixteen-value primary-band regions in the reusable parameter buffer.
    /// </summary>
    public const int SecondaryBandsOffset = MatrixComponentCount * MaximumDistanceBands;

    /// <summary>
    /// The first special-weight slot, after both three-component radial-band regions.
    /// </summary>
    public const int SpecialWeightsOffset = 2 * SecondaryBandsOffset;

    /// <summary>
    /// The nine special values per component required by AFV: six frequency weights and three band deltas.
    /// </summary>
    public const int SpecialWeightStride = 9;

    /// <summary>
    /// The inclusive positive weight limit, 1e-8, from native quant_weights.cc kAlmostZero.
    /// It also bounds encoded nonzero denominators and multipliers before table generation.
    /// </summary>
    public const float MinimumQuantizationWeight = 1e-8F;

    /// <summary>
    /// The exclusive 1e8 generated-weight limit from native ComputeQuantTable. Reciprocal decoder factors are prepared only after validation.
    /// </summary>
    public const float MaximumQuantizationWeight = 1e8F;

    /// <summary>
    /// The serialized identity description from native QuantEncoding::Mode. It stores three special weights per component.
    /// </summary>
    public const byte IdentityMatrixMode = 1;

    /// <summary>
    /// The serialized DCT2 description. It stores six special weights per component.
    /// </summary>
    public const byte Dct2MatrixMode = 2;

    /// <summary>
    /// The serialized DCT4 description. It stores two multipliers per component and one radial table.
    /// </summary>
    public const byte Dct4MatrixMode = 3;

    /// <summary>
    /// The serialized DCT4-by-8 description. It stores one multiplier per component and one radial table.
    /// </summary>
    public const byte Dct4X8MatrixMode = 4;

    /// <summary>
    /// The serialized AFV description. It stores special frequency weights and two radial tables.
    /// </summary>
    public const byte AfvMatrixMode = 5;

    /// <summary>
    /// The serialized generic DCT description, generated from one radial table.
    /// </summary>
    public const byte DctMatrixMode = 6;

    /// <summary>
    /// The serialized RAW description, carrying an F16 denominator and three modular integer planes.
    /// </summary>
    public const byte RawMatrixMode = 7;

    /// <summary>
    /// The number of samples on each axis of one coefficient block.
    /// </summary>
    private const int BlockSize = 8;

    /// <summary>
    /// Gets the default unit-coefficient biases for X, Y and B, followed by the nonunit correction numerator.
    /// </summary>
    public static Vector4 DefaultBias => new(1F - 0.05465007330715401F, 1F - 0.07005449891748593F, 1F - 0.049935103337343655F, 0.145F);

    /// <summary>
    /// Gets the default DC reconstruction factors in X, Y and B order.
    /// </summary>
    public static Vector3 DefaultDcFactors => new(1F / 4096F, 1F / 512F, 1F / 256F);

    /// <summary>
    /// Gets the quantization family selected by each validated raw strategy.
    /// </summary>
    public static ReadOnlySpan<byte> QuantizationFamilies =>
    [
        0, 1, 2, 3, 4, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 10, 10, 11, 12, 12, 13, 14, 14, 15, 16, 16
    ];

    /// <summary>
    /// Gets each quantization family's first dimension in eight-sample blocks.
    /// </summary>
    public static ReadOnlySpan<byte> MatrixBlockWidths => [1, 1, 1, 1, 2, 4, 1, 1, 2, 1, 1, 8, 4, 16, 8, 32, 16];

    /// <summary>
    /// Gets each quantization family's second dimension in eight-sample blocks.
    /// </summary>
    public static ReadOnlySpan<byte> MatrixBlockHeights => [1, 1, 1, 1, 2, 4, 2, 4, 4, 1, 1, 8, 8, 16, 16, 32, 32];

    /// <summary>
    /// Selects the matrix-offset entry for a validated raw strategy and component.
    /// </summary>
    /// <param name="strategy">The raw strategy code established by AC metadata parsing.</param>
    /// <param name="component">The X, Y or B component index.</param>
    /// <returns>The index in the retained component-offset table.</returns>
    public static int GetQuantizationMatrixIndex(byte strategy, int component) => (MatrixComponentCount * QuantizationFamilies[strategy]) + component;

    /// <summary>
    /// Expands one format-defined description into reusable parameter storage.
    /// </summary>
    /// <param name="family">The quantization family index.</param>
    /// <param name="parameters">The reusable primary, secondary and special-weight storage.</param>
    /// <returns>The selected mode and radial band counts.</returns>
    public static MatrixDescription GetLibraryMatrix(int family, Span<float> parameters)
    {
        byte mode = LibraryMatrixModes[family];
        int primaryCount = LibraryBandCounts[family];
        int secondaryCount = LibrarySecondaryBandCounts[family];
        ReadOnlySpan<float> source = LibraryMatrixParameters[LibraryParameterOffsets[family]..LibraryParameterOffsets[family + 1]];
        int specialCount = GetSpecialWeightCount(mode);
        int position = 0;
        for (int component = 0; component < MatrixComponentCount; component++)
        {
            source.Slice(position, specialCount).CopyTo(parameters.Slice(SpecialWeightsOffset + (component * SpecialWeightStride), specialCount));
            position += specialCount;
        }

        for (int component = 0; component < MatrixComponentCount; component++)
        {
            source.Slice(position, primaryCount).CopyTo(parameters.Slice(component * MaximumDistanceBands, primaryCount));
            position += primaryCount;
        }

        for (int component = 0; component < MatrixComponentCount; component++)
        {
            source.Slice(position, secondaryCount).CopyTo(parameters.Slice(SecondaryBandsOffset + (component * MaximumDistanceBands), secondaryCount));
            position += secondaryCount;
        }

        return new MatrixDescription(mode, primaryCount, secondaryCount);
    }

    /// <summary>
    /// Preserves zero, scales unit magnitudes, and corrects larger signed coefficients by numerator divided by coefficient.
    /// </summary>
    /// <param name="value">The signed coefficient converted to float.</param>
    /// <param name="unitBias">The component's unit-magnitude bias.</param>
    /// <param name="numerator">The correction numerator for magnitudes of at least two.</param>
    /// <returns>The bias-adjusted coefficient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float AdjustBias(float value, float unitBias, float numerator)
        => MathF.Abs(value) < 2F ? value * unitBias : value - (numerator / value);

    /// <inheritdoc cref="AdjustBias(float, float, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> AdjustBias(Vector128<float> value, float unitBias, float numerator)
    {
        // Compare float magnitudes so int.MinValue cannot overflow an integer absolute value.
        // Zero and unit lanes select the multiplication result; the unused divide-by-zero lanes do not affect output.
        Vector128<float> small = value * Vector128.Create(unitBias);
        Vector128<float> large = value - (Vector128.Create(numerator) / value);
        return Vector128.ConditionalSelect(Vector128.LessThan(Vector128.Abs(value), Vector128.Create(2F)), small, large);
    }

    /// <inheritdoc cref="AdjustBias(float, float, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> AdjustBias(Vector256<float> value, float unitBias, float numerator)
    {
        // Compare float magnitudes so int.MinValue cannot overflow an integer absolute value.
        // Zero and unit lanes select the multiplication result; the unused divide-by-zero lanes do not affect output.
        Vector256<float> small = value * Vector256.Create(unitBias);
        Vector256<float> large = value - (Vector256.Create(numerator) / value);
        return Vector256.ConditionalSelect(Vector256.LessThan(Vector256.Abs(value), Vector256.Create(2F)), small, large);
    }

    /// <inheritdoc cref="AdjustBias(float, float, float)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> AdjustBias(Vector512<float> value, float unitBias, float numerator)
    {
        // Compare float magnitudes so int.MinValue cannot overflow an integer absolute value.
        // Zero and unit lanes select the multiplication result; the unused divide-by-zero lanes do not affect output.
        Vector512<float> small = value * Vector512.Create(unitBias);
        Vector512<float> large = value - (Vector512.Create(numerator) / value);
        return Vector512.ConditionalSelect(Vector512.LessThan(Vector512.Abs(value), Vector512.Create(2F)), small, large);
    }

    /// <summary>
    /// Gets the number of explicitly stored special weights per component for one matrix mode.
    /// </summary>
    /// <param name="mode">The matrix description mode.</param>
    /// <returns>The special-weight count, or zero for a purely radial description.</returns>
    public static int GetSpecialWeightCount(byte mode) => mode switch
    {
        IdentityMatrixMode => 3,
        Dct2MatrixMode => 6,
        Dct4MatrixMode => 2,
        Dct4X8MatrixMode => 1,
        AfvMatrixMode => SpecialWeightStride,
        _ => 0
    };

    /// <summary>
    /// Describes the mode and occupied band slots in the current reusable parameter buffer.
    /// </summary>
    public readonly struct MatrixDescription
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MatrixDescription"/> struct.
        /// </summary>
        /// <param name="mode">The resolved nonlibrary mode.</param>
        /// <param name="bandCount">The primary radial band count.</param>
        /// <param name="secondaryBandCount">The secondary AFV radial band count.</param>
        public MatrixDescription(byte mode, int bandCount, int secondaryBandCount)
        {
            this.Mode = mode;
            this.BandCount = bandCount;
            this.SecondaryBandCount = secondaryBandCount;
        }

        /// <summary>
        /// Gets the resolved description mode.
        /// </summary>
        public byte Mode { get; }

        /// <summary>
        /// Gets the primary radial band count.
        /// </summary>
        public int BandCount { get; }

        /// <summary>
        /// Gets the secondary AFV radial band count.
        /// </summary>
        public int SecondaryBandCount { get; }
    }
}
