// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Contains the HEVC quantization scaling matrices and their large-transform DC coefficients.
/// </summary>
internal sealed class HevcScalingList
{
    /// <summary>
    /// The number of matrix identifiers defined for each transform-size category.
    /// </summary>
    private const int MatrixCount = 6;

    /// <summary>
    /// The number of decoded scaling coefficients stored for every transform-size category.
    /// </summary>
    private const int CompactCoefficientCount = (16 * MatrixCount) + (64 * MatrixCount * 3);

    /// <summary>
    /// The number of separately coded DC coefficients.
    /// </summary>
    private const int DcCoefficientCount = 4 * MatrixCount;

    /// <summary>
    /// The first separately coded DC coefficient in the contiguous coefficient store.
    /// </summary>
    private const int DcCoefficientOffset = CompactCoefficientCount;

    /// <summary>
    /// The first transform-sized matrix in the contiguous coefficient store.
    /// </summary>
    private const int ExpandedCoefficientOffset = DcCoefficientOffset + DcCoefficientCount;

    /// <summary>
    /// The number of transform-sized coefficients stored across every size and matrix identifier.
    /// </summary>
    private const int ExpandedCoefficientCount = MatrixCount * ((4 * 4) + (8 * 8) + (16 * 16) + (32 * 32));

    /// <summary>
    /// The compact syntax matrices, separately coded DC values, and transform-sized matrices.
    /// </summary>
    private readonly byte[] coefficients = new byte[ExpandedCoefficientOffset + ExpandedCoefficientCount];

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcScalingList"/> class with the normative default matrices.
    /// </summary>
    public HevcScalingList()
    {
        for (int sizeId = 0; sizeId < 4; sizeId++)
        {
            for (int matrixId = 0; matrixId < MatrixCount; matrixId++)
            {
                ReadOnlySpan<byte> source = sizeId == 0
                    ? Default4x4
                    : matrixId < 3 ? DefaultIntra8x8 : DefaultInter8x8;

                source.CopyTo(this.GetWritableMatrix(sizeId, matrixId));
                this.coefficients[GetDcCoefficientOffset(sizeId, matrixId)] = 16;
            }
        }

        this.ExpandMatrices();
    }

    /// <summary>
    /// Gets the flat default matrix used by four-by-four transforms.
    /// </summary>
    private static ReadOnlySpan<byte> Default4x4 =>
    [
        16, 16, 16, 16,
        16, 16, 16, 16,
        16, 16, 16, 16,
        16, 16, 16, 16,
    ];

    /// <summary>
    /// Gets the default intra-predicted matrix used by transforms of eight-by-eight and larger.
    /// </summary>
    private static ReadOnlySpan<byte> DefaultIntra8x8 =>
    [
        16, 16, 16, 16, 17, 18, 21, 24,
        16, 16, 16, 16, 17, 19, 22, 25,
        16, 16, 17, 18, 20, 22, 25, 29,
        16, 16, 18, 21, 24, 27, 31, 36,
        17, 17, 20, 24, 30, 35, 41, 47,
        18, 19, 22, 27, 35, 44, 54, 65,
        21, 22, 25, 31, 41, 54, 70, 88,
        24, 25, 29, 36, 47, 65, 88, 115,
    ];

    /// <summary>
    /// Gets the default inter-predicted matrix used by transforms of eight-by-eight and larger.
    /// </summary>
    private static ReadOnlySpan<byte> DefaultInter8x8 =>
    [
        16, 16, 16, 16, 17, 18, 20, 24,
        16, 16, 16, 17, 18, 20, 24, 25,
        16, 16, 17, 18, 20, 24, 25, 28,
        16, 17, 18, 20, 24, 25, 28, 33,
        17, 18, 20, 24, 25, 28, 33, 41,
        18, 20, 24, 25, 28, 33, 41, 54,
        20, 24, 25, 28, 33, 41, 54, 71,
        24, 25, 28, 33, 41, 54, 71, 91,
    ];

    /// <summary>
    /// Gets the diagonal coefficient order for four-by-four matrices.
    /// </summary>
    private static ReadOnlySpan<byte> DiagonalScan4x4 =>
    [
        0, 4, 1, 8, 5, 2, 12, 9, 6, 3, 13, 10, 7, 14, 11, 15
    ];

    /// <summary>
    /// Gets the diagonal coefficient order for matrices of eight-by-eight and larger.
    /// </summary>
    private static ReadOnlySpan<byte> DiagonalScan8x8 =>
    [
        0, 8, 1, 16, 9, 2, 24, 17, 10, 3, 32, 25, 18, 11, 4, 40,
        33, 26, 19, 12, 5, 48, 41, 34, 27, 20, 13, 6, 56, 49, 42, 35,
        28, 21, 14, 7, 57, 50, 43, 36, 29, 22, 15, 58, 51, 44, 37, 30,
        23, 59, 52, 45, 38, 31, 60, 53, 46, 39, 61, 54, 47, 62, 55, 63
    ];

