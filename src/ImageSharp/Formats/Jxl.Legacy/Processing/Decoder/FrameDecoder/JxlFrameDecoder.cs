// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.FrameDecoder;

/// <summary>
/// Owns one frame's section control and modular sample storage while borrowing the codestream's decoder workspace.
/// </summary>
internal sealed class JxlFrameDecoder : IDisposable
{
    private JxlPassesDecoderState decoderState;
    private List<JxlTocEntry> toc = [];
    private long sectionSizesSum;
    private JxlFrameHeader frameHeader;
    private JxlFrameDimensions frameDimensions;
    private JxlImageBundle decoded;
    private JxlModularFrameDecoder modularFrameDecoder;
    private bool renderSpotcolors = true;
    private bool coalescing = true;
    private List<byte> processedSection = [];
    private List<byte> decodedPassesPerAcGroup = [];
    private List<byte> decodedDcGroups = [];
    private bool decodedDcGlobal;
    private bool decodedAcGlobal;
    private bool finalizedDc = true;
    private long numSectionsDone;
    private bool isFinalized = true;
    private bool allocated;
    private JxlProgressiveDetail progressiveDetail = JxlProgressiveDetail.Frames;
    private List<int> passesToPause = [];

    /// <summary>
    /// Binds a parsed frame to its decoder workspace and reads its section table.
    /// </summary>
    /// <param name="decoderState">The borrowed workspace whose shared state outlives this controller.</param>
    /// <param name="frameHeader">The header already read from this frame's bitstream.</param>
    /// <param name="frameDimensions">The validated dimensions derived from that header.</param>
    /// <param name="decoded">The caller-owned destination bundle.</param>
    /// <param name="reader">The reader immediately after the frame header. It advances to the first section.</param>
    public JxlFrameDecoder(
        JxlPassesDecoderState decoderState,
        JxlFrameHeader frameHeader,
        JxlFrameDimensions frameDimensions,
        JxlImageBundle decoded,
        JxlBitReader reader)
    {
        this.decoderState = decoderState;
        this.frameHeader = frameHeader;
        this.frameDimensions = frameDimensions;
        this.decoded = decoded;
        this.modularFrameDecoder = new(decoderState.Shared.Configuration, frameDimensions);

        int entries = JxlToc.NumberOfTocEntries(frameDimensions.NumGroups, frameDimensions.NumDcGroups, (int)frameHeader.Passes.NumberOfPasses);
        List<uint> sizes = new(entries);
        List<uint> permutation = [];
        if (!JxlToc.ReadToc(decoderState.Shared.Configuration, entries, reader, sizes, permutation))
        {
            throw new InvalidImageContentException("The JPEG XL frame section table is invalid or incomplete.");
        }

        this.toc.EnsureCapacity(entries);
        for (int i = 0; i < entries; i++)
        {
            // TOC sizes follow physical stream order. The permutation maps each logical section to its physical slot.
            // The U32 distribution limits one size to less than Int32.MaxValue; at most 65,536 entries fit in Int64.
            this.toc.Add(new JxlTocEntry { Size = (int)sizes[i], Id = i });
            this.sectionSizesSum += sizes[i];
        }

        if (permutation.Count != 0)
        {
            for (int i = 0; i < entries; i++)
            {
                int physicalIndex = (int)permutation[i];
                JxlTocEntry entry = this.toc[physicalIndex];
                entry.Id = i;
                this.toc[physicalIndex] = entry;
            }
        }

        // A reused output bundle can still contain a previous frame with another extent. Only it owns those planes.
        decoded.RemoveColor();
        decoded.ClearExtraChannels();
        decoded.Duration = frameHeader.AnimationFrame.Duration;

        if (!frameHeader.IsPreviewFrame && (frameHeader.IsLast || decoded.Duration > 0)
            && frameHeader.FrameType is JxlFrameType.RegularFrame or JxlFrameType.SkipProgressive)
        {
            decoderState.VisibleFrameIndex++;
            decoderState.NonvisibleFrameIndex = 0;
        }
        else
        {
            decoderState.NonvisibleFrameIndex++;
        }
    }

