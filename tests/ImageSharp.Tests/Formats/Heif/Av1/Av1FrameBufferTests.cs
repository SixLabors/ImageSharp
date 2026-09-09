// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.ChromaFromLuma;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 frame-plane allocation contracts.
/// </summary>
[Trait("Format", "Avif")]
public class Av1FrameBufferTests
{
    /// <summary>
    /// Verifies reconstruction workspace reuse and recovery when its initial or larger allocation fails.
    /// </summary>
    /// <param name="failGrowth">Whether the rejected request replaces the smaller workspace.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReconstructionWorkspaceAllocationFailureAllowsRetry(bool failGrowth)
    {
        byte[][] payloads = new byte[2][];
        int[] workspaceLengths = new int[2];
        for (int i = 0; i < payloads.Length; i++)
        {
            int size = i == 0 ? 64 : 128;
            using Image<L8> source = new(size, size);
            for (int row = 0; row < size; row++)
            {
                source.Frames.RootFrame.PixelBuffer.DangerousGetRowSpan(row).Fill(new L8(91));
            }

            ObuColorConfig colorConfig = new()
            {
                IsMonochrome = true,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit,
                ColorRange = true
            };

            using MemoryStream output = new();
            ObuSequenceHeader sequenceHeader = Av1FrameEncoder.Encode(
                Configuration.Default, source.Frames.RootFrame, output, colorConfig, qIndex: 0, effort: 10);

            Assert.Equal(i == 1, sequenceHeader.Use128x128Superblock);
            workspaceLengths[i] = Av1BlockDecoder.GetWorkspaceLength(sequenceHeader);
            payloads[i] = output.ToArray();
        }

        // Find the actual workspace request through the production decoder. This keeps fault injection tied
        // to the owner under test when unrelated frame allocations change their request order.
        TestMemoryAllocator baselineAllocator = new();
        baselineAllocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = baselineAllocator;
        using (Av1Decoder decoder = new(configuration))
        {
            foreach (byte[] payload in payloads)
            {
                using Av1FrameBuffer<byte> frame = decoder.DecodeFrameBuffer(payload, null, null, out _);
            }
        }

        TestMemoryAllocator.AllocationRequest[] allocations = baselineAllocator.AllocationLog.ToArray();
        int rejectedLength = workspaceLengths[failGrowth ? 1 : 0];
        int failureAllocationNumber = Array.FindIndex(
            allocations, x => x.ElementType == typeof(short) && x.Length == rejectedLength) + 1;

        Assert.True(failureAllocationNumber > 0);
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber);
        configuration.MemoryAllocator = allocator;
        using (Av1Decoder decoder = new(configuration))
        {
            int[] frameOrder = [0, 1, 1, 0];
            int retainedLength = 0;
            for (int step = 0; step < frameOrder.Length; step++)
            {
                int index = frameOrder[step];
                byte[] payload = payloads[index];
                if (step == (failGrowth ? 1 : 0))
                {
                    Assert.Throws<InvalidMemoryOperationException>(() => decoder.DecodeFrameBuffer(payload, null, null, out _));
                    Assert.Equal(failureAllocationNumber, allocator.AllocationAttemptCount);
                    if (failGrowth)
                    {
                        int oldOwner = Assert.Single(
                            allocator.AllocationLog,
                            x => x.ElementType == typeof(short) && x.Length == workspaceLengths[0]).HashCodeOfBuffer;

                        Assert.Single(allocator.ReturnLog, x => x.HashCodeOfBuffer == oldOwner);
                    }
                }

                using Av1FrameBuffer<byte> frame = decoder.DecodeFrameBuffer(payload, null, null, out _);
                retainedLength = Math.Max(retainedLength, workspaceLengths[index]);
                int owner = Assert.Single(
                    allocator.AllocationLog, x => x.ElementType == typeof(short) && x.Length == retainedLength).HashCodeOfBuffer;

                Assert.DoesNotContain(allocator.ReturnLog, x => x.HashCodeOfBuffer == owner);
                Buffer2DRegion<byte> luma = frame.DeriveBlockPointer(Av1Plane.Y, 0, 0);
                for (int row = 0; row < frame.Height; row++)
                {
                    Assert.True(luma.DangerousGetRowSpan(row).IndexOfAnyExcept((byte)91) < 0);
                }
            }
        }

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(allocator.AllocationLog, allocation => Assert.Single(allocator.ReturnLog, x => x.HashCodeOfBuffer == allocation.HashCodeOfBuffer));
    }

    /// <summary>
    /// Verifies initial and growing restoration-stage allocations unwind and recover without replacing live owners twice.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void RestorationStageAllocationFailureAllowsRetry(int failureAllocationNumber)
    {
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber);
        ObuSequenceHeader sequence = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            ColorConfig = new ObuColorConfig { IsMonochrome = true, BitDepth = Av1BitDepth.EightBit }
        };

        using Av1LoopRestorationBoundary boundary = new(Configuration.Default.MemoryAllocator);
        using (Av1LoopRestorationDecoder restoration = new(allocator))
        {
            foreach (int width in new[] { 8, 64 })
            {
                ObuFrameHeader header = new()
                {
                    ModeInfoColumnCount = width / 4,
                    ModeInfoRowCount = 16,
                    FrameSize = new ObuFrameSize { FrameWidth = width, SuperResolutionUpscaledWidth = width, FrameHeight = 64 }
                };

                header.LoopRestorationParameters.Items[0].Type = ObuRestorationType.Wiener;
                header.LoopRestorationParameters.Items[0].Size = 64;
                using Av1FrameInfo frameInfo = new(sequence, header);
                frameInfo.InitializeLoopRestoration(sequence, header);
                using Av1FrameBuffer<byte> source = new(Configuration.Default, sequence, Av1ColorFormat.Yuv400, false, width, 64);
                Buffer2DRegion<byte> visible = source.DeriveBlockPointer(Av1Plane.Y, 0, 0);
                for (int row = 0; row < 64; row++)
                {
                    visible.DangerousGetRowSpan(row).Fill(17);
                }

                boundary.SaveDeblockedRows(sequence, header, source);
                boundary.SaveFrameEdgeRows(sequence, header, source);

                // Each geometry needs an output, convolution scratch, and projection scratch. Reject
                // each initial/growth rent in turn; later successful owners must remain reachable for disposal.
                if ((width == 8 && failureAllocationNumber <= 3) || (width == 64 && failureAllocationNumber > 3))
                {
                    Assert.Throws<InvalidMemoryOperationException>(() => restoration.DecodeFrame(sequence, header, frameInfo, source, boundary));
                    Assert.Equal(failureAllocationNumber, allocator.AllocationAttemptCount);
                }

                restoration.DecodeFrame(sequence, header, frameInfo, source, boundary);
                int allocationAttempts = allocator.AllocationAttemptCount;
                restoration.DecodeFrame(sequence, header, frameInfo, source, boundary);
                Assert.Equal(allocationAttempts, allocator.AllocationAttemptCount);
                for (int row = 0; row < 64; row++)
                {
                    Assert.True(visible.DangerousGetRowSpan(row).IndexOfAnyExcept((byte)17) < 0);
                }
            }
        }

        Assert.Equal(7, allocator.AllocationAttemptCount);
        Assert.Equal(6, allocator.AllocationLog.Count);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(allocator.AllocationLog, allocation => Assert.Single(allocator.ReturnLog, x => x.HashCodeOfBuffer == allocation.HashCodeOfBuffer));
    }

    /// <summary>
    /// Verifies restoration output layout, capacity reuse across sequence changes, and exactly-once returns.
    /// </summary>
    [Theory]
    [InlineData(0, (int)Av1ColorFormat.Yuv400)]
    [InlineData(0, (int)Av1ColorFormat.Yuv420)]
    [InlineData(0, (int)Av1ColorFormat.Yuv422)]
    [InlineData(0, (int)Av1ColorFormat.Yuv444)]
    [InlineData(1, (int)Av1ColorFormat.Yuv400)]
    [InlineData(1, (int)Av1ColorFormat.Yuv420)]
    [InlineData(1, (int)Av1ColorFormat.Yuv422)]
    [InlineData(1, (int)Av1ColorFormat.Yuv444)]
    [InlineData(2, (int)Av1ColorFormat.Yuv400)]
    [InlineData(2, (int)Av1ColorFormat.Yuv420)]
    [InlineData(2, (int)Av1ColorFormat.Yuv422)]
    [InlineData(2, (int)Av1ColorFormat.Yuv444)]
    public void RestorationFrameRetainsCapacityAcrossDimensionsAndPrecision(int initialBitDepth, int colorFormatValue)
    {
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuSequenceHeader sequence = new()
        {
            MaxFrameWidth = 129,
            MaxFrameHeight = 129,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
                SubSamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
                SubSamplingY = colorFormat == Av1ColorFormat.Yuv420,
                BitDepth = (Av1BitDepth)initialBitDepth
            }
        };

        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        using Av1FrameBuffer<byte> initial = new(Configuration.Default, sequence, colorFormat, false, 65, 65);
        using (Av1FrameBuffer<byte> restored = Av1FrameBuffer<byte>.CreateRestoration(allocator, sequence, initial))
        {
            int capacity = 0;
            int owners = 0;
            int[] dimensions = [65, 65, 66, 129, 65, 65, 65];
            int[] depths =
            [
                initialBitDepth, initialBitDepth, initialBitDepth, initialBitDepth,
                (initialBitDepth + 1) % 3, (initialBitDepth + 2) % 3, initialBitDepth
            ];

            for (int frameIndex = 0; frameIndex < dimensions.Length; frameIndex++)
            {
                int dimension = dimensions[frameIndex];
                sequence.ColorConfig.BitDepth = (Av1BitDepth)depths[frameIndex];
                using Av1FrameBuffer<byte> source = new(Configuration.Default, sequence, colorFormat, false, dimension, dimension);
                Buffer2D<byte> previousLuma = restored.GetPlaneBuffer(Av1Plane.Y);
                restored.ResizeRestoration(sequence, source);

                // These fixed layout sizes include 32 border samples, eight-sample coded extents,
                // 32-sample luma row alignment, and subsampled chroma strides/heights.
                int samples = colorFormat switch
                {
                    Av1ColorFormat.Yuv400 => dimension == 129 ? 44800 : 21760,
                    Av1ColorFormat.Yuv420 => dimension == 129 ? 67200 : 32640,
                    Av1ColorFormat.Yuv422 => dimension == 129 ? 89600 : 43520,
                    _ => dimension == 129 ? 134400 : 65280
                };

                int bytesPerSample = depths[frameIndex] == 0 ? 1 : 2;
                int required = samples * bytesPerSample;
                bool grows = required > capacity;
                if (grows)
                {
                    capacity = required;
                    owners++;
                    Assert.Equal(typeof(byte), allocator.AllocationLog[^1].ElementType);
                    Assert.Equal(required, allocator.AllocationLog[^1].Length);
                }

                Assert.Equal(owners, allocator.AllocationLog.Count);
                Assert.Equal(owners - 1, allocator.ReturnLog.Count);
                Assert.Equal(dimension, restored.Width);
                Assert.Equal(dimension, restored.Height);
                Assert.Equal((Av1BitDepth)depths[frameIndex], restored.BitDepth);
                Assert.Equal(bytesPerSample, restored.BytesPerSample);
                Assert.Equal(new Point(32, 32), restored.StartPosition);
                if (frameIndex == 1)
                {
                    Assert.Same(previousLuma, restored.GetPlaneBuffer(Av1Plane.Y));
                }

                int planeCount = colorFormat == Av1ColorFormat.Yuv400 ? 1 : 3;
                for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                {
                    int subX = planeIndex > 0 && sequence.ColorConfig.SubSamplingX ? 1 : 0;
                    int subY = planeIndex > 0 && sequence.ColorConfig.SubSamplingY ? 1 : 0;
                    Span<byte> plane = restored.GetPaddedPlaneSpan((Av1Plane)planeIndex, subX, subY, out int stride, out Point origin);
                    Assert.Equal((dimension == 129 ? 224 : 160) >> subX, stride);
                    Assert.Equal(new Point(32 >> subX, 32 >> subY), origin);
                    Assert.Equal(stride * ((dimension == 129 ? 200 : 136) >> subY) * bytesPerSample, plane.Length);
                    if (grows)
                    {
                        Assert.Equal(-1, plane.IndexOfAnyExcept((byte)0));
                    }
                    else if (planeIndex == 0)
                    {
                        // Reuse preserves existing bytes. Clearing on every frame would conceal
                        // incomplete writes and add a full-frame operation to this path.
                        Assert.Equal((byte)(frameIndex + 16), plane[0]);
                    }

                    plane.Fill((byte)(frameIndex + 17));
                }
            }
        }

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        foreach (TestMemoryAllocator.AllocationRequest allocation in allocator.AllocationLog)
        {
            Assert.Single(allocator.ReturnLog, returned => returned.HashCodeOfBuffer == allocation.HashCodeOfBuffer);
        }
    }

    /// <summary>
    /// Verifies failed restoration growth returns the previous allocation and permits a subsequent resize.
    /// </summary>
    [Fact]
    public void RestorationFrameGrowthFailureReleasesOldOwnerAndAllowsRetry()
    {
        FailingTestMemoryAllocator allocator = new(2);
        ObuSequenceHeader sequence = new()
        {
            MaxFrameWidth = 129,
            MaxFrameHeight = 129,
            ColorConfig = new ObuColorConfig { IsMonochrome = true, BitDepth = Av1BitDepth.EightBit }
        };

        using Av1FrameBuffer<byte> small = new(Configuration.Default, sequence, Av1ColorFormat.Yuv400, false, 65, 65);
        using Av1FrameBuffer<byte> large = new(Configuration.Default, sequence, Av1ColorFormat.Yuv400, false, 129, 129);
        using (Av1FrameBuffer<byte> restored = Av1FrameBuffer<byte>.CreateRestoration(allocator, sequence, small))
        {
            Assert.Throws<InvalidMemoryOperationException>(() => restored.ResizeRestoration(sequence, large));
            Assert.Equal(Assert.Single(allocator.AllocationLog).HashCodeOfBuffer, Assert.Single(allocator.ReturnLog).HashCodeOfBuffer);

            restored.ResizeRestoration(sequence, large);
            Assert.Equal(3, allocator.AllocationAttemptCount);
            Assert.Equal(2, allocator.AllocationLog.Count);
            Assert.Single(allocator.ReturnLog);
            Assert.Equal(129, restored.Width);
            Assert.Equal(129, restored.Height);
            Assert.Equal(44800, allocator.AllocationLog[1].Length);
        }

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        foreach (TestMemoryAllocator.AllocationRequest allocation in allocator.AllocationLog)
        {
            Assert.Single(allocator.ReturnLog, returned => returned.HashCodeOfBuffer == allocation.HashCodeOfBuffer);
        }
    }

    /// <summary>
    /// Verifies restoration row precision, stripe contents, resizing, reuse, and exactly-once returns.
    /// </summary>
    [Theory]
    [InlineData(0, (int)Av1ColorFormat.Yuv400)]
    [InlineData(0, (int)Av1ColorFormat.Yuv420)]
    [InlineData(0, (int)Av1ColorFormat.Yuv422)]
    [InlineData(0, (int)Av1ColorFormat.Yuv444)]
    [InlineData(1, (int)Av1ColorFormat.Yuv400)]
    [InlineData(1, (int)Av1ColorFormat.Yuv420)]
    [InlineData(1, (int)Av1ColorFormat.Yuv422)]
    [InlineData(1, (int)Av1ColorFormat.Yuv444)]
    [InlineData(2, (int)Av1ColorFormat.Yuv400)]
    [InlineData(2, (int)Av1ColorFormat.Yuv420)]
    [InlineData(2, (int)Av1ColorFormat.Yuv422)]
    [InlineData(2, (int)Av1ColorFormat.Yuv444)]
    public void RestorationBoundaryReusesAlignedRowsAcrossFrames(int bitDepthIndex, int colorFormatValue)
    {
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 129,
            MaxFrameHeight = 129,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
                SubSamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
                SubSamplingY = colorFormat == Av1ColorFormat.Yuv420,
                BitDepth = (Av1BitDepth)bitDepthIndex
            }
        };

        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        int planeCount = colorFormat == Av1ColorFormat.Yuv400 ? 1 : 3;
        int bytesPerSample = bitDepthIndex == 0 ? 1 : 2;
        using (Av1LoopRestorationBoundary boundary = new(allocator))
        {
            int[] dimensions = [65, 66, 129, 65];
            for (int frameIndex = 0; frameIndex < dimensions.Length; frameIndex++)
            {
                int dimension = dimensions[frameIndex];
                using Av1FrameBuffer<byte> frame = new(Configuration.Default, sequenceHeader, colorFormat, false, dimension, dimension);
                ObuFrameHeader frameHeader = new()
                {
                    ModeInfoColumnCount = ((dimension + 7) / 8) * 2,
                    ModeInfoRowCount = ((dimension + 7) / 8) * 2,
                    FrameSize = new ObuFrameSize
                    {
                        FrameWidth = dimension,
                        SuperResolutionUpscaledWidth = dimension,
                        FrameHeight = dimension
                    }
                };

                for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                {
                    frameHeader.LoopRestorationParameters.Items[planeIndex].Type = ObuRestorationType.Wiener;
                    int subsamplingX = planeIndex != 0 && sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
                    int subsamplingY = planeIndex != 0 && sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
                    int width = (dimension + subsamplingX) >> subsamplingX;
                    int height = (dimension + subsamplingY) >> subsamplingY;
                    Buffer2D<byte> samples = frame.GetPlaneBuffer((Av1Plane)planeIndex);
                    for (int row = 0; row < height; row++)
                    {
                        Span<byte> visible = samples.DangerousGetRowSpan((frame.OriginY >> subsamplingY) + row)
                            .Slice((frame.OriginX >> subsamplingX) * bytesPerSample, width * bytesPerSample);

                        for (int column = 0; column < width; column++)
                        {
                            // The frame-dependent signal detects stale reused rows; high-bit-depth cases
                            // also carry nonzero upper bytes, so narrowing cannot pass by coincidence.
                            int value = ((row * 3) + column + planeIndex + (frameIndex * 17)) & 255;
                            if (bytesPerSample == 1)
                            {
                                visible[column] = (byte)value;
                            }
                            else
                            {
                                MemoryMarshal.Cast<byte, ushort>(visible)[column] = (ushort)(value + 512);
                            }
                        }
                    }
                }

                boundary.SaveDeblockedRows(sequenceHeader, frameHeader, frame);
                boundary.SaveFrameEdgeRows(sequenceHeader, frameHeader, frame);
                Span<ushort> savedRows = boundary.GetStripeSaveBuffer();
                Assert.Equal(2352, savedRows.Length);
                Assert.Equal(typeof(ushort), allocator.AllocationLog[0].ElementType);
                Assert.Equal(2352, allocator.AllocationLog[0].Length);
                if (frameIndex > 0)
                {
                    Assert.Equal((ushort)frameIndex, savedRows[0]);
                }

                savedRows.Fill((ushort)(frameIndex + 1));
                int ownerGroups = frameIndex == 0 ? 1 : frameIndex;
                Assert.Equal(1 + (ownerGroups * planeCount * 2), allocator.AllocationLog.Count);
                Assert.Equal((ownerGroups - 1) * planeCount * 2, allocator.ReturnLog.Count);
                int groupStart = 1 + ((ownerGroups - 1) * planeCount * 2);
                int stripeCount = dimension == 129 ? 3 : 2;
                for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
                {
                    int subsamplingX = planeIndex != 0 && sequenceHeader.ColorConfig.SubSamplingX ? 1 : 0;
                    int subsamplingY = planeIndex != 0 && sequenceHeader.ColorConfig.SubSamplingY ? 1 : 0;
                    int width = (dimension + subsamplingX) >> subsamplingX;
                    int height = (dimension + subsamplingY) >> subsamplingY;
                    int rowStride = dimension == 129 ? (subsamplingX == 0 ? 160 : 96) : (subsamplingX == 0 ? 96 : 64);
                    for (int side = 0; side < 2; side++)
                    {
                        TestMemoryAllocator.AllocationRequest allocation = allocator.AllocationLog[groupStart + (planeIndex * 2) + side];
                        Assert.Equal(typeof(byte), allocation.ElementType);
                        Assert.Equal(stripeCount * 2 * rowStride * bytesPerSample, allocation.Length);
                        for (int stripe = 0; stripe < stripeCount; stripe++)
                        {
                            for (int contextRow = 0; contextRow < 2; contextRow++)
                            {
                                ReadOnlySpan<byte> row = side == 0
                                    ? boundary.GetRowAbove(planeIndex, stripe, contextRow)
                                    : boundary.GetRowBelow(planeIndex, stripe, contextRow);

                                int sourceRow = side == 0
                                    ? Math.Max(0, (((stripe * 64) - 8) >> subsamplingY) - 2 + contextRow)
                                    : Math.Min(height - 1, ((((stripe + 1) * 64) - 8) >> subsamplingY) + contextRow);

                                Assert.Equal(width * bytesPerSample, row.Length);
                                for (int column = 0; column < width; column++)
                                {
                                    int expected = ((sourceRow * 3) + column + planeIndex + (frameIndex * 17)) & 255;
                                    int actual = bytesPerSample == 1 ? row[column] : MemoryMarshal.Cast<byte, ushort>(row)[column];
                                    Assert.Equal(expected + (bytesPerSample == 1 ? 0 : 512), actual);
                                }
                            }
                        }
                    }
                }
            }
        }

        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        foreach (TestMemoryAllocator.AllocationRequest allocation in allocator.AllocationLog)
        {
            Assert.Single(allocator.ReturnLog, returned => returned.HashCodeOfBuffer == allocation.HashCodeOfBuffer);
        }
    }

    /// <summary>
    /// Verifies partial boundary allocation is released by session disposal, including failed replacement.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void RestorationBoundaryAllocationFailureReturnsEveryOwner(int failureAllocationNumber)
    {
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber);
        ObuSequenceHeader sequence = new()
        {
            MaxFrameWidth = 129,
            MaxFrameHeight = 129,
            ColorConfig = new ObuColorConfig { IsMonochrome = true, BitDepth = Av1BitDepth.EightBit }
        };

        using Av1FrameBuffer<byte> frame = new(Configuration.Default, sequence, Av1ColorFormat.Yuv400, false);
        ObuFrameHeader header = new()
        {
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16,
            FrameSize = new ObuFrameSize { FrameWidth = 64, SuperResolutionUpscaledWidth = 64, FrameHeight = 64 }
        };

        header.LoopRestorationParameters.Items[0].Type = ObuRestorationType.Wiener;
        using (Av1LoopRestorationBoundary boundary = new(allocator))
        {
            if (failureAllocationNumber == 5)
            {
                boundary.SaveDeblockedRows(sequence, header, frame);
                header.ModeInfoColumnCount = 34;
                header.ModeInfoRowCount = 34;
                header.FrameSize.FrameWidth = 129;
                header.FrameSize.SuperResolutionUpscaledWidth = 129;
                header.FrameSize.FrameHeight = 129;
            }

            Assert.Throws<InvalidMemoryOperationException>(() => boundary.SaveDeblockedRows(sequence, header, frame));
            Assert.Equal(failureAllocationNumber, allocator.AllocationAttemptCount);
        }

        Assert.Equal(failureAllocationNumber - 1, allocator.AllocationLog.Count);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        foreach (TestMemoryAllocator.AllocationRequest allocation in allocator.AllocationLog)
        {
            Assert.Single(allocator.ReturnLog, returned => returned.HashCodeOfBuffer == allocation.HashCodeOfBuffer);
        }
    }

    [Theory]
    [InlineData(0, (int)Av1ColorFormat.Yuv400, 448)]
    [InlineData(0, (int)Av1ColorFormat.Yuv420, 672)]
    [InlineData(0, (int)Av1ColorFormat.Yuv422, 896)]
    [InlineData(0, (int)Av1ColorFormat.Yuv444, 1344)]
    [InlineData(1, (int)Av1ColorFormat.Yuv400, 672)]
    [InlineData(1, (int)Av1ColorFormat.Yuv420, 1008)]
    [InlineData(1, (int)Av1ColorFormat.Yuv422, 1344)]
    [InlineData(1, (int)Av1ColorFormat.Yuv444, 2016)]
    [InlineData(2, (int)Av1ColorFormat.Yuv400, 672)]
    [InlineData(2, (int)Av1ColorFormat.Yuv420, 1008)]
    [InlineData(2, (int)Av1ColorFormat.Yuv422, 1344)]
    [InlineData(2, (int)Av1ColorFormat.Yuv444, 2016)]
    public void PresentationUsesVisibleExtentWithoutPredictionBorder(int bitDepthIndex, int colorFormatValue, int expectedLength)
    {
        Av1ColorFormat colorFormat = (Av1ColorFormat)colorFormatValue;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = colorFormat == Av1ColorFormat.Yuv400,
                SubSamplingX = colorFormat is Av1ColorFormat.Yuv420 or Av1ColorFormat.Yuv422,
                SubSamplingY = colorFormat == Av1ColorFormat.Yuv420,
                BitDepth = (Av1BitDepth)bitDepthIndex
            }
        };

        using Av1FrameBuffer<byte> source = new(Configuration.Default, sequenceHeader, colorFormat, false);
        source.Width = 19;
        source.Height = 13;
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1FrameBuffer<byte> presentation = Av1FrameBuffer<byte>.CreatePresentation(configuration, sequenceHeader, source))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Equal(expectedLength, allocation.Length);
            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(Point.Empty, presentation.StartPosition);
            Assert.Equal(0, presentation.OriginX);
            Assert.Equal(0, presentation.OriginY);
            Assert.Equal(19, presentation.MaxWidth);
            Assert.Equal(13, presentation.MaxHeight);
            int planeCount = colorFormat == Av1ColorFormat.Yuv400 ? 1 : 3;
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                source.GetPlaneBuffer((Av1Plane)planeIndex).DangerousGetSingleSpan().Fill((byte)(planeIndex + 1));
                presentation.GetPlaneBuffer((Av1Plane)planeIndex).DangerousGetSingleSpan().Fill(0xA5);
            }

            source.CopyVisibleTo(presentation);
            Assert.Equal(19, presentation.Width);
            Assert.Equal(13, presentation.Height);
            Assert.Equal(0, presentation.OriginX);
            Assert.Equal(0, presentation.OriginY);

            // Odd pictures reserve one extension row; the byte stride is 16-aligned at each sample precision.
            for (int planeIndex = 0; planeIndex < planeCount; planeIndex++)
            {
                Buffer2D<byte> plane = presentation.GetPlaneBuffer((Av1Plane)planeIndex);
                int visibleWidth = planeIndex == 0 || colorFormat == Av1ColorFormat.Yuv444 ? 19 : 10;
                int visibleHeight = planeIndex != 0 && colorFormat == Av1ColorFormat.Yuv420 ? 7 : 13;
                int visibleBytes = visibleWidth * (bitDepthIndex == 0 ? 1 : 2);
                for (int row = 0; row < plane.Height; row++)
                {
                    ReadOnlySpan<byte> samples = plane.DangerousGetRowSpan(row);
                    for (int column = 0; column < samples.Length; column++)
                    {
                        byte expected = row < visibleHeight && column < visibleBytes ? (byte)(planeIndex + 1) : (byte)0xA5;
                        Assert.Equal(expected, samples[column]);
                    }
                }
            }

            // The complete presentation still belongs to the one frame owner.
            Buffer2D<byte> luma = presentation.GetPlaneBuffer(Av1Plane.Y);
            Assert.Equal(bitDepthIndex == 0 ? 32 : 48, luma.Width);
            Assert.Equal(14, luma.Height);
            Assert.Single(luma.MemoryGroup);
            if (colorFormat != Av1ColorFormat.Yuv400)
            {
                Buffer2D<byte> chroma = presentation.GetPlaneBuffer(Av1Plane.U);
                Assert.Equal(colorFormat == Av1ColorFormat.Yuv444 ? luma.Width : luma.Width / 2, chroma.Width);
                Assert.Equal(colorFormat == Av1ColorFormat.Yuv420 ? 7 : 14, chroma.Height);
                Assert.Single(chroma.MemoryGroup);
                Assert.Single(presentation.GetPlaneBuffer(Av1Plane.V).MemoryGroup);
            }
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    /// <summary>
    /// Verifies that padded frame planes remain contiguous when the allocator would otherwise split the buffer.
    /// </summary>
    [Fact]
    public void ConstructorRequestsContiguousPaddedPlanes()
    {
        TestMemoryAllocator allocator = new() { BufferCapacityInBytes = 10_000 };
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        using Av1FrameBuffer<byte> frameBuffer = new(
            configuration,
            sequenceHeader,
            Av1ColorFormat.Yuv400,
            false);

        MemoryGroup<byte> memoryGroup = frameBuffer.GetPlaneBuffer(Av1Plane.Y).FastMemoryGroup;
        Assert.Equal(1, memoryGroup.Count);
        Assert.True(memoryGroup.TotalLength > allocator.BufferCapacityInBytes);
    }

    /// <summary>
    /// Verifies that an external frame geometry cannot make the padded-plane owner fall back to multiple groups.
    /// </summary>
    [Fact]
    public void ConstructorRejectsPaddedPlaneThatCannotBeContiguous()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 65_536,
            MaxFrameHeight = 65_536,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        Assert.Throws<InvalidImageContentException>(
            () => new Av1FrameBuffer<byte>(configuration, sequenceHeader, Av1ColorFormat.Yuv400, false));

        Assert.Empty(allocator.AllocationLog);
    }

    /// <summary>
    /// Verifies that all padded component planes share one frame owner.
    /// </summary>
    [Fact]
    public void ConstructorUsesOneFrameOwnerForAllPaddedPlanes()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = false,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        TestMemoryAllocator.AllocationRequest allocation;
        using (Av1FrameBuffer<byte> frameBuffer = new(
            configuration,
            sequenceHeader,
            Av1ColorFormat.Yuv420,
            false))
        {
            allocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(byte), allocation.ElementType);
            Assert.Equal(614_400, allocation.Length);
            Assert.Single(frameBuffer.GetPlaneBuffer(Av1Plane.Y).MemoryGroup);
            Assert.Single(frameBuffer.GetPlaneBuffer(Av1Plane.U).MemoryGroup);
            Assert.Single(frameBuffer.GetPlaneBuffer(Av1Plane.V).MemoryGroup);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    /// <summary>
    /// Verifies that block reconstruction uses one exact-size owner across monochrome and chroma plane layouts.
    /// </summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void BlockDecoderUsesOneContiguousWorkspaceOwner(
        bool isMonochrome,
        bool subsamplingX,
        bool subsamplingY)
    {
        TestMemoryAllocator allocator = new();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = isMonochrome,
                SubSamplingX = subsamplingX,
                SubSamplingY = subsamplingY,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        Av1ColorFormat colorFormat = isMonochrome
            ? Av1ColorFormat.Yuv400
            : subsamplingX
                ? subsamplingY ? Av1ColorFormat.Yuv420 : Av1ColorFormat.Yuv422
                : Av1ColorFormat.Yuv444;

        using Av1FrameBuffer<byte> frameBuffer = new(configuration, sequenceHeader, colorFormat, false);
        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16
        };

        using Av1ReferenceFrameStore referenceFrames = new();

        // Reset frame-plane logs so these assertions describe the supplied workspace and the borrowing decoder.
        allocator.EnableNonThreadSafeLogging();

        int maximumBlockLength = 1 << sequenceHeader.SuperblockSizeLog2;
        int maximumBlockArea = maximumBlockLength * maximumBlockLength;
        int predictorWorkingLength = Math.Max(
            Av1PredictionDecoder.ScratchLength,
            Math.Max(
                Av1TranslationalInterPredictor.GetScratchLength(maximumBlockLength, maximumBlockLength),
                Av1ScaledInterPredictor.GetMaximumScaledScratchLength(maximumBlockLength, maximumBlockLength)));

        int predictionScratchLength =
            (2 * maximumBlockArea) +
            ((maximumBlockArea + 1) >> 1) +
            predictorWorkingLength +
            Av1ChromaFromLumaContext.BufferLength;

        int expectedWorkspaceLength =
            (Av1TransformWorkspace.InverseMaximumLength * 2) +
            predictionScratchLength;

        TestMemoryAllocator.AllocationRequest workspaceAllocation;
        using (IMemoryOwner<short> workspace = allocator.Allocate<short>(Av1BlockDecoder.GetWorkspaceLength(sequenceHeader)))
        {
            Av1BlockDecoder blockDecoder = new(
                sequenceHeader,
                frameHeader,
                frameBuffer,
                referenceFrames,
                workspace.Memory);

            workspaceAllocation = Assert.Single(allocator.AllocationLog);
            Assert.Empty(allocator.ReturnLog);
            Assert.Equal(typeof(short), workspaceAllocation.ElementType);
            Assert.Equal(expectedWorkspaceLength, workspaceAllocation.Length);
        }

        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(workspaceAllocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    /// <summary>
    /// Verifies that active-superblock coefficient scratch uses one configured allocator lease and omits unused
    /// chroma storage for a monochrome frame.
    /// </summary>
    [Fact]
    [ValidateDisposedMemoryAllocations]
    public void FrameInfoCoefficientScratchUsesConfiguredAllocator()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        ObuFrameHeader frameHeader = new()
        {
            FrameSize = new ObuFrameSize
            {
                FrameWidth = 64,
                FrameHeight = 64
            }
        };

        Av1FrameInfo frameInfo = new(configuration, sequenceHeader, frameHeader);
        int expectedCoefficientCount = 16 * 16 * Av1FrameInfo.CoefficientCountPerModeInfo;
        TestMemoryAllocator.AllocationRequest coefficientScratch = Assert.Single(
            allocator.AllocationLog,
            request => request.ElementType == typeof(int) && request.Length == expectedCoefficientCount);

        Assert.Equal(expectedCoefficientCount, frameInfo.GetCoefficientsY().Length);
        Assert.Equal(0, frameInfo.GetCoefficientsU().Length);
        Assert.Equal(0, frameInfo.GetCoefficientsV().Length);

        frameInfo.Dispose();

        Assert.Single(
            allocator.ReturnLog,
            returned => returned.HashCodeOfBuffer == coefficientScratch.HashCodeOfBuffer);
    }

    /// <summary>
    /// Verifies that a later frame-state allocation failure returns the coefficient scratch rented first.
    /// </summary>
    [Fact]
    public void FrameInfoConstructorFailureReleasesCoefficientScratch()
    {
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber: 2);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = true,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        ObuFrameHeader frameHeader = new()
        {
            FrameSize = new ObuFrameSize
            {
                FrameWidth = 64,
                FrameHeight = 64
            }
        };

        Assert.Throws<InvalidMemoryOperationException>(() => new Av1FrameInfo(configuration, sequenceHeader, frameHeader));

        TestMemoryAllocator.AllocationRequest coefficientScratch = Assert.Single(allocator.AllocationLog);
        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);
        Assert.Equal(typeof(int), coefficientScratch.ElementType);
        Assert.Equal(2, allocator.AllocationAttemptCount);
        Assert.Equal(coefficientScratch.HashCodeOfBuffer, returned.HashCodeOfBuffer);
    }

    /// <summary>
    /// Provides tracked plane owners until the configured allocation attempt fails.
    /// </summary>
    private sealed class FailingTestMemoryAllocator : TestMemoryAllocator
    {
        private readonly int failureAllocationNumber;
        private int allocationAttemptCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="FailingTestMemoryAllocator"/> class.
        /// </summary>
        /// <param name="failureAllocationNumber">The one-based allocation attempt that must fail.</param>
        public FailingTestMemoryAllocator(int failureAllocationNumber)
        {
            this.failureAllocationNumber = failureAllocationNumber;
            this.EnableNonThreadSafeLogging();
        }

        /// <summary>
        /// Gets the number of backing-owner allocation attempts made through this allocator.
        /// </summary>
        public int AllocationAttemptCount => this.allocationAttemptCount;

        /// <inheritdoc/>
        protected override AllocationTrackedMemoryManager<T> AllocateCore<T>(int length, AllocationOptions options = AllocationOptions.None)
        {
            this.allocationAttemptCount++;

            if (this.allocationAttemptCount == this.failureAllocationNumber)
            {
                // Fail before delegating so this attempt never creates or tracks an owner. Diagnostic counts then
                // describe only the successfully published owners that the constructor is responsible for releasing.
                throw new InvalidMemoryOperationException("The configured AV1 allocation failed.");
            }

            return base.AllocateCore<T>(length, options);
        }
    }
}
