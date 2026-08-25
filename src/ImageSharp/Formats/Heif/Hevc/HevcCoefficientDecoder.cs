// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Decodes HEVC transform coefficients while retaining entropy-substream Rice state and reusable scratch storage.
/// </summary>
internal sealed class HevcCoefficientDecoder : IDisposable
{
    /// <summary>
    /// The maximum coefficient count in a 32 by 32 transform block.
    /// </summary>
    private const int MaximumCoefficientCount = 32 * 32;

    /// <summary>
    /// The maximum number of 4 by 4 coefficient groups in a transform block.
    /// </summary>
    private const int MaximumCoefficientGroupCount = MaximumCoefficientCount / 16;

    /// <summary>
    /// The maximum number of significant coefficients in one coefficient group.
    /// </summary>
    private const int CoefficientsPerGroup = 16;

    /// <summary>
    /// The maximum number of greater-than-one flags coded in one coefficient group.
    /// </summary>
    private const int GreaterThanOneFlagCount = 8;

    /// <summary>
    /// The minimum scan-position separation that enables sign-data hiding.
    /// </summary>
    private const int SignDataHidingThreshold = 4;

    /// <summary>
    /// The divisor that converts a persistent adaptation statistic to its Rice parameter.
    /// </summary>
    private const int RiceAdaptationDivisor = 4;

    /// <summary>
    /// The first scratch index occupied by coefficient-group significance flags.
    /// </summary>
    private const int CoefficientGroupFlagsOffset = MaximumCoefficientCount;

    /// <summary>
    /// The first scratch index occupied by significant coefficient raster positions.
    /// </summary>
    private const int CoefficientPositionsOffset = CoefficientGroupFlagsOffset + MaximumCoefficientGroupCount;

    /// <summary>
    /// The first scratch index occupied by absolute coefficient levels.
    /// </summary>
    private const int AbsoluteLevelsOffset = CoefficientPositionsOffset + CoefficientsPerGroup;

    /// <summary>
    /// The total number of pooled integers used by coefficient decoding.
    /// </summary>
    private const int ScratchLength = AbsoluteLevelsOffset + CoefficientsPerGroup;

    /// <summary>
    /// The allocator-owned scan and coefficient-group working storage reused for every transform block.
    /// </summary>
    private readonly IMemoryOwner<int> scratchOwner;

    /// <summary>
    /// The persistent Rice statistics for transformed and non-transformed luma and chroma blocks.
    /// </summary>
    private InlineArray4<int> riceAdaptationStatistics;

    /// <summary>
    /// Initializes a new instance of the <see cref="HevcCoefficientDecoder"/> class for one entropy substream.
    /// </summary>
    /// <param name="configuration">The configuration providing pooled codec memory.</param>
    public HevcCoefficientDecoder(Configuration configuration)
    {
        this.scratchOwner = configuration.MemoryAllocator.Allocate<int>(ScratchLength);
        this.riceAdaptationStatistics = default;
    }

    /// <summary>
    /// Gets the minimum coordinate represented by each last-significant prefix.
    /// </summary>
    private static ReadOnlySpan<byte> MinimumCoordinateInGroup => [0, 1, 2, 3, 4, 6, 8, 12, 16, 24];

    /// <summary>
    /// Gets the last-significant prefix selected by each transform coordinate.
    /// </summary>
    private static ReadOnlySpan<byte> CoordinateGroupIndex =>
    [
        0, 1, 2, 3, 4, 4, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7,
        8, 8, 8, 8, 8, 8, 8, 8, 9, 9, 9, 9, 9, 9, 9, 9,
    ];

    /// <summary>
    /// Clears all persistent Rice adaptation statistics for a newly initialized entropy substream.
    /// </summary>
    public void ResetRiceAdaptation() => this.riceAdaptationStatistics = default;

    /// <summary>
    /// Copies the four persistent Rice adaptation statistics to caller-owned wavefront state.
    /// </summary>
    /// <param name="destination">The four-element destination.</param>
    public void CopyRiceAdaptationTo(Span<int> destination) => this.riceAdaptationStatistics[..4].CopyTo(destination);