    /// <summary>
    /// Gets section sizes and logical identifiers in their physical stream order.
    /// </summary>
    public IReadOnlyList<JxlTocEntry> Toc => this.toc;

    /// <summary>
    /// Gets the total byte count of the frame's sections, excluding its header and section table.
    /// </summary>
    public long SectionSizesSum => this.sectionSizesSum;

    /// <summary>
    /// Gets a value indicating whether there are any DC groups left to decode.
    /// </summary>
    private bool ContainsDcGroupToDecode => this.decodedDcGroups.Any(x => x == 0);

    public int References
    {
        get
        {
            if (!this.isFinalized || !this.HasEverything)
            {
                return 0;
            }

            int result = 0;

            if (this.frameHeader.FrameType is JxlFrameType.RegularFrame or JxlFrameType.SkipProgressive)
            {
                bool cropped = this.frameHeader.CustomSizeOrOrigin;
                if (cropped || this.frameHeader.BlendingInfo!.BlendMode != JxlBlendMode.Replace)
                {
                    result |= 1 << (int)this.frameHeader.BlendingInfo!.Source;
                }

                List<JxlBlendingInfo> extra = this.frameHeader.ExtraChannelBlendingInfo;
                foreach (JxlBlendingInfo extraChannelBlendingInfo in extra)
                {
                    if (cropped || extraChannelBlendingInfo.BlendMode != JxlBlendMode.Replace)
                    {
                        result |= 1 << (int)extraChannelBlendingInfo.Source;
                    }
                }
            }

            // Patches
            if ((this.frameHeader.Flags & (ulong)JxlFrameHeaderFlags.Patches) != 0)
            {
                result |= this.decoderState.Shared.ImageFeatures.PatchDictionary.GetReferences();
            }

            // DC Level
            if ((this.frameHeader.Flags & (ulong)JxlFrameHeaderFlags.Dc) != 0)
            {
                // Reads from the next DC level
                int dcLevel = (int)this.frameHeader.DcLevel + 1;

                // Bits 16, 32, 64, 128 for DC level
                result |= 16 << (dcLevel - 1);
            }

            return result;
        }
    }

    public bool HasEverything
    {
        get
        {
            if (!this.decodedDcGlobal)
            {
                return false;
            }

            if (!this.decodedAcGlobal)
            {
                return false;
            }

            if (this.HasDcGroupToDecode)
            {
                return false;
            }

            if (this.decodedPassesPerAcGroup.Any(nbp => nbp < this.frameHeader.Passes.NumberOfPasses))
            {
                return false;
            }

            return true;
        }
    }

    public static int SavedAs(JxlFrameHeader header)
    {
        if (header.FrameType == JxlFrameType.DcFrame)
        {
            // Bits 16, 32, 64, 128 for DC level
            return 16 << ((int)header.DcLevel - 1);
        }
        else if (header.CanBeReferenced)
        {
            // Bits 1, 2, 4 and 8 for the references
            return 1 << (int)header.SaveAsReference;
        }

        return 0;
    }

    public static void DecodeGlobalDcInfo(Configuration configuration, JxlBitReader reader, bool isJpeg, JxlPassesDecoderState state)
    {
        state.Shared.Quantizer.Decode(reader);

        if (!JxlEntropyCoder.DecodeBlockContextMap(configuration, reader, ref state.Shared.BlockContextMap))
        {
            throw new InvalidOperationException("Could not decode block context map");
        }

        if (!state.Shared.ColorMap.DecodeDc(reader))
        {
            throw new InvalidOperationException("Could not decode DC color correlation map");
        }

        if (isJpeg)
        {
            state.Shared.Quantizer.ClearDcMultipliers();
        }

        state.Shared.AcStrategy.FillInvalid();
    }

    /// <summary>
    /// Releases frame-owned modular channels without disposing borrowed decoder controls or the output bundle.
    /// </summary>
    public void Dispose()
    {
        this.modularFrameDecoder.Dispose();
    }
}
