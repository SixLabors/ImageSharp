// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ModeDecision;

/// <summary>
/// Builds the AV1 block and transform geometries traversed by the mode-decision scan.
/// </summary>
internal class Av1BlockGeometryFactory
{
    /// <summary>
    /// The number of scan entries required by the largest supported 128-pixel superblock geometry.
    /// </summary>
    private const int MaxBlocksAllocated = 4421;

    /// <summary>
    /// Marks a geometry-depth combination that has no valid scan offset.
    /// </summary>
    private const int NotUsedValue = 0;

    /// <summary>
    /// Maps each partition shape, axis, and component to its origin offset measured in quarter-block units.
    /// </summary>
    private static readonly int[][][] NonSkipQuarterOffMult =
        [

            // 9 means not used.
            //          |   x   |     |   y   |
            /*P=0*/ [[0, 9, 9, 9], [0, 9, 9, 9]],
            /*P=1*/ [[0, 0, 9, 9], [0, 2, 9, 9]],
            /*P=2*/ [[0, 2, 9, 9], [0, 0, 9, 9]],

            /*P=7*/ [[0, 0, 0, 0], [0, 1, 2, 3]],
            /*P=8*/ [[0, 1, 2, 3], [0, 0, 0, 0]],

            /*P=3*/ [[0, 2, 0, 9], [0, 0, 2, 9]],
            /*P=4*/ [[0, 0, 2, 9], [0, 2, 2, 9]],
            /*P=5*/ [[0, 0, 2, 9], [0, 2, 0, 9]],
            /*P=6*/ [[0, 2, 2, 9], [0, 0, 2, 9]]
        ];

    /// <summary>
    /// Maps each partition shape, axis, and component to its dimension measured in quarter-block units.
    /// </summary>
    private static readonly uint[][][] NonSkipSizeMult =
        [

            // 9 means not used.
            //          |   h   |     |   v   |
            /*P=0*/ [[4, 9, 9, 9], [4, 9, 9, 9]],
            /*P=1*/ [[4, 4, 9, 9], [2, 2, 9, 9]],
            /*P=2*/ [[2, 2, 9, 9], [4, 4, 9, 9]],

            /*P=7*/ [[4, 4, 4, 4], [1, 1, 1, 1]],
            /*P=8*/ [[1, 1, 1, 1], [4, 4, 4, 4]],

            /*P=3*/ [[2, 2, 4, 9], [2, 2, 2, 9]],
            /*P=4*/ [[4, 2, 2, 9], [2, 2, 2, 9]],
            /*P=5*/ [[2, 2, 2, 9], [2, 2, 4, 9]],
            /*P=6*/ [[2, 2, 2, 9], [4, 2, 2, 9]]
        ];

    /// <summary>
    /// Maps geometry and quadtree depth to the scan offset of the next quadrant at that depth.
    /// </summary>
    private static readonly int[][] NonSkipDepthOffset =
        [
            [85, 21, 5, 1, NotUsedValue, NotUsedValue],
            [105, 25, 5, 1, NotUsedValue, NotUsedValue],
            [169, 41, 9, 1, NotUsedValue, NotUsedValue],
            [425, 105, 25, 5, NotUsedValue, NotUsedValue],
            [681, 169, 41, 9, 1, NotUsedValue],
            [849, 209, 49, 9, 1, NotUsedValue],
            [1101, 269, 61, 9, 1, NotUsedValue],
            [4421, 1101, 269, 61, 9, 1],
            [2377, 593, 145, 33, 5, NotUsedValue]
        ];

    /// <summary>
    /// Maps geometry and quadtree depth to the scan offset of the square block's first child.
    /// </summary>
    private static readonly int[][] Depth1DepthOffset =
        [
            [1, 1, 1, 1, 1, NotUsedValue],
            [5, 5, 1, 1, 1, NotUsedValue],
            [5, 5, 5, 1, 1, NotUsedValue],
            [5, 5, 5, 5, 1, NotUsedValue],
            [5, 5, 5, 5, 1, NotUsedValue],
            [13, 13, 13, 5, 1, NotUsedValue],
            [25, 25, 25, 5, 1, NotUsedValue],
            [17, 25, 25, 25, 5, 1],
            [5, 13, 13, 13, 5, NotUsedValue]
        ];

