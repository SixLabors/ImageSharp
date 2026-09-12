// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

/// <summary>
/// Contains matrices used to inverse quantize coefficients.
/// </summary>
internal sealed class JxlDequantMatrices
{
    /// <summary>
    /// Sum(DotProduct(RequiredSizeX, RequiredSizeY)).
    /// </summary>
    private const int SumRequiredXY = 2056;

    private const int TotalTableSize = SumRequiredXY * JxlFrameDimensions.DctBlockSize * 3;

    /// <summary>
    /// Contains weights &amp; multipliers for transforms used by the codec (e.g. DCT, identity, AFV).
    /// </summary>
    public static readonly JxlQuantizerEncoding[] Library = GetLibrary();

    private uint computedMask;

    /// <summary>
    /// Storage for quantization.
    /// </summary>
    private readonly Memory<byte> tableStorage;

    /// <summary>
    /// Contains matrices for forward quantization.
    /// </summary>
    private readonly Memory<float> table;

    /// <summary>
    /// Contains matrices for inverse quantization.
    /// </summary>
    private readonly Memory<float> inverseTable;

    /// <summary>
    /// Quantization table for DC
    /// </summary>
    private InlineArray3<float> dcQuant;

    /// <summary>
    /// Inverse quantization table for DC
    /// </summary>
    private InlineArray3<float> inverseDcQuant;

    /// <summary>
    /// Table offsets.
    /// </summary>
    private readonly int[] tableOffsets = new int[JxlAcStrategy.NumberOfValidStrategies * 3];

    /// <summary>
    /// Quantizer encodings. Multiple may be used depending on the kind of transform.
    /// </summary>
    private JxlQuantizerEncoding[] encodings = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlDequantMatrices"/> class.
    /// </summary>
    public JxlDequantMatrices()
    {
        // float dc_quant_[3] = {kDCQuant[0], kDCQuant[1], kDCQuant[2]};
        // float inv_dc_quant_[3] = {kInvDCQuant[0], kInvDCQuant[1], kInvDCQuant[2]};
        this.dcQuant[0] = JxlQuantizerConstants.DcQuant[0];
        this.dcQuant[1] = JxlQuantizerConstants.DcQuant[1];
        this.dcQuant[2] = JxlQuantizerConstants.DcQuant[2];

        this.inverseDcQuant[0] = JxlQuantizerConstants.InverseDcQuant[0];
        this.inverseDcQuant[1] = JxlQuantizerConstants.InverseDcQuant[1];
        this.inverseDcQuant[2] = JxlQuantizerConstants.InverseDcQuant[2];

        this.encodings = new JxlQuantizerEncoding[JxlQuantizerConstants.NumberOfQuantizerTables];
        for (int i = 0; i < this.encodings.Length; i++)
        {
            this.encodings[i] = JxlQuantizerEncoding.Library(0);
        }

        int pos = 0;
        Span<int> offsets = stackalloc int[JxlQuantizerConstants.NumberOfQuantizerTables * 3];

        for (int i = 0; i < JxlQuantizerConstants.NumberOfQuantizerTables; i++)
        {
            int numBlocks = RequiredSizeX[i] * RequiredSizeY[i];
            int num = numBlocks * JxlFrameDimensions.DctBlockSize;
            int i3 = 3 * i;

            for (int c = 0; c < 3; c++)
            {
                offsets[i3 + c] = pos + (c * num);
            }

            pos += 3 * num;
        }

        for (int i = 0; i < JxlAcStrategy.NumberOfValidStrategies; i++)
        {
            for (int c = 0; c < 3; c++)
            {
                this.tableOffsets[(i * 3) + c] = offsets[((int)JxlQuantizerConstants.AcStrategyToQuantTableMap[i] * 3) + c];
            }
        }
    }

    /// <summary>
    /// Gets a lookup which represents required widths for each quantizer.
    /// </summary>
    public static ReadOnlySpan<int> RequiredSizeX => [1, 1, 1, 1, 2, 4, 1, 1, 2, 1, 1, 8, 4, 16, 8, 32, 16];

    /// <summary>
    /// Gets a lookup which represents required heights for each quantizer.
    /// </summary>
    public static ReadOnlySpan<int> RequiredSizeY => [1, 1, 1, 1, 2, 4, 2, 4, 4, 1, 1, 8, 8, 16, 16, 32, 32];