    /// <summary>
    /// Restores the four persistent Rice adaptation statistics captured for a later wavefront row.
    /// </summary>
    /// <param name="source">The four saved statistics.</param>
    public void CopyRiceAdaptationFrom(ReadOnlySpan<int> source) => source[..4].CopyTo(this.riceAdaptationStatistics[..4]);

    /// <summary>
    /// Decodes one transform block into raster-ordered signed coefficient levels.
    /// </summary>
    /// <param name="reader">The current entropy-substream syntax reader.</param>
    /// <param name="coefficients">The destination coefficient block.</param>
    /// <param name="parameters">The transform-block coefficient coding parameters.</param>
    /// <returns>The number of nonzero coefficients decoded into <paramref name="coefficients"/>.</returns>
    public int Decode(ref HevcCabacSyntaxReader reader, Span<int> coefficients, in HevcCoefficientCodingParameters parameters)
    {
        int width = parameters.Width;
        int height = parameters.Height;
        int coefficientCount = width * height;
        bool isChroma = parameters.Plane != HevcPlane.Y;
        coefficients[..coefficientCount].Clear();

        ReadLastSignificantPosition(ref reader, in parameters, out int lastX, out int lastY);
        int lastRasterPosition = (lastY * width) + lastX;
        Span<int> scratch = this.scratchOwner.Memory.Span;
        Span<int> scan = scratch[..coefficientCount];
        int lastScanPosition = HevcCoefficientScanOrder.Write(scan, width, height, parameters.ScanType, lastRasterPosition);
        int groupCount = coefficientCount / CoefficientsPerGroup;
        Span<int> significantGroupFlags = scratch.Slice(CoefficientGroupFlagsOffset, groupCount);
        Span<int> positions = scratch.Slice(CoefficientPositionsOffset, CoefficientsPerGroup);
        Span<int> absoluteLevels = scratch.Slice(AbsoluteLevelsOffset, CoefficientsPerGroup);
        significantGroupFlags.Clear();

        int widthInGroups = width / 4;
        int lastSubset = lastScanPosition / CoefficientsPerGroup;
        int significantScanPosition = lastScanPosition;
        int c1 = 1;
        int totalNonZero = 0;
        ref int currentRiceStatistic = ref this.riceAdaptationStatistics[parameters.RiceStatisticsIndex];

        // Coefficient groups are decoded from the last significant position toward DC. This direction makes the
        // already decoded right and lower groups available to the significance-context derivation below.
        for (int subset = lastSubset; subset >= 0; subset--)
        {
            int subsetStart = subset * CoefficientsPerGroup;
            int riceParameter = currentRiceStatistic / RiceAdaptationDivisor;
            bool updateRiceStatistic = parameters.PersistentRiceAdaptationEnabled;
            int nonZeroCount = 0;
            int lastNonZeroScanPosition = -1;
            int firstNonZeroScanPosition = CoefficientsPerGroup;
            bool escapeDataPresent = false;

            if (significantScanPosition == lastScanPosition)
            {
                lastNonZeroScanPosition = significantScanPosition;
                firstNonZeroScanPosition = significantScanPosition;
                significantScanPosition--;
                positions[0] = lastRasterPosition;
                nonZeroCount = 1;
            }

            int groupRasterPosition = scan[subsetStart];
            int groupY = (groupRasterPosition / width) / 4;
            int groupX = (groupRasterPosition % width) / 4;
            int groupIndex = (groupY * widthInGroups) + groupX;
            if (subset == lastSubset || subset == 0)
            {
                significantGroupFlags[groupIndex] = 1;
            }
            else
            {
                int groupContext = parameters.GetSignificantGroupContext(significantGroupFlags, groupX, groupY);
                significantGroupFlags[groupIndex] = reader.ReadSignificantCoefficientGroup(isChroma, groupContext) ? 1 : 0;
            }

            int significancePattern = parameters.GetSignificancePattern(significantGroupFlags, groupX, groupY);
            for (; significantScanPosition >= subsetStart; significantScanPosition--)
            {
                int rasterPosition = scan[significantScanPosition];
                bool isSignificant = false;
                if (significantGroupFlags[groupIndex] != 0)
                {
                    if (significantScanPosition > subsetStart || subset == 0 || nonZeroCount != 0)
                    {
                        int contextIndex = parameters.GetSignificantCoefficientContext(rasterPosition, significancePattern);
                        isSignificant = reader.ReadSignificantCoefficient(isChroma, contextIndex);
                    }
                    else
                    {
                        // A coded significant group must contain at least one coefficient. When every later flag is
                        // zero, the first scan position is therefore inferred rather than consuming another CABAC bin.
                        isSignificant = true;
                    }
                }

                if (isSignificant)
                {
                    positions[nonZeroCount++] = rasterPosition;
                    if (lastNonZeroScanPosition < 0)
                    {
                        lastNonZeroScanPosition = significantScanPosition;
                    }

                    firstNonZeroScanPosition = significantScanPosition;
                }
            }

            if (nonZeroCount == 0)
            {
                continue;
            }

            bool hideSign = lastNonZeroScanPosition - firstNonZeroScanPosition >= SignDataHidingThreshold;
            int contextSet = parameters.GetLevelContextSet(subset, c1 == 0);
            c1 = 1;
            absoluteLevels[..nonZeroCount].Fill(1);
            int greaterThanOneCount = Math.Min(nonZeroCount, GreaterThanOneFlagCount);
            int firstGreaterThanOneIndex = -1;

            for (int index = 0; index < greaterThanOneCount; index++)
            {
                bool greaterThanOne = reader.ReadCoefficientGreaterThanOne(isChroma, (contextSet * 4) + c1);
                if (greaterThanOne)
                {
                    c1 = 0;
                    if (firstGreaterThanOneIndex < 0)
                    {
                        firstGreaterThanOneIndex = index;
                    }
                    else
                    {
                        escapeDataPresent = true;
                    }
                }
                else if (c1 is > 0 and < 3)
                {
                    c1++;
                }

                absoluteLevels[index] = greaterThanOne ? 2 : 1;
            }

            if (c1 == 0 && firstGreaterThanOneIndex >= 0)
            {
                bool greaterThanTwo = reader.ReadCoefficientGreaterThanTwo(isChroma, contextSet);
                absoluteLevels[firstGreaterThanOneIndex] = greaterThanTwo ? 3 : 2;
                escapeDataPresent |= greaterThanTwo;
            }

            escapeDataPresent |= nonZeroCount > GreaterThanOneFlagCount;
            if (escapeDataPresent && parameters.CabacBypassAlignmentEnabled)
            {
                reader.AlignBypass();
            }

            int signCount = hideSign && parameters.SignDataHidingEnabled ? nonZeroCount - 1 : nonZeroCount;
            uint coefficientSigns = reader.ReadBypassBits(signCount);
            int nextSignBit = signCount - 1;
            int firstCoefficientAtLeastTwo = 1;
            if (escapeDataPresent)
            {
                for (int index = 0; index < nonZeroCount; index++)
                {
                    int baseLevel = index < GreaterThanOneFlagCount ? 2 + firstCoefficientAtLeastTwo : 1;
                    if (absoluteLevels[index] == baseLevel)
                    {
                        uint remainder = reader.ReadCoefficientRemaining(
                            riceParameter,
                            parameters.ExtendedPrecisionProcessingEnabled,
                            parameters.MaximumLog2TransformDynamicRange);

                        ulong decodedLevel = (ulong)remainder + (uint)baseLevel;
                        if (decodedLevel > int.MaxValue)
                        {
                            throw new InvalidImageContentException("The HEVC transform coefficient level is too large.");
                        }

                        absoluteLevels[index] = (int)decodedLevel;
                        if (decodedLevel > (3UL << riceParameter))
                        {
                            riceParameter = parameters.PersistentRiceAdaptationEnabled ? riceParameter + 1 : Math.Min(riceParameter + 1, 4);
                        }

                        if (updateRiceStatistic)
                        {
                            int initialRiceParameter = currentRiceStatistic / RiceAdaptationDivisor;
                            if (remainder >= (3UL << initialRiceParameter))
                            {
                                currentRiceStatistic++;
                            }
                            else if (((ulong)remainder * 2) < (1UL << initialRiceParameter) && currentRiceStatistic > 0)
                            {
                                currentRiceStatistic--;
                            }

                            // Only the first escape value in a coefficient group updates persistent state.
                            updateRiceStatistic = false;
                        }
                    }

                    if (absoluteLevels[index] >= 2)
                    {
                        firstCoefficientAtLeastTwo = 0;
                    }
                }
            }

            int absoluteSum = 0;
            for (int index = 0; index < nonZeroCount; index++)
            {
                int rasterPosition = positions[index];
                int level = absoluteLevels[index];
                absoluteSum += level;
                if (index == nonZeroCount - 1 && hideSign && parameters.SignDataHidingEnabled)
                {
                    level = (absoluteSum & 1) == 0 ? level : -level;
                }
                else if (((coefficientSigns >> nextSignBit--) & 1U) != 0)
                {
                    level = -level;
                }

                coefficients[rasterPosition] = level;
            }

            totalNonZero += nonZeroCount;
        }

        return totalNonZero;
    }

