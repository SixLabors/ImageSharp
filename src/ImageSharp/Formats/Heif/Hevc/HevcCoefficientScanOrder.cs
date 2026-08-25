// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Writes the grouped coefficient scan used by HEVC residual entropy coding.
/// </summary>
internal static class HevcCoefficientScanOrder
{
    /// <summary>
    /// The width and height of one coefficient group.
    /// </summary>
    private const int CoefficientGroupSize = 4;

    /// <summary>
    /// Writes the grouped scan for one transform block into caller-owned storage.
    /// </summary>
    /// <param name="destination">The destination receiving raster coefficient indices in scan order.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="scanType">The scan direction selected for the transform block.</param>
    /// <param name="lastRasterPosition">The raster index of the last significant coefficient.</param>
    /// <returns>The scan position of <paramref name="lastRasterPosition"/>.</returns>
    public static int Write(Span<int> destination, int width, int height, HevcCoefficientScanType scanType, int lastRasterPosition)
    {
        int widthInGroups = width / CoefficientGroupSize;
        int heightInGroups = height / CoefficientGroupSize;
        int groupCount = widthInGroups * heightInGroups;
        int lastScanPosition = -1;
        ScanGenerator groupScan = new(widthInGroups, heightInGroups, scanType);

        // H.265 scans the 4x4 groups first, then applies the same direction inside each group. Keeping this grouped
        // layout contiguous lets coefficient decoding walk every 16-entry subset without lookup-table allocations.
        for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
        {
            int groupOffsetX = groupScan.X * CoefficientGroupSize;
            int groupOffsetY = groupScan.Y * CoefficientGroupSize;
            int groupScanOffset = groupIndex * CoefficientGroupSize * CoefficientGroupSize;
            ScanGenerator coefficientScan = new(CoefficientGroupSize, CoefficientGroupSize, scanType);

            for (int coefficientIndex = 0; coefficientIndex < CoefficientGroupSize * CoefficientGroupSize; coefficientIndex++)
            {
                int rasterPosition = ((groupOffsetY + coefficientScan.Y) * width) + groupOffsetX + coefficientScan.X;
                int scanPosition = groupScanOffset + coefficientIndex;
                destination[scanPosition] = rasterPosition;
                if (rasterPosition == lastRasterPosition)
                {
                    lastScanPosition = scanPosition;
                }

                coefficientScan.MoveNext();
            }

            groupScan.MoveNext();
        }

        return lastScanPosition;
    }

    /// <summary>
    /// Advances through one rectangular scan without retaining a heap-backed lookup table.
    /// </summary>
    private struct ScanGenerator
    {
        /// <summary>
        /// The scan width.
        /// </summary>
        private readonly int width;

        /// <summary>
        /// The scan height.
        /// </summary>
        private readonly int height;

        /// <summary>
        /// The selected scan direction.
        /// </summary>
        private readonly HevcCoefficientScanType scanType;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanGenerator"/> struct.
        /// </summary>
        /// <param name="width">The scan width.</param>
        /// <param name="height">The scan height.</param>
        /// <param name="scanType">The scan direction.</param>
        public ScanGenerator(int width, int height, HevcCoefficientScanType scanType)
        {
            this.width = width;
            this.height = height;
            this.scanType = scanType;
            this.X = 0;
            this.Y = 0;
        }

        /// <summary>
        /// Gets the current horizontal coordinate.
        /// </summary>
        public int X { get; private set; }

        /// <summary>
        /// Gets the current vertical coordinate.
        /// </summary>
        public int Y { get; private set; }

        /// <summary>
        /// Advances to the next coordinate in the selected scan direction.
        /// </summary>
        public void MoveNext()
        {
            switch (this.scanType)
            {
                case HevcCoefficientScanType.Diagonal:
                    if (this.X == this.width - 1 || this.Y == 0)
                    {
                        this.Y += this.X + 1;
                        this.X = 0;
                        if (this.Y >= this.height)
                        {
                            this.X += this.Y - (this.height - 1);
                            this.Y = this.height - 1;
                        }
                    }
                    else
                    {
                        this.X++;
                        this.Y--;
                    }

                    break;
                case HevcCoefficientScanType.Horizontal:
                    if (++this.X == this.width)
                    {
                        this.X = 0;
                        this.Y++;
                    }

                    break;
                default:
                    if (++this.Y == this.height)
                    {
                        this.Y = 0;
                        this.X++;
                    }

                    break;
            }
        }
    }
}
