// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1GoodQualityReferenceStructureTests
{
    /// <summary>
    /// Verifies the reference slots, refreshed slots, and primary references of the first frames after a key frame.
    /// The expected values are those that aomenc writes for good-quality coding without lookahead: the key frame
    /// fills every slot, each later frame refreshes the next empty slot, GOLDEN keeps the key frame once a second
    /// buffer exists, and the first inter frame has no earlier frame of its reference type to take a context from.
    /// </summary>
    [Fact]
    public void LowDelayPyramidMatchesReferenceEncoderSlots()
    {
        uint[][] expectedIndices =
        [
            [0, 0, 0, 0, 0, 0, 0],
            [1, 0, 0, 0, 0, 0, 0],
            [2, 1, 0, 0, 0, 0, 0],
            [3, 2, 1, 0, 0, 0, 0],
            [4, 3, 2, 0, 1, 0, 0]
        ];

        uint[] expectedPrimary = [Av1Constants.PrimaryReferenceFrameNone, 0, 0, 0, 0];
        Av1GoodQualityReferenceStructure structure = new();
        ObuFrameHeader frameHeader = new() { FrameType = ObuFrameType.KeyFrame, ErrorResilientMode = true };
        structure.Configure(frameHeader, 0);
        Assert.Equal(Av1FrameUpdateType.Key, structure.UpdateType);
        Assert.Equal(byte.MaxValue, frameHeader.RefreshFrameFlags);
        Assert.Equal(Av1Constants.PrimaryReferenceFrameNone, frameHeader.PrimaryReferenceFrame);
        structure.Complete(frameHeader);

        for (int frame = 1; frame <= expectedIndices.Length; frame++)
        {
            frameHeader.FrameType = ObuFrameType.InterFrame;
            frameHeader.ErrorResilientMode = false;
            structure.Configure(frameHeader, frame);
            Assert.Equal(Av1FrameUpdateType.Last, structure.UpdateType);
            Assert.Equal(expectedIndices[frame - 1], frameHeader.GetReferenceFrameIndices().ToArray());
            Assert.Equal(1U << frame, frameHeader.RefreshFrameFlags);
            Assert.Equal(expectedPrimary[frame - 1], frameHeader.PrimaryReferenceFrame);
            structure.Complete(frameHeader);
        }
    }

    /// <summary>
    /// Verifies that a golden group holds 32 frames, so the 33rd frame after a key frame starts a new group.
    /// </summary>
    [Fact]
    public void GoldenGroupStartsAfterThirtyTwoFrames()
    {
        Av1GoodQualityReferenceStructure structure = new();
        ObuFrameHeader frameHeader = new() { FrameType = ObuFrameType.KeyFrame, ErrorResilientMode = true };
        structure.Configure(frameHeader, 0);
        structure.Complete(frameHeader);
        frameHeader.FrameType = ObuFrameType.InterFrame;
        frameHeader.ErrorResilientMode = false;
        for (int frame = 1; frame <= 32; frame++)
        {
            structure.Configure(frameHeader, frame);
            Assert.Equal(frame == 32 ? Av1FrameUpdateType.Golden : Av1FrameUpdateType.Last, structure.UpdateType);
            structure.Complete(frameHeader);
        }
    }
}