    /// <summary>
    /// Releases the allocator-owned coefficient scratch storage.
    /// </summary>
    public void Dispose() => this.scratchOwner.Dispose();

    /// <summary>
    /// Decodes the raster coordinates of the final significant coefficient.
    /// </summary>
    /// <param name="reader">The current entropy-substream syntax reader.</param>
    /// <param name="parameters">The transform-block coefficient coding parameters.</param>
    /// <param name="x">The decoded horizontal coordinate.</param>
    /// <param name="y">The decoded vertical coordinate.</param>
    private static void ReadLastSignificantPosition(
        ref HevcCabacSyntaxReader reader,
        in HevcCoefficientCodingParameters parameters,
        out int x,
        out int y)
    {
        bool verticalScan = parameters.ScanType == HevcCoefficientScanType.Vertical;
        int syntaxWidth = verticalScan ? parameters.Height : parameters.Width;
        int syntaxHeight = verticalScan ? parameters.Width : parameters.Height;
        bool isChroma = parameters.Plane != HevcPlane.Y;
        x = ReadLastSignificantCoordinate(ref reader, isChroma, syntaxWidth, true);
        y = ReadLastSignificantCoordinate(ref reader, isChroma, syntaxHeight, false);
        if (verticalScan)
        {
            (x, y) = (y, x);
        }
    }

