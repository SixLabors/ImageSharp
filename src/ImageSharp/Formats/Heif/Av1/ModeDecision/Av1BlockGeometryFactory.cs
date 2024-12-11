// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.ModeDecision;

internal class Av1BlockGeometryFactory
{
    private const int MaxBlocksAllocated = 4421;
    private const int NotUsedValue = 0;
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

    // gives the index of next quadrant child within a depth
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

    // gives the next depth block(first qudrant child) from a given parent square
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

    private static Av1GeometryIndex geometryIndex;
    private static int maxSuperblock;
    private static int maxDepth;
    private static int maxPart;

    // private static int maxActiveBlockCount;
    private readonly Av1BlockGeometry[] blockGeometryModeDecisionScan;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1BlockGeometryFactory"/> class.
    /// </summary>
    /// <remarks>SVT: md_scan_all_blks</remarks>
    public Av1BlockGeometryFactory(Av1GeometryIndex geom)
    {
        this.blockGeometryModeDecisionScan = new Av1BlockGeometry[MaxBlocksAllocated];
        int max_block_count;
        geometryIndex = geom;
        byte min_nsq_bsize;
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

        // if (maxActiveBlockCount != max_block_count)
        //    SVT_LOG(" \n\n Error %i blocks\n\n ", maxActiveBlockCount);
        // (2) Construct md scan blk_geom_mds:  use info from dps
        int idx_mds = 0;
        this.ScanAllBlocks(ref idx_mds, maxSuperblock, 0, 0, false, 0, min_nsq_bsize);
        LogRedundancySimilarity(max_block_count);
    }

    /// <summary>
    /// SVT: count_total_num_of_active_blks
    /// </summary>
    private static int CountTotalNumberOfActiveBlocks(int min_nsq_bsize)
    {
        int depth_scan_idx = 0;

        for (int depthIterator = 0; depthIterator < maxDepth; depthIterator++)
        {
            int totalSquareCount = 1 << depthIterator;
            int sequenceSize = depthIterator == 0 ? maxSuperblock
                   : depthIterator == 1 ? maxSuperblock / 2
                   : depthIterator == 2 ? maxSuperblock / 4
                   : depthIterator == 3 ? maxSuperblock / 8
                   : depthIterator == 4 ? maxSuperblock / 16 : maxSuperblock / 32;

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
    /// SVT: get_num_ns_per_part
    /// </summary>
    private static int GetNonSquareCountPerPart(int partitionIterator, int sequenceSize)
    {
        int tot_num_ns_per_part = partitionIterator < 1 ? 1 : partitionIterator < 3 ? 2 : partitionIterator < 5 && sequenceSize < 128 ? 4 : 3;
        return tot_num_ns_per_part;
    }

    /// <summary>
    /// SVT: log_redundancy_similarity
    /// </summary>
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
    /// SVT: get_blk_geom_mds
    /// </summary>
    public static Av1BlockGeometry GetBlockGeometryByModeDecisionScanIndex(int modeDecisionScanIndex) => throw new NotImplementedException();

    private void ScanAllBlocks(ref int index, int sequenceSize, int x, int y, bool isLastQuadrant, byte quadIterator, byte minNonSquareBlockSize)
    {
        // The input block is the parent square block of size sq_size located at pos (x,y)
        Guard.MustBeLessThanOrEqualTo(quadIterator, (byte)3, nameof(quadIterator));

        int halfsize = sequenceSize / 2;
        int quartsize = sequenceSize / 4;

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
                // this.blockGeometryModeDecisionScan[index].svt_aom_geom_idx = svt_aom_geom_idx;
                /*
                this.blockGeometryModeDecisionScan[index].parent_depth_idx_mds = sqi_mds == 0
                    ? 0
                    : (sqi_mds + (3 - quad_it) * ns_depth_offset[svt_aom_geom_idx][this.blockGeometryModeDecisionScan[index].Depth]) -
                        parent_depth_offset[svt_aom_geom_idx][this.blockGeometryModeDecisionScan[index].Depth];*/
                this.blockGeometryModeDecisionScan[index].Depth1Offset =
                    Depth1DepthOffset[(int)geometryIndex][this.blockGeometryModeDecisionScan[index].Depth];
                this.blockGeometryModeDecisionScan[index].NextDepthOffset =
                    NonSkipDepthOffset[(int)geometryIndex][this.blockGeometryModeDecisionScan[index].Depth];
                this.blockGeometryModeDecisionScan[index].TotalNonSuareCount = tot_num_ns_per_part;
                this.blockGeometryModeDecisionScan[index].NonSquareIndex = nonSquareIterator;
                uint blockWidth = (uint)quartsize * NonSkipSizeMult[part_it_idx][0][nonSquareIterator];
                uint blockHeight = (uint)quartsize * NonSkipSizeMult[part_it_idx][1][nonSquareIterator];
                this.blockGeometryModeDecisionScan[index].BlockSize =
                    Av1BlockSizeExtensions.FromWidthAndHeight(Av1Math.Log2_32(blockWidth) - 2u, Av1Math.Log2_32(blockHeight) - 2u);
                this.blockGeometryModeDecisionScan[index].BlockSizeUv = this.blockGeometryModeDecisionScan[index].BlockSize.GetSubsampled(true, true);

                // this.blockGeometryModeDecisionScan[index].BlockWidthUv = Math.Max(4, this.blockGeometryModeDecisionScan[index].BlockWidth >> 1);
                // this.blockGeometryModeDecisionScan[index].BlockHeightUv = Math.Max(4, this.blockGeometryModeDecisionScan[index].BlockHeight >> 1);
                this.blockGeometryModeDecisionScan[index].HasUv = true;

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

                // tx_depth 1 geom settings
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

                    /*if (this.blockGeometryModeDecisionScan[index].bsize == BLOCK_16X8)
                        SVT_LOG("");
                    this.blockGeometryModeDecisionScan[index].tx_width[tx_depth] =
                        tx_size_wide[this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth]];
                    this.blockGeometryModeDecisionScan[index].tx_height[tx_depth] =
                        tx_size_high[this.blockGeometryModeDecisionScan[index].TransformSize[tx_depth]];
                    this.blockGeometryModeDecisionScan[index].tx_width_uv[tx_depth] =
                        tx_size_wide[this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth]];
                    this.blockGeometryModeDecisionScan[index].tx_height_uv[tx_depth] =
                        tx_size_high[this.blockGeometryModeDecisionScan[index].TransformSizeUv[tx_depth]];*/
                }

                // tx_depth 1 geom settings
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

                // tx_depth 2 geom settings
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
    /// SVT: av1_get_tx_size
    /// </summary>
    private static Av1TransformSize GetTransformSize(Av1BlockSize blockSize, int plane)
    {
        // const MbModeInfo* mbmi = xd->mi[0];
        // if (xd->lossless[mbmi->segment_id]) return TX_4X4;
        if (plane == 0)
        {
            return blockSize.GetMaximumTransformSize();
        }

        // const MacroblockdPlane *pd = &xd->plane[plane];
        bool subsampling_x = plane > 0;
        bool subsampling_y = plane > 0;
        return blockSize.GetMaxUvTransformSize(subsampling_x, subsampling_y);
    }
}