    /// <summary>
    /// The geometry whose lookup-table row is active while a scan is constructed.
    /// </summary>
    private static Av1GeometryIndex geometryIndex;

    /// <summary>
    /// The active geometry's superblock width and height in pixels.
    /// </summary>
    private static int maxSuperblock;

    /// <summary>
    /// The number of quadtree depths generated for the active geometry.
    /// </summary>
    private static int maxDepth;

    /// <summary>
    /// The number of partition shapes considered by the active geometry before size-specific restrictions.
    /// </summary>
    private static int maxPart;

    // private static int maxActiveBlockCount;

    /// <summary>
    /// Stores block geometries by mode-decision scan index.
    /// </summary>
    private readonly Av1BlockGeometry[] blockGeometryModeDecisionScan;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BlockGeometryFactory"/> class.
    /// </summary>
    /// <param name="geom">The predefined geometry used to size and populate the mode-decision scan.</param>
    public Av1BlockGeometryFactory(Av1GeometryIndex geom)
    {
        this.blockGeometryModeDecisionScan = new Av1BlockGeometry[MaxBlocksAllocated];
        int max_block_count;
        geometryIndex = geom;
        byte min_nsq_bsize;

        // These preset limits and the enum order form the row index contract for the offset tables above.
        // Changing one without the other would make parent and sibling scan offsets refer to a different geometry.
        if (geom == Av1GeometryIndex.Geometry0)
        {
            maxSuperblock = 64;
            maxDepth = 4;
            maxPart = 1;
            max_block_count = 85;
            min_nsq_bsize = 16;
        }
        else if (geom == Av1GeometryIndex.Geometry1)
        {
            maxSuperblock = 64;
            maxDepth = 4;
            maxPart = 3;
            max_block_count = 105;
            min_nsq_bsize = 16;
        }
        else if (geom == Av1GeometryIndex.Geometry2)
        {
            maxSuperblock = 64;
            maxDepth = 4;
            maxPart = 3;
            max_block_count = 169;
            min_nsq_bsize = 8;
        }
        else if (geom == Av1GeometryIndex.Geometry3)
        {
            maxSuperblock = 64;
            maxDepth = 4;
            maxPart = 3;
            max_block_count = 425;
            min_nsq_bsize = 0;
        }
        else if (geom == Av1GeometryIndex.Geometry4)
        {
            maxSuperblock = 64;
            maxDepth = 5;
            maxPart = 3;
            max_block_count = 681;
            min_nsq_bsize = 0;
        }
        else if (geom == Av1GeometryIndex.Geometry5)
        {
            maxSuperblock = 64;
            maxDepth = 5;
            maxPart = 5;
            max_block_count = 849;
            min_nsq_bsize = 0;
        }
        else if (geom == Av1GeometryIndex.Geometry6)
        {
            maxSuperblock = 64;
            maxDepth = 5;
            maxPart = 9;
            max_block_count = 1101;
            min_nsq_bsize = 0;
        }
        else if (geom == Av1GeometryIndex.Geometry7)
        {
            maxSuperblock = 128;
            maxDepth = 6;
            maxPart = 9;
            max_block_count = 4421;
            min_nsq_bsize = 0;
        }
        else
        {
            maxSuperblock = 128;
            maxDepth = 5;
            maxPart = 5;
            max_block_count = 2377;
            min_nsq_bsize = 0;
        }

        // (0)compute total number of blocks using the information provided
        // maxActiveBlockCount = CountTotalNumberOfActiveBlocks(min_nsq_bsize);

        // (2) Construct md scan blk_geom_mds:  use info from dps
        int idx_mds = 0;
        this.ScanAllBlocks(ref idx_mds, maxSuperblock, 0, 0, false, 0, min_nsq_bsize);
        LogRedundancySimilarity(max_block_count);
    }