    /// <summary>
    /// Reads a complete scaling-list-data structure.
    /// </summary>
    /// <param name="reader">The parameter-set raw byte sequence payload reader.</param>
    /// <returns>The decoded scaling matrices.</returns>
    /// <exception cref="InvalidImageContentException">A prediction reference is outside its permitted matrix set.</exception>
    public static HevcScalingList Parse(ref HevcBitReader reader)
    {
        HevcScalingList scalingList = new();
        for (int sizeId = 0; sizeId < 4; sizeId++)
        {
            int matrixStep = sizeId == 3 ? 3 : 1;
            for (int matrixId = 0; matrixId < MatrixCount; matrixId += matrixStep)
            {
                bool predictionMode = reader.ReadFlag();
                if (!predictionMode)
                {
                    uint matrixIdDelta = reader.ReadUnsignedExpGolomb();
                    if (sizeId == 3)
                    {
                        if (matrixIdDelta > matrixId / 3)
                        {
                            throw new InvalidImageContentException("The HEVC scaling list references an unavailable matrix.");
                        }

                        matrixIdDelta *= 3;
                    }

                    if (matrixIdDelta > matrixId)
                    {
                        throw new InvalidImageContentException("The HEVC scaling list references an unavailable matrix.");
                    }

                    int referenceMatrixId = matrixId - (int)matrixIdDelta;
                    if (referenceMatrixId != matrixId)
                    {
                        // Span copying uses ImageSharp's runtime-optimized memory path and preserves one scalar
                        // behavior model for these small, infrequently parsed coefficient tables.
                        scalingList.GetMatrix(sizeId, referenceMatrixId).CopyTo(scalingList.GetWritableMatrix(sizeId, matrixId));
                        byte dcCoefficient = scalingList.coefficients[GetDcCoefficientOffset(sizeId, referenceMatrixId)];

                        scalingList.coefficients[GetDcCoefficientOffset(sizeId, matrixId)] = dcCoefficient;
                    }

                    continue;
                }

                int nextCoefficient = 8;
                if (sizeId > 1)
                {
                    nextCoefficient = (int)(((long)reader.ReadSignedExpGolomb() + 8) & 255);
                    scalingList.coefficients[GetDcCoefficientOffset(sizeId, matrixId)] = (byte)(nextCoefficient & 255);
                }

                ReadOnlySpan<byte> scan = sizeId == 0 ? DiagonalScan4x4 : DiagonalScan8x8;
                Span<byte> matrix = scalingList.GetWritableMatrix(sizeId, matrixId);
                for (int coefficient = 0; coefficient < matrix.Length; coefficient++)
                {
                    nextCoefficient = (int)(((long)nextCoefficient + reader.ReadSignedExpGolomb()) & 255);
                    matrix[scan[coefficient]] = (byte)nextCoefficient;
                }
            }

            if (sizeId == 3)
            {
                // HEVC signals only luma matrices at 32x32. Chroma uses the corresponding 16x16 matrices.
                for (int matrixId = 0; matrixId < MatrixCount; matrixId++)
                {
                    if (matrixId is 0 or 3)
                    {
                        continue;
                    }

                    scalingList.GetMatrix(sizeId - 1, matrixId).CopyTo(scalingList.GetWritableMatrix(sizeId, matrixId));
                    scalingList.coefficients[GetDcCoefficientOffset(sizeId, matrixId)] = scalingList.coefficients[GetDcCoefficientOffset(sizeId - 1, matrixId)];
                }
            }
        }

        scalingList.ExpandMatrices();
        return scalingList;
    }

    /// <summary>
    /// Gets a decoded scaling matrix.
    /// </summary>
    /// <param name="sizeId">The transform-size category from zero for 4x4 through three for 32x32.</param>
    /// <param name="matrixId">The prediction and color-component matrix identifier.</param>
    /// <returns>The 16 or 64 decoded scaling coefficients in raster order.</returns>
    public ReadOnlySpan<byte> GetMatrix(int sizeId, int matrixId)
    {
        DebugGuard.MustBeBetweenOrEqualTo(sizeId, 0, 3, nameof(sizeId));
        DebugGuard.MustBeBetweenOrEqualTo(matrixId, 0, MatrixCount - 1, nameof(matrixId));
        int coefficientCount = GetCompactMatrixLength(sizeId);
        return this.coefficients.AsSpan(GetCompactMatrixOffset(sizeId, matrixId), coefficientCount);
    }

    /// <summary>
    /// Gets a scaling matrix expanded to its transform dimensions.
    /// </summary>
    /// <param name="sizeId">The transform-size category from zero for 4x4 through three for 32x32.</param>
    /// <param name="matrixId">The prediction and color-component matrix identifier.</param>
    /// <returns>The transform-sized scaling coefficients in raster order.</returns>
    public ReadOnlySpan<byte> GetExpandedMatrix(int sizeId, int matrixId)
    {
        DebugGuard.MustBeBetweenOrEqualTo(sizeId, 0, 3, nameof(sizeId));
        DebugGuard.MustBeBetweenOrEqualTo(matrixId, 0, MatrixCount - 1, nameof(matrixId));
        int coefficientCount = GetExpandedMatrixLength(sizeId);
        return this.coefficients.AsSpan(GetExpandedMatrixOffset(sizeId, matrixId), coefficientCount);
    }

