// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Group;

/// <summary>
/// Owns the sample destinations and coefficient workspace reused by sequential groups and frames in one codestream.
/// </summary>
/// <remarks>
/// Channel planes contain one group's encoded-resolution samples. They do not contain neighboring groups or filter halos.
/// Reconstruction retains cross-group borders separately after it resolves the active filters and each channel's sampling rate.
/// Borrowed memory and row spans must not survive workspace growth or disposal.
/// </remarks>
internal sealed class JxlGroupDecoderCache : IDisposable
{
    /// <summary>
    /// The two neighboring samples required on each side by the five-tap DC upsampling kernel.
    /// </summary>
    public const int DcPadding = 2;

    /// <summary>
    /// The configuration that owns sample and coefficient storage.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The parsed frame grid used to locate partial groups.
    /// </summary>
    private JxlFrameDimensions frameDimensions;

    /// <summary>
    /// The maximum encoded-resolution group extent for each channel.
    /// </summary>
    private readonly Size[] channelGroupSizes;

    /// <summary>
    /// The full encoded-resolution frame extent for each channel, including required DCT padding.
    /// </summary>
    private readonly Size[] channelFrameSizes;

    /// <summary>
    /// Owns the color and extra-channel destinations for the current group.
    /// </summary>
    private readonly JxlPlane<float>[] channels;

    /// <summary>
    /// Owns three floating-point coefficient blocks and transform scratch.
    /// </summary>
    private IMemoryOwner<float>? floatMemory;

    /// <summary>
    /// Owns the full-width quantized coefficient blocks.
    /// </summary>
    private IMemoryOwner<int>? int32Memory;

    /// <summary>
    /// Owns the narrow quantized coefficient blocks.
    /// </summary>
    private IMemoryOwner<short>? int16Memory;

    /// <summary>
    /// The largest transform area supported by the current workspace.
    /// </summary>
    private int maxBlockArea;