    /// <summary>
    /// Counts the block entries produced by every enabled partition at every depth of the active geometry.
    /// </summary>
    /// <param name="min_nsq_bsize">The smallest square size, in pixels, at which non-square partitions remain enabled.</param>
    /// <returns>The number of active mode-decision scan entries.</returns>
    private static int CountTotalNumberOfActiveBlocks(int min_nsq_bsize)
    {
        int depth_scan_idx = 0;

        for (int depthIterator = 0; depthIterator < maxDepth; depthIterator++)
        {
            int totalSquareCount = 1 << depthIterator;

            // Each quadtree depth halves the square sequence dimension. The final branch covers the deepest
            // 128-pixel-superblock geometry, whose sixth level contains 4-pixel squares.
            int sequenceSize = depthIterator == 0 ? maxSuperblock
                   : depthIterator == 1 ? maxSuperblock / 2
                   : depthIterator == 2 ? maxSuperblock / 4
                   : depthIterator == 3 ? maxSuperblock / 8
                   : depthIterator == 4 ? maxSuperblock / 16 : maxSuperblock / 32;

            // AV1 restricts the partition shapes allowed at the largest and smallest block sizes. Apply those
            // caps before walking the shape table so a row is never interpreted for an illegal block size.
            int max_part_updated = sequenceSize == 128 ? Math.Min(maxPart, maxPart < 9 && maxPart > 3 ? 3 : 7)
                : sequenceSize == 8 ? Math.Min(maxPart, 3)
                : sequenceSize == 4 ? 1 : maxPart;
            if (sequenceSize <= min_nsq_bsize)
            {
                max_part_updated = 1;
            }

            for (int squareIteratorY = 0; squareIteratorY < totalSquareCount; squareIteratorY++)
            {
                for (int squareIteratorX = 0; squareIteratorX < totalSquareCount; squareIteratorX++)
                {
                    for (int partitionIterator = 0; partitionIterator < max_part_updated; partitionIterator++)
                    {
                        int tot_num_ns_per_part = GetNonSquareCountPerPart(partitionIterator, sequenceSize);
                        depth_scan_idx += tot_num_ns_per_part;
                    }
                }
            }
        }

        return depth_scan_idx;
    }

    /// <summary>
    /// Gets the number of component blocks emitted by one partition shape.
    /// </summary>
    /// <param name="partitionIterator">The zero-based partition-shape index in scan order.</param>
    /// <param name="sequenceSize">The width and height, in pixels, of the square being partitioned.</param>
    /// <returns>The number of component blocks in the partition.</returns>
    private static int GetNonSquareCountPerPart(int partitionIterator, int sequenceSize)
    {
        int tot_num_ns_per_part = partitionIterator < 1 ? 1 : partitionIterator < 3 ? 2 : partitionIterator < 5 && sequenceSize < 128 ? 4 : 3;
        return tot_num_ns_per_part;
    }