    /// <summary>
    /// Decodes one last-significant coefficient coordinate from its context prefix and bypass suffix.
    /// </summary>
    /// <param name="reader">The current entropy-substream syntax reader.</param>
    /// <param name="isChroma">Whether the coordinate belongs to a chroma transform block.</param>
    /// <param name="size">The transform-block extent along the coded axis.</param>
    /// <param name="horizontal">Whether to use the horizontal rather than vertical context set.</param>
    /// <returns>The decoded zero-based coefficient coordinate.</returns>
    private static int ReadLastSignificantCoordinate(ref HevcCabacSyntaxReader reader, bool isChroma, int size, bool horizontal)
    {
        int convertedSize = BitOperations.Log2((uint)size) - 2;
        int contextOffset = isChroma ? 0 : (convertedSize * 3) + ((convertedSize + 1) >> 2);
        int contextShift = isChroma ? convertedSize : (convertedSize + 3) >> 2;
        int maximumPrefix = CoordinateGroupIndex[size - 1];
        int prefix;
        for (prefix = 0; prefix < maximumPrefix; prefix++)
        {
            int contextIndex = contextOffset + (prefix >> contextShift);
            bool prefixContinues = horizontal
                ? reader.ReadLastSignificantX(isChroma, contextIndex)
                : reader.ReadLastSignificantY(isChroma, contextIndex);

            if (!prefixContinues)
            {
                break;
            }
        }

        if (prefix <= 3)
        {
            return prefix;
        }

        int suffixLength = (prefix - 2) >> 1;
        return MinimumCoordinateInGroup[prefix] + (int)reader.ReadBypassBits(suffixLength);
    }
}
