// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.ReferenceFrames;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 frame-plane allocation contracts and constructor rollback ownership.
/// </summary>
[Trait("Format", "Avif")]
public class Av1FrameBufferTests
{
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
    /// Verifies that a failure while renting the final chroma plane releases every previously rented plane.
    /// </summary>
    [Fact]
    public void ConstructorFailureOnThirdPlaneReleasesEarlierPlanes()
    {
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber: 3);
        Configuration configuration = Configuration.Default.Clone();
        configuration.MemoryAllocator = allocator;
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 1,
            MaxFrameHeight = 1,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = false,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        // All three padded planes fit in one backing owner each, making attempt three the Cr plane rent after Y and
        // Cb have succeeded. The allocator log therefore contains exactly the two owners requiring rollback.
        Assert.Throws<InvalidMemoryOperationException>(
            () => new Av1FrameBuffer<byte>(configuration, sequenceHeader, Av1ColorFormat.Yuv420, false));

        Assert.Equal(3, allocator.AllocationAttemptCount);
        Assert.Equal(2, allocator.AllocationLog.Count);
        Assert.Equal(2, allocator.ReturnLog.Count);
        Assert.All(
            allocator.AllocationLog,
            allocation => Assert.Single(
                allocator.ReturnLog,
                returned => returned.AllocationId == allocation.AllocationId));
    }

    /// <summary>
    /// Verifies that each later block-decoder workspace failure releases every workspace rented earlier.
    /// </summary>
    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    public void BlockDecoderConstructorFailureReleasesEarlierWorkspaces(
        int failureAllocationNumber,
        int successfulWorkspaceCount)
    {
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber);
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
                BitDepth = Av1BitDepth.EightBit
            }
        };

        using Av1FrameBuffer<byte> frameBuffer = new(configuration, sequenceHeader, Av1ColorFormat.Yuv400, false);
        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16
        };

        using Av1LoopFilterContext loopFilterContext =
            new(Configuration.Default.MemoryAllocator, sequenceHeader, frameHeader);

        Av1InverseQuantizer inverseQuantizer = new(sequenceHeader, frameHeader);
        using Av1ReferenceFrameStore referenceFrames = new();

        // The frame's luma plane is allocation attempt one. Resetting only the logs preserves that counter while
        // isolating the block-decoder owners that must be returned when a later workspace rent fails.
        allocator.EnableNonThreadSafeLogging();

        Assert.Throws<InvalidMemoryOperationException>(
            () => new Av1BlockDecoder(
                sequenceHeader,
                frameHeader,
                frameBuffer,
                loopFilterContext,
                inverseQuantizer,
                referenceFrames));

        Assert.Equal(failureAllocationNumber, allocator.AllocationAttemptCount);
        Assert.Equal(successfulWorkspaceCount, allocator.AllocationLog.Count);
        Assert.Equal(successfulWorkspaceCount, allocator.ReturnLog.Count);
        foreach (TestMemoryAllocator.AllocationRequest allocation in allocator.AllocationLog)
        {
            Assert.Contains(
                allocator.ReturnLog,
                returned => returned.HashCodeOfBuffer == allocation.HashCodeOfBuffer);
        }
    }

    /// <summary>
    /// Verifies that failure to allocate the active chroma transform map releases the preceding luma map.
    /// </summary>
    [Fact]
    public void LoopFilterContextAllocationFailureReleasesLumaMap()
    {
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber: 2);
        ObuSequenceHeader sequenceHeader = new()
        {
            MaxFrameWidth = 64,
            MaxFrameHeight = 64,
            Use128x128Superblock = false,
            ColorConfig = new ObuColorConfig
            {
                IsMonochrome = false,
                SubSamplingX = true,
                SubSamplingY = true,
                BitDepth = Av1BitDepth.EightBit
            }
        };

        ObuFrameHeader frameHeader = new()
        {
            ModeInfoColumnCount = 16,
            ModeInfoRowCount = 16
        };

        Assert.Throws<InvalidMemoryOperationException>(
            () => new Av1LoopFilterContext(allocator, sequenceHeader, frameHeader));

        TestMemoryAllocator.AllocationRequest allocation = Assert.Single(allocator.AllocationLog);
        TestMemoryAllocator.ReturnRequest returned = Assert.Single(allocator.ReturnLog);

        Assert.Equal(2, allocator.AllocationAttemptCount);
        Assert.Equal(allocation.HashCodeOfBuffer, returned.HashCodeOfBuffer);
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