    /// <summary>
    /// Records scan entries that represent the same block size at the same pixel origin.
    /// </summary>
    /// <param name="max_block_count">The number of populated scan entries to compare.</param>
    private static void LogRedundancySimilarity(int max_block_count)
    {
        for (int blockIterator = 0; blockIterator < max_block_count; blockIterator++)
        {
            Av1BlockGeometry cur_geom = GetBlockGeometryByModeDecisionScanIndex(blockIterator);
            cur_geom.RedunancyList.Clear();

            for (int searchIterator = 0; searchIterator < max_block_count; searchIterator++)
            {
                Av1BlockGeometry search_geom = GetBlockGeometryByModeDecisionScanIndex(searchIterator);

                if (cur_geom.BlockSize == search_geom.BlockSize &&
                    cur_geom.Origin == search_geom.Origin &&
                    searchIterator != blockIterator)
                {
                    if (cur_geom.NonSquareIndex == 0 && search_geom.NonSquareIndex == 0 && cur_geom.RedunancyList.Count < 3)
                    {
                        cur_geom.RedunancyList.Add(search_geom.ModeDecisionIndex);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the block geometry at a mode-decision scan index.
    /// </summary>
    /// <param name="modeDecisionScanIndex">The zero-based mode-decision scan index.</param>
    /// <returns>The geometry stored at <paramref name="modeDecisionScanIndex"/>.</returns>
    /// <exception cref="NotImplementedException">Always thrown because the geometry lookup has not been implemented.</exception>
    public static Av1BlockGeometry GetBlockGeometryByModeDecisionScanIndex(int modeDecisionScanIndex) => throw new NotImplementedException();

    /// <summary>
    /// Appends every enabled partition and transform layout for a square region to scan order.
    /// </summary>
    /// <param name="index">The next scan index; advanced once for every emitted block geometry.</param>
    /// <param name="sequenceSize">The width and height, in pixels, of the square region being partitioned.</param>
    /// <param name="x">The region's horizontal origin in pixels relative to the superblock.</param>
    /// <param name="y">The region's vertical origin in pixels relative to the superblock.</param>
    /// <param name="isLastQuadrant">Whether the region is the final quadrant of its parent.</param>
    /// <param name="quadIterator">The zero-based quadrant index within the parent.</param>
    /// <param name="minNonSquareBlockSize">The smallest square size, in pixels, at which non-square partitions remain enabled.</param>
    private void ScanAllBlocks(ref int index, int sequenceSize, int x, int y, bool isLastQuadrant, byte quadIterator, byte minNonSquareBlockSize)
    {
        // The input block is the parent square block of size sq_size located at pos (x,y)
        Guard.MustBeLessThanOrEqualTo(quadIterator, (byte)3, nameof(quadIterator));

        int halfsize = sequenceSize / 2;
        int quartsize = sequenceSize / 4;

        // AV1 removes partition shapes that cannot be represented at 128-, 8-, and 4-pixel square sizes.
        // The scan tables are ordered by the remaining shape set, so the cap must be applied before indexing them.
        int max_part_updated = sequenceSize == 128 ? Math.Min(maxPart, maxPart is < 9 and > 3 ? 3 : 7)
            : sequenceSize == 8 ? Math.Min(maxPart, 3)
            : sequenceSize == 4 ? 1 : maxPart;
        if (sequenceSize <= minNonSquareBlockSize)
        {
            max_part_updated = 1;
        }

        int sqi_mds = index;

        for (int partitionIterator = 0; partitionIterator < max_part_updated; partitionIterator++)
        {
            int tot_num_ns_per_part = GetNonSquareCountPerPart(partitionIterator, sequenceSize);

            for (int nonSquareIterator = 0; nonSquareIterator < tot_num_ns_per_part; nonSquareIterator++)
            {
                // Geometry presets use power-of-two superblocks, so the current square dimension uniquely identifies
                // its quadtree depth without carrying recursion state in every scan entry.
                this.blockGeometryModeDecisionScan[index].Depth = sequenceSize == maxSuperblock / 1 ? 0
                    : sequenceSize == maxSuperblock / 2 ? 1
                    : sequenceSize == maxSuperblock / 4 ? 2
                    : sequenceSize == maxSuperblock / 8 ? 3
                    : sequenceSize == maxSuperblock / 16 ? 4 : 5;

                this.blockGeometryModeDecisionScan[index].SequenceSize = sequenceSize;
                this.blockGeometryModeDecisionScan[index].IsLastQuadrant = isLastQuadrant;

                // part_it >= 3 for 128x128 blocks corresponds to HA/HB/VA/VB shapes since H4/V4 are not allowed
                // for 128x128 blocks.  Therefore, need to offset part_it by 2 to not index H4/V4 shapes.
                int part_it_idx = partitionIterator >= 3 && sequenceSize == 128 ? partitionIterator + 2 : partitionIterator;
                this.blockGeometryModeDecisionScan[index].Origin = new Point(
                    x + (quartsize * NonSkipQuarterOffMult[part_it_idx][0][nonSquareIterator]),
                    y + (quartsize * NonSkipQuarterOffMult[part_it_idx][1][nonSquareIterator]));

                // These properties aren't used.
                // this.blockGeometryModeDecisionScan[index].Shape = (Part)part_it_idx;
                // this.blockGeometryModeDecisionScan[index].QuadIndex = quadIterator;
                // this.blockGeometryModeDecisionScan[index].d1i = depth1Iterator++;
                // this.blockGeometryModeDecisionScan[index].sqi_mds = sqi_mds;
                this.blockGeometryModeDecisionScan[index].Depth1Offset =
                    Depth1DepthOffset[(int)geometryIndex][this.blockGeometryModeDecisionScan[index].Depth];
                this.blockGeometryModeDecisionScan[index].NextDepthOffset =
                    NonSkipDepthOffset[(int)geometryIndex][this.blockGeometryModeDecisionScan[index].Depth];
                this.blockGeometryModeDecisionScan[index].TotalNonSuareCount = tot_num_ns_per_part;
                this.blockGeometryModeDecisionScan[index].NonSquareIndex = nonSquareIterator;
                uint blockWidth = (uint)quartsize * NonSkipSizeMult[part_it_idx][0][nonSquareIterator];
                uint blockHeight = (uint)quartsize * NonSkipSizeMult[part_it_idx][1][nonSquareIterator];

                // Av1BlockSize indexes dimensions by log2(size) - 2 because 4x4 is the smallest coded block.
                this.blockGeometryModeDecisionScan[index].BlockSize =
                    Av1BlockSizeExtensions.FromWidthAndHeight(Av1Math.Log2_32(blockWidth) - 2u, Av1Math.Log2_32(blockHeight) - 2u);
                this.blockGeometryModeDecisionScan[index].BlockSizeUv = this.blockGeometryModeDecisionScan[index].BlockSize.GetSubsampled(true, true);

                // this.blockGeometryModeDecisionScan[index].BlockWidthUv = Math.Max(4, this.blockGeometryModeDecisionScan[index].BlockWidth >> 1);
                // this.blockGeometryModeDecisionScan[index].BlockHeightUv = Math.Max(4, this.blockGeometryModeDecisionScan[index].BlockHeight >> 1);
                this.blockGeometryModeDecisionScan[index].HasUv = true;

                // Chroma cannot be subdivided below its minimum block dimensions. When several luma blocks map to
                // the same chroma block, only the final contributing luma component owns that shared U/V geometry.
                if (this.blockGeometryModeDecisionScan[index].BlockWidth == 4 && this.blockGeometryModeDecisionScan[index].BlockHeight == 4)
                {
                    this.blockGeometryModeDecisionScan[index].HasUv = isLastQuadrant;
                }
                else if ((this.blockGeometryModeDecisionScan[index].BlockWidth >> 1) < this.blockGeometryModeDecisionScan[index].BlockWidthUv ||
                         (this.blockGeometryModeDecisionScan[index].BlockHeight >> 1) < this.blockGeometryModeDecisionScan[index].BlockHeightUv)
                {
                    int num_blk_same_uv = 1;
                    if (this.blockGeometryModeDecisionScan[index].BlockWidth >> 1 < 4)
                    {
                        num_blk_same_uv *= 2;
                    }

                    if (this.blockGeometryModeDecisionScan[index].BlockHeight >> 1 < 4)
                    {
                        num_blk_same_uv *= 2;
                    }

                    // if (this.blockGeometryModeDecisionScan[index].nsi % 2 == 0)
                    // if (this.blockGeometryModeDecisionScan[index].nsi != (this.blockGeometryModeDecisionScan[index].totns-1) )
                    if (this.blockGeometryModeDecisionScan[index].NonSquareIndex != (num_blk_same_uv - 1) &&
                        this.blockGeometryModeDecisionScan[index].NonSquareIndex != ((2 * num_blk_same_uv) - 1))
                    {
                        this.blockGeometryModeDecisionScan[index].HasUv = false;
                    }
                }

                // Transform depth zero keeps the largest legal transform. Blocks larger than AV1's 64x64 transform
                // limit are represented by two or four transform blocks whose origins cover the coded block.
                int tx_depth = 0;
                this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x128
                    ? 4
                    : this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block128x64 or Av1BlockSize.Block64x128
                    ? 2
                    : 1;
                for (int transformBlockIterator = 0; transformBlockIterator < this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth]; transformBlockIterator++)
                {
                    this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] =
                        GetTransformSize(this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                    this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                        GetTransformSize(this.blockGeometryModeDecisionScan[index].BlockSize, 1);
                    if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x128)
                    {
                        int offsetx = (transformBlockIterator is 0 or 2) ? 0 : 64;
                        int offsety = (transformBlockIterator is 0 or 1) ? 0 : 64;
                        Size offset = new(offsetx, offsety);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x64)
                    {
                        int offsetx = (transformBlockIterator == 0) ? 0 : 64;
                        int offsety = 0;
                        Size offset = new(offsetx, offsety);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x128)
                    {
                        int offsetx = 0;
                        int offsety = (transformBlockIterator == 0) ? 0 : 64;
                        Size offset = new(offsetx, offsety);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else
                    {
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin;
                    }
                }

                // Transform depth one subdivides eligible luma blocks once while chroma retains its depth-zero size.
                // The block-count cases below mirror the legal rectangular AV1 transform partitions.
                tx_depth = 1;
                this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x128
                    ? 4
                    : this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block128x64 or Av1BlockSize.Block64x128
                    ? 2
                    : 1;

                if (this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block64x64 or
                    Av1BlockSize.Block32x32 or
                    Av1BlockSize.Block16x16 or
                    Av1BlockSize.Block8x8)
                {
                    this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = 4;
                }

                if (this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block64x32 or
                    Av1BlockSize.Block32x64 or
                    Av1BlockSize.Block32x16 or
                    Av1BlockSize.Block16x32 or
                    Av1BlockSize.Block16x8 or
                    Av1BlockSize.Block8x16)
                {
                    this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = 2;
                }

                if (this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block64x16 or
                    Av1BlockSize.Block16x64 or
                    Av1BlockSize.Block32x8 or
                    Av1BlockSize.Block8x32 or
                    Av1BlockSize.Block16x4 or
                    Av1BlockSize.Block4x16)
                {
                    this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = 2;
                }

                for (int transformBlockIterator = 0; transformBlockIterator < this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth]; transformBlockIterator++)
                {
                    if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x64)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block32x32, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 32, 0, 32];
                        int[] offsety = [0, 0, 32, 32];

                        // 0  1
                        // 2  3
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block32x32, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 32];
                        int[] offsety = [0, 0];

                        // 0  1
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x64)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block32x32, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 0];
                        int[] offsety = [0, 32];

                        // 0  1
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 16, 0, 16];
                        int[] offsety = [0, 0, 16, 16];

                        // 0  1
                        // 2  3
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 16];
                        int[] offsety = [0, 0];

