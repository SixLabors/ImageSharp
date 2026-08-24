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
    /// The flat default matrix used by four-by-four transforms.
    /// </summary>
    private static readonly byte[] Default4x4 =
    [
        16, 16, 16, 16,
        16, 16, 16, 16,
        16, 16, 16, 16,
        16, 16, 16, 16,
    ];

    /// <summary>
    /// The default intra-predicted matrix used by transforms of eight-by-eight and larger.
    /// </summary>
    private static readonly byte[] DefaultIntra8x8 =
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
    /// The default inter-predicted matrix used by transforms of eight-by-eight and larger.
    /// </summary>
    private static readonly byte[] DefaultInter8x8 =
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
    /// The diagonal coefficient order for four-by-four matrices.
    /// </summary>
    private static readonly byte[] DiagonalScan4x4 = CreateDiagonalScan(4);

    /// <summary>
    /// The diagonal coefficient order for matrices of eight-by-eight and larger.
    /// </summary>
    private static readonly byte[] DiagonalScan8x8 = CreateDiagonalScan(8);

    /// <summary>
    /// The decoded matrices, indexed by transform-size category and matrix identifier.
    /// </summary>
    private readonly byte[][] matrices = new byte[4 * MatrixCount][];

    /// <summary>
    /// The DC coefficients for sixteen-by-sixteen and thirty-two-by-thirty-two matrices.
    /// </summary>
    private readonly byte[] dcCoefficients = new byte[4 * MatrixCount];

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcScalingList"/> class with the normative default matrices.
    /// </summary>
    public HevcScalingList()
    {
        for (int sizeId = 0; sizeId < 4; sizeId++)
        {
            int coefficientCount = sizeId == 0 ? 16 : 64;
            for (int matrixId = 0; matrixId < MatrixCount; matrixId++)
            {
                byte[] matrix = new byte[coefficientCount];
                ReadOnlySpan<byte> source = sizeId == 0
                    ? Default4x4
                    : matrixId < 3 ? DefaultIntra8x8 : DefaultInter8x8;

                source.CopyTo(matrix);
                this.matrices[GetIndex(sizeId, matrixId)] = matrix;
                this.dcCoefficients[GetIndex(sizeId, matrixId)] = 16;
            }
        }
    }

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
                int destinationIndex = GetIndex(sizeId, matrixId);
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
                        int referenceIndex = GetIndex(sizeId, referenceMatrixId);

                        // Span copying uses ImageSharp's runtime-optimized memory path and preserves one scalar
                        // behavior model for these small, infrequently parsed coefficient tables.
                        scalingList.matrices[referenceIndex].CopyTo(scalingList.matrices[destinationIndex], 0);
                        scalingList.dcCoefficients[destinationIndex] = scalingList.dcCoefficients[referenceIndex];
                    }

                    continue;
                }

                int nextCoefficient = 8;
                if (sizeId > 1)
                {
                    nextCoefficient = (int)(((long)reader.ReadSignedExpGolomb() + 8) & 255);
                    scalingList.dcCoefficients[destinationIndex] = (byte)(nextCoefficient & 255);
                }

                byte[] scan = sizeId == 0 ? DiagonalScan4x4 : DiagonalScan8x8;
                byte[] matrix = scalingList.matrices[destinationIndex];
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

                    int destinationIndex = GetIndex(sizeId, matrixId);
                    int referenceIndex = GetIndex(sizeId - 1, matrixId);
                    scalingList.matrices[referenceIndex].CopyTo(scalingList.matrices[destinationIndex], 0);
                    scalingList.dcCoefficients[destinationIndex] = scalingList.dcCoefficients[referenceIndex];
                }
            }
        }

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
        return this.matrices[GetIndex(sizeId, matrixId)];
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
        return this.dcCoefficients[GetIndex(sizeId, matrixId)];
    }

    /// <summary>
    /// Gets the flattened storage index for a size and matrix identifier.
    /// </summary>
    /// <param name="sizeId">The transform-size category.</param>
    /// <param name="matrixId">The matrix identifier.</param>
    /// <returns>The flattened storage index.</returns>
    private static int GetIndex(int sizeId, int matrixId) => (sizeId * MatrixCount) + matrixId;

    /// <summary>
    /// Creates the HEVC up-right diagonal scan for a square coefficient block.
    /// </summary>
    /// <param name="size">The coefficient block width and height.</param>
    /// <returns>The raster indices in coded order.</returns>
    private static byte[] CreateDiagonalScan(int size)
    {
        byte[] scan = new byte[size * size];
        int row = 0;
        int column = 0;
        for (int position = 0; position < scan.Length; position++)
        {
            scan[position] = (byte)((row * size) + column);
            if (column == size - 1 || row == 0)
            {
                row += column + 1;
                column = 0;
                if (row >= size)
                {
                    column += row - (size - 1);
                    row = size - 1;
                }
            }
            else
            {
                column++;
                row--;
            }
        }

        return scan;
    }
}