    /// <summary>
    /// Allocates reusable group destinations from a parsed frame's channel dimensions.
    /// </summary>
    /// <param name="configuration">The configuration that owns all numeric allocations.</param>
    /// <param name="frameHeader">The parsed frame settings and channel sampling rates.</param>
    public JxlGroupDecoderCache(Configuration configuration, JxlFrameHeader frameHeader)
    {
        this.configuration = configuration;
        this.frameDimensions = frameHeader.FrameDimensions;

        int channelCount = 3 + frameHeader.ExtraChannelUpsampling.Count;
        this.channels = new JxlPlane<float>[channelCount];
        this.channelGroupSizes = new Size[channelCount];
        this.channelFrameSizes = new Size[channelCount];
        try
        {
            this.PrepareFrame(frameHeader);
        }
        catch
        {
            // No channel storage escapes a failed constructor. Entries after the failed allocation are still absent.
            this.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets the owned destination planes. The caller borrows these owners without replacing or disposing them.
    /// </summary>
    public ReadOnlySpan<JxlPlane<float>> Channels => this.channels;

    /// <summary>
    /// Gets the three consecutive floating-point coefficient blocks and the following transform scratch.
    /// </summary>
    public Memory<float> DecodeGroupBlockScratchSpace { get; private set; }

    /// <summary>
    /// Gets the three consecutive full-width quantized coefficient blocks.
    /// </summary>
    public Memory<int> DecodeGroupQBlockScratchSpace { get; private set; }

    /// <summary>
    /// Gets the three consecutive narrow quantized coefficient blocks.
    /// </summary>
    public Memory<short> DecodeGroupQBlock16ScratchSpace { get; private set; }

    /// <summary>
    /// Gets the transform scratch that follows the three floating-point coefficient blocks.
    /// </summary>
    public Memory<float> ScratchSpaceMemory { get; private set; }

    /// <summary>
    /// Gets the owned nonzero-count history, stored as consecutive pass, channel and block rows.
    /// </summary>
    public JxlPlane<int> NumberOfNonZeroes { get; } = new();

    /// <summary>
    /// Gets the sample count between channel origins in the nonzero-count allocation, including row padding.
    /// </summary>
    public int NonZeroChannelStride { get; private set; }

    /// <summary>
    /// Gets the owned DC upsampling input, including two neighboring rows and columns on each side.
    /// </summary>
    public JxlPlane<float> DcBuffer { get; } = new();

    /// <summary>
    /// Applies the next parsed frame's sampling rates and reuses destination allocations when their capacities permit.
    /// </summary>
    /// <param name="frameHeader">The next parsed frame header from the same codestream.</param>
    public void PrepareFrame(JxlFrameHeader frameHeader)
    {
        this.frameDimensions = frameHeader.FrameDimensions;

        // The codestream metadata fixes the number of channels. Frames can change sampling rates and group dimensions,
        // but retain the same channel owners. All allocation and logical-size changes occur before group traversal.
        for (int channel = 0; channel < this.channels.Length; channel++)
        {
            Size groupSize;
            Size frameSize;
            if (channel < 3)
            {
                int horizontalShift = frameHeader.ChromaSubsampling.HShift(channel);
                int verticalShift = frameHeader.ChromaSubsampling.VShift(channel);
                groupSize = new(this.frameDimensions.GroupDimension >> horizontalShift, this.frameDimensions.GroupDimension >> verticalShift);
                frameSize = new(
                    JxlMath.DivCeil(this.frameDimensions.XSizePadded, 1 << horizontalShift),
                    JxlMath.DivCeil(this.frameDimensions.YSizePadded, 1 << verticalShift));
            }
            else
            {
                // Extra-channel factors include their metadata dimension shift and refer to the upsampled frame.
                // Express the group in that same coordinate system before dividing by the channel's factor.
                int upsampling = (int)frameHeader.ExtraChannelUpsampling[channel - 3];
                int groupDimension = this.frameDimensions.GroupDimension * (int)frameHeader.Upsampling / upsampling;
                groupSize = new(groupDimension, groupDimension);
                frameSize = new(
                    JxlMath.DivCeil(this.frameDimensions.XSizeUpsampledPadded, upsampling),
                    JxlMath.DivCeil(this.frameDimensions.YSizeUpsampledPadded, upsampling));
            }

            JxlPlane<float>? current = this.channels[channel];
            if (current is null)
            {
                this.channels[channel] = new(this.configuration, groupSize.Width, groupSize.Height);
            }
            else
            {
                current.Resize(this.configuration, groupSize.Width, groupSize.Height);
            }

            this.channelGroupSizes[channel] = groupSize;
            this.channelFrameSizes[channel] = frameSize;
        }
    }

    /// <summary>
    /// Gets a group's local destination rectangle in one channel's encoded sample coordinates.
    /// </summary>
    /// <param name="groupId">The row-major group index in the parsed frame grid.</param>
    /// <param name="channel">The color or extra-channel index.</param>
    /// <returns>The local rectangle, clipped to the encoded frame extent for a partial right or bottom group.</returns>
    public Rectangle GetChannelBounds(int groupId, int channel)
    {
        (int groupY, int groupX) = Math.DivRem(groupId, this.frameDimensions.XSizeGroups);
        Size groupSize = this.channelGroupSizes[channel];
        Size frameSize = this.channelFrameSizes[channel];

        return new(
            0,
            0,
            Math.Min(groupSize.Width, frameSize.Width - (groupX * groupSize.Width)),
            Math.Min(groupSize.Height, frameSize.Height - (groupY * groupSize.Height)));
    }

    /// <summary>
    /// Allocates the five-tap DC input once, retaining it for later groups in this frame.
    /// </summary>
    public void InitializeDcBufferOnce()
    {
        int blockDimension = this.frameDimensions.GroupDimension / JxlFrameDimensions.BlockDimensions;
        int paddedDimension = blockDimension + (2 * DcPadding);

        // Pixel zero of the logical DC block is at (DcPadding,DcPadding). No scheduler-specific row offset is required.
        this.DcBuffer.Resize(this.configuration, paddedDimension, paddedDimension);
    }

    /// <summary>
    /// Prepares nonzero-count history and grows coefficient scratch only when a larger transform is required.
    /// </summary>
    /// <param name="numPasses">The parsed number of requested coefficient passes.</param>
    /// <param name="usedAcStrategies">The decoded bit mask of transform strategies used by this frame.</param>
    public void InitializeOnce(int numPasses, int usedAcStrategies)
    {
        int blockDimension = this.frameDimensions.GroupDimension / JxlFrameDimensions.BlockDimensions;

        if (numPasses != 0)
        {
            // Every local pass retains three independent block grids. One physical stride serves all channels and passes.
            // Partial groups use a subset. ShrinkTo retains the allocation and can restore larger logical dimensions later.
            int historyHeight = numPasses * 3 * blockDimension;
            this.NumberOfNonZeroes.Resize(this.configuration, blockDimension, historyHeight);
            this.NonZeroChannelStride = this.NumberOfNonZeroes.PixelsPerRow * blockDimension;

            // Native prediction ignores the missing top and left neighbors at a group boundary.
            // Each decoded transform writes its whole covered region before a later transform reads it. No history clear is required.
        }

        int requiredBlockArea = 0;

        for (int strategy = 0; strategy < JxlAcStrategy.NumberOfValidStrategies; strategy++)
        {
            if ((usedAcStrategies & (1 << strategy)) == 0)
            {
                continue;
            }

            JxlAcStrategy transform = new(strategy);
            int area = transform.CoveredBlocksX * transform.CoveredBlocksY * JxlFrameDimensions.DctBlockSize;
            requiredBlockArea = Math.Max(area, requiredBlockArea);
        }

        if (requiredBlockArea > this.maxBlockArea)
        {
            IMemoryOwner<float> floatMemory = this.configuration.MemoryAllocator.Allocate<float>(requiredBlockArea * 7);
            IMemoryOwner<int>? int32Memory = null;
            IMemoryOwner<short>? int16Memory = null;

            try
            {
                int32Memory = this.configuration.MemoryAllocator.Allocate<int>(requiredBlockArea * 3);
                int16Memory = this.configuration.MemoryAllocator.Allocate<short>(requiredBlockArea * 3);
            }
            catch
            {
                // These three allocations form one workspace. Retain the previous workspace if any replacement fails.
                int16Memory?.Dispose();
                int32Memory?.Dispose();
                floatMemory.Dispose();
                throw;
            }

            this.floatMemory?.Dispose();
            this.int32Memory?.Dispose();
            this.int16Memory?.Dispose();
            this.floatMemory = floatMemory;
            this.int32Memory = int32Memory;
            this.int16Memory = int16Memory;
            this.maxBlockArea = requiredBlockArea;

            // The first three blocks hold X, Y and B coefficients. Four more blocks are transform scratch.
            // Views are captured only when storage changes; group loops borrow their spans once.
            this.DecodeGroupBlockScratchSpace = floatMemory.Memory[..(requiredBlockArea * 7)];
            this.ScratchSpaceMemory = this.DecodeGroupBlockScratchSpace[(requiredBlockArea * 3)..];
            this.DecodeGroupQBlockScratchSpace = int32Memory.Memory[..(requiredBlockArea * 3)];
            this.DecodeGroupQBlock16ScratchSpace = int16Memory.Memory[..(requiredBlockArea * 3)];
        }
    }

    /// <summary>
    /// Releases channel destinations, coefficient scratch, nonzero-count history, and DC storage owned by this cache.
    /// </summary>
    public void Dispose()
    {
        this.floatMemory?.Dispose();
        this.int32Memory?.Dispose();
        this.int16Memory?.Dispose();
        this.floatMemory = null;
        this.int32Memory = null;
        this.int16Memory = null;
        this.maxBlockArea = 0;
        this.DecodeGroupBlockScratchSpace = default;
        this.DecodeGroupQBlockScratchSpace = default;
        this.DecodeGroupQBlock16ScratchSpace = default;
        this.ScratchSpaceMemory = default;

        this.NumberOfNonZeroes.Dispose();
        this.NonZeroChannelStride = 0;

        // A channel can be absent only while constructor failure is releasing earlier allocations.
        foreach (JxlPlane<float> channel in this.channels)
        {
            channel?.Dispose();
        }

        this.DcBuffer.Dispose();
    }
}