                        // 0  1
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 0];
                        int[] offsety = [0, 16];

                        // 0  1
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 8, 0, 8];
                        int[] offsety = [0, 0, 8, 8];

                        // 0  1
                        // 2  3
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x8)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 8];
                        int[] offsety = [0, 0];

                        // 0  1
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block8x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 0];
                        int[] offsety = [0, 8];

                        // 0  1
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block8x8)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block4x4, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                        int[] offsetx = [0, 4, 0, 4];
                        int[] offsety = [0, 0, 4, 4];

                        // 0  1
                        // 2  3
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block32x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx = [0, 32];
                        int[] offsety = [0, 0];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x64)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x32, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx = [0, 0];
                        int[] offsety = [0, 32];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x8)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx = [0, 16];
                        int[] offsety = [0, 0];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block8x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        // 0  1 2 3
                        int[] offsetx = [0, 0];
                        int[] offsety = [0, 16];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x4)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x4, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx = [0, 8];
                        int[] offsety = [0, 0];

                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block4x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block4x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx = [0, 0];
                        int[] offsety = [0, 8];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else
                    {
                        if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x128)
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                            int offsetx = (transformBlockIterator is 0 or 2) ? 0 : 64;
                            int offsety = (transformBlockIterator is 0 or 1) ? 0 : 64;
                            Size offset = new(offsetx, offsety);
                            this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                    this.blockGeometryModeDecisionScan[index].Origin + offset;
                        }
                        else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x64)
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                            int offsetx = (transformBlockIterator is 0) ? 0 : 64;
                            int offsety = 0;
                            Size offset = new(offsetx, offsety);
                            this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                    this.blockGeometryModeDecisionScan[index].Origin + offset;
                        }
                        else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x128)
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                            int offsetx = 0;
                            int offsety = (transformBlockIterator is 0) ? 0 : 64;
                            Size offset = new(offsetx, offsety);
                            this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                    this.blockGeometryModeDecisionScan[index].Origin + offset;
                        }
                        else
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                            this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                    this.blockGeometryModeDecisionScan[index].Origin;
                        }
                    }

                    /*this.blockGeometryModeDecisionScan[index].tx_width[tx_depth] =
                        tx_size_wide[this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth]];
                    this.blockGeometryModeDecisionScan[index].tx_height[tx_depth] =
                        tx_size_high[this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth]];
                    this.blockGeometryModeDecisionScan[index].tx_width_uv[tx_depth] = this.blockGeometryModeDecisionScan[index].tx_width_uv[0];
                    this.blockGeometryModeDecisionScan[index].tx_height_uv[tx_depth] = this.blockGeometryModeDecisionScan[index].tx_height_uv[0];*/
                }

                // Transform depth two performs a second subdivision. The origin tables enumerate the child
                // transforms in raster order so coefficient reconstruction visits the same spatial layout.
                tx_depth = 2;

                this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x128
                    ? 4
                    : this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block128x64 or
                        Av1BlockSize.Block64x128
                    ? 2
                    : 1;

                if (this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block64x64 or
                    Av1BlockSize.Block32x32 or
                    Av1BlockSize.Block16x16)
                {
                    this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = 16;
                }

                if (this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block64x32 or
                    Av1BlockSize.Block32x64 or
                    Av1BlockSize.Block32x16 or
                    Av1BlockSize.Block16x32 or
                    Av1BlockSize.Block16x8 or
                    Av1BlockSize.Block8x16)
                {
                    this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = 8;
                }

                if (this.blockGeometryModeDecisionScan[index].BlockSize is Av1BlockSize.Block64x16 or
                    Av1BlockSize.Block16x64 or
                    Av1BlockSize.Block32x8 or
                    Av1BlockSize.Block8x32 or
                    Av1BlockSize.Block16x4 or
                    Av1BlockSize.Block4x16)
                {
                    this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth] = 4;
                }

                for (int transformBlockIterator = 0; transformBlockIterator < this.blockGeometryModeDecisionScan[index].TransformBlockCount[tx_depth]; transformBlockIterator++)
                {
                    if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x64)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 16, 32, 48, 0, 16, 32, 48, 0, 16, 32, 48, 0, 16, 32, 48];
                        int[] offsety_intra = [0, 0, 0, 0, 16, 16, 16, 16, 32, 32, 32, 32, 48, 48, 48, 48];
                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 16, 32, 48, 0, 16, 32, 48];
                        int[] offsety_intra = [0, 0, 0, 0, 16, 16, 16, 16];
                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x64)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 16, 0, 16, 0, 16, 0, 16];
                        int[] offsety_intra = [0, 0, 16, 16, 32, 32, 48, 48];

                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 8, 16, 24, 0, 8, 16, 24, 0, 8, 16, 24, 0, 8, 16, 24];
                        int[] offsety_intra = [0, 0, 0, 0, 8, 8, 8, 8, 16, 16, 16, 16, 24, 24, 24, 24];

                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 8, 16, 24, 0, 8, 16, 24];
                        int[] offsety_intra = [0, 0, 0, 0, 8, 8, 8, 8];

                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 8, 0, 8, 0, 8, 0, 8];
                        int[] offsety_intra = [0, 0, 8, 8, 16, 16, 24, 24];
                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x8)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block4x4, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 4, 8, 12, 0, 4, 8, 12];
                        int[] offsety_intra = [0, 0, 0, 0, 4, 4, 4, 4];
                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block8x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block4x4, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 4, 0, 4, 0, 4, 0, 4];
                        int[] offsety_intra = [0, 0, 4, 4, 8, 8, 12, 12];
                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block4x4, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        int[] offsetx_intra = [0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12, 0, 4, 8, 12];
                        int[] offsety_intra = [0, 0, 0, 0, 4, 4, 4, 4, 8, 8, 8, 8, 12, 12, 12, 12];
                        Size offset = new(offsetx_intra[transformBlockIterator], offsety_intra[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        // 0  1 2 3
                        int[] offsetx = [0, 16, 32, 48];
                        int[] offsety = [0, 0, 0, 0];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x64)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block16x16, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        // 0  1 2 3
                        int[] offsetx = [0, 0, 0, 0];
                        int[] offsety = [0, 16, 32, 48];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block32x8)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        // 0  1 2 3
                        int[] offsetx = [0, 8, 16, 24];
                        int[] offsety = [0, 0, 0, 0];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block8x32)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block8x8, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        // 0  1 2 3
                        int[] offsetx = [0, 0, 0, 0];
                        int[] offsety = [0, 8, 16, 24];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block16x4)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block4x4, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        // 0  1 2 3
                        int[] offsetx = [0, 4, 8, 12];
                        int[] offsety = [0, 0, 0, 0];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block4x16)
                    {
                        this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(Av1BlockSize.Block4x4, 0);
                        this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] = this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];

                        // 0  1 2 3
                        int[] offsetx = [0, 0, 0, 0];
                        int[] offsety = [0, 4, 8, 12];
                        Size offset = new(offsetx[transformBlockIterator], offsety[transformBlockIterator]);
                        this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                            this.blockGeometryModeDecisionScan[index].Origin + offset;
                    }
                    else
                    {
                        if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x128)
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                            int offsetx = (transformBlockIterator is 0 or 2) ? 0 : 64;
                            int offsety = (transformBlockIterator is 0 or 1) ? 0 : 64;
                            Size offset = new(offsetx, offsety);
                            this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                    this.blockGeometryModeDecisionScan[index].Origin + offset;
                        }
                        else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block128x64)
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                            int offsetx = (transformBlockIterator is 0) ? 0 : 64;
                            int offsety = 0;
                            Size offset = new(offsetx, offsety);
                            this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                    this.blockGeometryModeDecisionScan[index].Origin + offset;
                        }
                        else if (this.blockGeometryModeDecisionScan[index].BlockSize == Av1BlockSize.Block64x128)
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                            int offsetx = 0;
                            int offsety = (transformBlockIterator is 0) ? 0 : 64;
                            Size offset = new(offsetx, offsety);
                        }
                        else
                        {
                            this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth] = GetTransformSize(
                                this.blockGeometryModeDecisionScan[index].BlockSize, 0);
                            this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth] =
                                this.blockGeometryModeDecisionScan[index].TransformSizeUv[0];
                            this.blockGeometryModeDecisionScan[index].TransformOrigin[tx_depth][transformBlockIterator] =
                                    this.blockGeometryModeDecisionScan[index].Origin;
                        }
                    }

                    /*this.blockGeometryModeDecisionScan[index].tx_width[tx_depth] =
                        tx_size_wide[this.blockGeometryModeDecisionScan[index].txsize[tx_depth]];
                    this.blockGeometryModeDecisionScan[index].tx_height[tx_depth] =
                        tx_size_high[this.blockGeometryModeDecisionScan[index].txsize[tx_depth]];
                    this.blockGeometryModeDecisionScan[index].tx_width_uv[tx_depth] = this.blockGeometryModeDecisionScan[index].tx_width_uv[0];
                    this.blockGeometryModeDecisionScan[index].tx_height_uv[tx_depth] = this.blockGeometryModeDecisionScan[index].tx_height_uv[0];*/
                }

                this.blockGeometryModeDecisionScan[index].ModeDecisionIndex = index;
                index += 1;
            }
        }
    }

    /// <summary>
    /// Gets the largest legal transform size for a luma or subsampled chroma block.
    /// </summary>
    /// <param name="blockSize">The coded block size whose transform limit is requested.</param>
    /// <param name="plane">The plane index, where zero selects luma and a positive value selects chroma.</param>
    /// <returns>The maximum transform size for the selected plane.</returns>
    private static Av1TransformSize GetTransformSize(Av1BlockSize blockSize, int plane)
    {
        // Luma uses the coded block's normative transform ceiling directly.
        if (plane == 0)
        {
            return blockSize.GetMaximumTransformSize();
        }

        // This geometry models 4:2:0 chroma, so both chroma axes are subsampled before selecting their limit.
        bool subsampling_x = plane > 0;
        bool subsampling_y = plane > 0;
        return blockSize.GetMaxUvTransformSize(subsampling_x, subsampling_y);
    }
}