    /// <summary>
    /// Gets the DC scaling coefficient for a large transform matrix.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <param name="matrixId">The prediction and color-component matrix identifier.</param>
    /// <returns>The decoded DC coefficient.</returns>
    public byte GetDcCoefficient(int sizeId, int matrixId)
    {
        DebugGuard.MustBeBetweenOrEqualTo(sizeId, 0, 3, nameof(sizeId));
        DebugGuard.MustBeBetweenOrEqualTo(matrixId, 0, MatrixCount - 1, nameof(matrixId));
        return this.coefficients[GetDcCoefficientOffset(sizeId, matrixId)];
    }

    /// <summary>
    /// Expands every syntax matrix once so inverse quantization can consume consecutive weights without coordinate division.
    /// </summary>
    private void ExpandMatrices()
    {
        for (int sizeId = 0; sizeId < 4; sizeId++)
        {
            int size = 1 << (sizeId + 2);
            int ratio = Math.Max(1, size >> 3);
            int sourceSide = Math.Min(size, 8);
            for (int matrixId = 0; matrixId < MatrixCount; matrixId++)
            {
                ReadOnlySpan<byte> source = this.GetMatrix(sizeId, matrixId);
                Span<byte> destination = this.coefficients.AsSpan(GetExpandedMatrixOffset(sizeId, matrixId), size * size);
                for (int y = 0; y < size; y++)
                {
                    int sourceRowOffset = (y / ratio) * sourceSide;
                    int destinationRowOffset = y * size;
                    for (int x = 0; x < size; x++)
                    {
                        destination[destinationRowOffset + x] = source[sourceRowOffset + (x / ratio)];
                    }
                }

                if (sizeId > 1)
                {
                    // Sixteen- and thirty-two-point matrices code their DC weight separately from the 8x8 body.
                    destination[0] = this.coefficients[GetDcCoefficientOffset(sizeId, matrixId)];
                }
            }
        }
    }

    /// <summary>
    /// Gets a writable compact syntax matrix.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <param name="matrixId">The matrix identifier.</param>
    /// <returns>The writable compact matrix.</returns>
    private Span<byte> GetWritableMatrix(int sizeId, int matrixId)
        => this.coefficients.AsSpan(GetCompactMatrixOffset(sizeId, matrixId), GetCompactMatrixLength(sizeId));

    /// <summary>
    /// Gets the number of coefficients coded for one syntax matrix.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <returns>The compact coefficient count.</returns>
    private static int GetCompactMatrixLength(int sizeId) => sizeId == 0 ? 16 : 64;

    /// <summary>
    /// Gets the number of coefficients in one transform-sized matrix.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <returns>The expanded coefficient count.</returns>
    private static int GetExpandedMatrixLength(int sizeId) => 1 << ((sizeId + 2) * 2);

    /// <summary>
    /// Gets the compact-matrix offset for a size and matrix identifier.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <param name="matrixId">The matrix identifier.</param>
    /// <returns>The compact-matrix offset.</returns>
    private static int GetCompactMatrixOffset(int sizeId, int matrixId)
    {
        int sizeOffset = sizeId switch
        {
            0 => 0,
            1 => 16 * MatrixCount,
            2 => (16 * MatrixCount) + (64 * MatrixCount),
            _ => (16 * MatrixCount) + (64 * MatrixCount * 2),
        };

        return sizeOffset + (matrixId * GetCompactMatrixLength(sizeId));
    }

    /// <summary>
    /// Gets the expanded-matrix offset for a size and matrix identifier.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <param name="matrixId">The matrix identifier.</param>
    /// <returns>The expanded-matrix offset.</returns>
    private static int GetExpandedMatrixOffset(int sizeId, int matrixId)
    {
        int sizeOffset = sizeId switch
        {
            0 => 0,
            1 => 16 * MatrixCount,
            2 => (16 * MatrixCount) + (64 * MatrixCount),
            _ => (16 * MatrixCount) + (64 * MatrixCount) + (256 * MatrixCount),
        };

        return ExpandedCoefficientOffset + sizeOffset + (matrixId * GetExpandedMatrixLength(sizeId));
    }

    /// <summary>
    /// Gets the separately coded DC-coefficient offset for a size and matrix identifier.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <param name="matrixId">The matrix identifier.</param>
    /// <returns>The DC-coefficient offset.</returns>
    private static int GetDcCoefficientOffset(int sizeId, int matrixId) => DcCoefficientOffset + (sizeId * MatrixCount) + matrixId;
}
