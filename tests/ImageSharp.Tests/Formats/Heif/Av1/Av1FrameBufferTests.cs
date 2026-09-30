// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopRestoration;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.Memory;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 frame-plane allocation contracts.
/// </summary>
[Trait("Format", "Avif")]
public class Av1FrameBufferTests
{
    /// <summary>
    /// Verifies initial and growing restoration-stage allocations unwind and recover without replacing live owners twice.
    /// </summary>
    [Theory]
    [InlineData(4)]
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
                Av1PlaneRegion<byte> visible = source.DeriveBlockPointer(Av1Plane.Y, 0, 0);
                for (int row = 0; row < 64; row++)
                {
                    visible.GetRowSpan(row).Fill(17);
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
                    Assert.True(visible.GetRowSpan(row).IndexOfAnyExcept((byte)17) < 0);
                }
            }
        }

        Assert.Equal(7, allocator.AllocationAttemptCount);
        Assert.Equal(6, allocator.AllocationLog.Count);
        Assert.Equal(allocator.AllocationLog.Count, allocator.ReturnLog.Count);
        Assert.All(allocator.AllocationLog, allocation => Assert.Single(allocator.ReturnLog, x => x.HashCodeOfBuffer == allocation.HashCodeOfBuffer));
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