    /// <summary>
    /// Returns the default library with quantizer encodings for all transforms
    /// used by the JPEG XL codec.
    /// </summary>
    /// <returns>Encodings for all kinds of transforms.</returns>
    /// <exception cref="InvalidOperationException">Used when quantization constants were partially updated.</exception>
    public static JxlQuantizerEncoding[] GetLibrary()
    {
        if (JxlQuantizerConstants.NumberOfQuantizerTables != 17)
        {
            throw new InvalidOperationException("This function should be updated when adding new quantization types");
        }

        if (JxlQuantWeights.NumPredefinedTables != 1)
        {
            throw new InvalidOperationException("This function should be updated when adding new quantization matrices to the library");
        }

        Verify(0, JxlQuantTable.DCT);
        Verify(1, JxlQuantTable.IDENTITY);
        Verify(2, JxlQuantTable.DCT2X2);
        Verify(3, JxlQuantTable.DCT4X4);
        Verify(4, JxlQuantTable.DCT16X16);
        Verify(5, JxlQuantTable.DCT32X32);
        Verify(6, JxlQuantTable.DCT8X16);
        Verify(7, JxlQuantTable.DCT8X32);
        Verify(8, JxlQuantTable.DCT16X32);
        Verify(9, JxlQuantTable.DCT4X8);
        Verify(10, JxlQuantTable.AFV0);
        Verify(11, JxlQuantTable.DCT64X64);
        Verify(12, JxlQuantTable.DCT32X64);
        Verify(13, JxlQuantTable.DCT128X128);
        Verify(14, JxlQuantTable.DCT64X128);
        Verify(15, JxlQuantTable.DCT256X256);
        Verify(16, JxlQuantTable.DCT128X256);

        return
        [
            JxlQuantWeights.Dct,
            JxlQuantWeights.Identity,
            JxlQuantWeights.Dct2x2,
            JxlQuantWeights.Dct4x4,
            JxlQuantWeights.Dct16x16,
            JxlQuantWeights.Dct32x32,
            JxlQuantWeights.Dct8x16,
            JxlQuantWeights.Dct8x32,
            JxlQuantWeights.Dct16x32,
            JxlQuantWeights.Dct4x8,
            JxlQuantWeights.Afv,
            JxlQuantWeights.Dct64x64,
            JxlQuantWeights.Dct32x32,
            JxlQuantWeights.Dct128x128,
            JxlQuantWeights.Dct64x128,
            JxlQuantWeights.Dct256x256,
            JxlQuantWeights.Dct128x256
        ];

        [Conditional("DEBUG")]
        static void Verify(int expected, JxlQuantTable actual)
        {
            if (expected != (byte)actual)
            {
                throw new InvalidOperationException("Quantizer modes were partially updated; this method needs to be updated too");
            }
        }
    }

    /// <summary>
    /// Returns a matrix for the specified kind of quantizer and index.
    /// </summary>
    /// <param name="quantKind">Quantizer kind</param>
    /// <param name="c">Index</param>
    /// <returns>Matrix</returns>
    public Span<float> GetMatrix(JxlAcStrategyType quantKind, int c)
    {
        DebugGuard.MustBeGreaterThan((1 << (int)quantKind) & this.computedMask, 0, nameof(quantKind));
        return this.table.Span[this.tableOffsets[((int)quantKind * 3) + c]..];
    }

    /// <summary>
    /// Returns an inverse matrix for the specified kind of quantizer and index.
    /// </summary>
    /// <param name="quantKind">Quantizer kind</param>
    /// <param name="c">Index</param>
    /// <returns>Inverse matrix</returns>
    public Span<float> GetInverseMatrix(JxlAcStrategyType quantKind, int c)
    {
        DebugGuard.MustBeGreaterThan((1 << (int)quantKind) & this.computedMask, 0, nameof(quantKind));
        return this.inverseTable.Span[this.tableOffsets[((int)quantKind * 3) + c]..];
    }

    /// <summary>
    /// Returns a DC quant for index c.
    /// </summary>
    /// <param name="c">The DC quantizer index.</param>
    /// <returns>DC quant for index <paramref name="c"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetDcQuant(int c) => this.dcQuant[c];

    /// <summary>
    /// Returns all DC quantizers. See also <seealso cref="GetDcQuant(int)"/>.
    /// </summary>
    /// <returns>Span that covers all DC quantizers.</returns>
    public Span<float> GetDcQuants() => this.dcQuant;

    /// <summary>
    /// Returns an inverse DC quant for index c.
    /// </summary>
    /// <param name="c">The inverse DC quantizer index.</param>
    /// <returns>Inverse DC quant for index <paramref name="c"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetInverseDcQuant(int c) => this.inverseDcQuant[c];

    /// <summary>
    /// Applies the specified DC quantizer.
    /// </summary>
    /// <param name="dc">DC quantizer to apply to the dequantization matrices.</param>
    public void SetDcQuant(InlineArray3<float> dc)
    {
        for (int c = 0; c < 3; c++)
        {
            this.dcQuant[c] = 1f / dc[c];
            this.inverseDcQuant[c] = dc[c];
        }
    }

    /// <summary>
    /// Sets custom quantizer encodings for transform functions.
    /// </summary>
    /// <param name="encodings">The encodings to identify required transform functions.</param>
    public void SetEncodings(JxlQuantizerEncoding[] encodings)
    {
        this.encodings = encodings;
        this.computedMask = 0;
    }

    /// <summary>
    /// Returns quantizer encodings for this dequant matrices instance.
    /// </summary>
    /// <returns>
    /// Encodings set by the <see cref="SetEncodings(JxlQuantizerEncoding[])"/> method.
    /// By default (when the aforementioned method wasn't invoked), the result
    /// is simply an empty span.
    /// </returns>
    public Span<JxlQuantizerEncoding> GetEncodings() => this.encodings;
}
