// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
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

        MemoryGroup<byte> memoryGroup = frameBuffer.BufferY!.FastMemoryGroup;
        Assert.Equal(1, memoryGroup.Count);
        Assert.True(memoryGroup.TotalLength > allocator.BufferCapacityInBytes);
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
        Assert.Equal(allocator.AllocationLog[0].HashCodeOfBuffer, allocator.ReturnLog[0].HashCodeOfBuffer);
        Assert.Equal(allocator.AllocationLog[1].HashCodeOfBuffer, allocator.ReturnLog[1].HashCodeOfBuffer);
    }

    /// <summary>
    /// Verifies that a block-decoder workspace failure releases every workspace rented earlier in construction.
    /// </summary>
    [Fact]
    public void BlockDecoderConstructorFailureReleasesEarlierWorkspaces()
    {
        FailingTestMemoryAllocator allocator = new(failureAllocationNumber: 3);
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
        ObuFrameHeader frameHeader = new();
        Av1LoopFilterContext loopFilterContext = new(sequenceHeader);
        Av1InverseQuantizer inverseQuantizer = new(sequenceHeader, frameHeader);

        // The frame's luma plane is allocation attempt one. Resetting only the logs preserves that counter, so the
        // inverse-quantization workspace succeeds on attempt two and the transform workspace fails on attempt three.
        allocator.EnableNonThreadSafeLogging();

        Assert.Throws<InvalidMemoryOperationException>(
            () => new Av1BlockDecoder(sequenceHeader, frameHeader, frameBuffer, loopFilterContext, inverseQuantizer));

        Assert.Equal(3, allocator.AllocationAttemptCount);
        Assert.Single(allocator.AllocationLog);
        Assert.Single(allocator.ReturnLog);
        Assert.Equal(allocator.AllocationLog[0].HashCodeOfBuffer, allocator.ReturnLog[0].HashCodeOfBuffer);
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
