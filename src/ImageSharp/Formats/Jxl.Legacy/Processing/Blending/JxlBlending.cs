// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

internal static partial class JxlBlending
{
    /// <summary>
    /// Determines whether the frame requires composition with the existing image canvas.
    /// </summary>
    /// <param name="header">The decoded frame header and per-channel composition settings.</param>
    /// <returns>Whether the frame type, bounds, or blend modes require canvas composition.</returns>
    public static bool NeedsBlending(JxlFrameHeader header)
    {
        if (header.FrameType is not JxlFrameType.RegularFrame and not JxlFrameType.SkipProgressive)
        {
            return false;
        }

        JxlBlendingInfo blendingInfo = header.BlendingInfo;
        bool replaceAll = blendingInfo.BlendMode == JxlBlendMode.Replace;

        foreach (JxlBlendingInfo info in header.ExtraChannelBlendingInfo)
        {
            if (info.BlendMode != JxlBlendMode.Replace)
            {
                replaceAll = false;
            }
        }

        if (!header.CustomSizeOrOrigin && replaceAll)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Blends a row segment and commits all color and extra-channel results after composition.
    /// </summary>
    /// <param name="background">The background rows, with one memory entry per channel.</param>
    /// <param name="foreground">The foreground rows, each starting at the first source sample to blend.</param>
    /// <param name="output">The destination rows, which can share storage with either input.</param>
    /// <param name="startX">The first background and destination column.</param>
    /// <param name="pixelCount">The number of samples per channel.</param>
    /// <param name="colorBlending">The settings for the three color channels.</param>
    /// <param name="extraChannelBlending">The settings for each extra channel.</param>
    /// <param name="extraChannelInfo">The extra-channel types and alpha representations.</param>
    /// <param name="scratch">The caller-owned plane, with one row per channel and at least pixelCount columns.</param>
    public static void PerformBlending(
        ReadOnlySpan<Memory<float>> background,
        ReadOnlySpan<Memory<float>> foreground,
        Span<Memory<float>> output,
        int startX,
        int pixelCount,
        JxlPatchBlending colorBlending,
        ReadOnlySpan<JxlPatchBlending> extraChannelBlending,
        List<JxlExtraChannelInfo> extraChannelInfo,
        JxlPlane<float> scratch)
    {
        bool hasAlpha = false;

        foreach (JxlExtraChannelInfo channel in extraChannelInfo)
        {
            if (channel.Type == JxlExtraChannel.Alpha)
            {
                hasAlpha = true;
                break;
            }
        }

        // Read original input alpha throughout composition. Destination rows can be the background rows,
        // and different extra channels can use different alpha rows. Publish only after every channel is complete.
        for (int channel = 0; channel < extraChannelInfo.Count; channel++)
        {
            BlendChannel(background, foreground, startX, pixelCount, 3 + channel, extraChannelBlending[channel], extraChannelInfo, true, scratch);
        }

        bool colorSourceOver = colorBlending.Mode is JxlPatchBlendMode.BlendAbove or JxlPatchBlendMode.BlendBelow;

        if (hasAlpha && colorSourceOver)
        {
            int weightChannel = 3 + colorBlending.AlphaChannel;
            JxlBlendInput bottom = new(
                background[0].Span.Slice(startX, pixelCount),
                background[1].Span.Slice(startX, pixelCount),
                background[2].Span.Slice(startX, pixelCount),
                background[weightChannel].Span.Slice(startX, pixelCount));

            JxlBlendInput top = new(
                foreground[0].Span[..pixelCount],
                foreground[1].Span[..pixelCount],
                foreground[2].Span[..pixelCount],
                foreground[weightChannel].Span[..pixelCount]);

            JxlBlendOutput result = new(
                scratch.GetRow(0)[..pixelCount],
                scratch.GetRow(1)[..pixelCount],
                scratch.GetRow(2)[..pixelCount],
                scratch.GetRow(weightChannel)[..pixelCount]);

            // Color composition also defines its selected output weight, overriding that extra channel's separate mode.
            bool associated = extraChannelInfo[colorBlending.AlphaChannel].AlphaAssociated;

            if (colorBlending.Mode == JxlPatchBlendMode.BlendBelow)
            {
                SourceOver(top, bottom, result, associated, colorBlending.Clamp);
            }
            else
            {
                SourceOver(bottom, top, result, associated, colorBlending.Clamp);
            }
        }
        else
        {
            for (int channel = 0; channel < 3; channel++)
            {
                BlendChannel(background, foreground, startX, pixelCount, channel, colorBlending, extraChannelInfo, hasAlpha, scratch);
            }
        }

        for (int channel = 0; channel < 3 + extraChannelInfo.Count; channel++)
        {
            scratch.GetRow(channel)[..pixelCount].CopyTo(output[channel].Span.Slice(startX, pixelCount));
        }
    }

    /// <summary>
    /// Selects the source rows and applies one channel's blend mode through the shared bulk APIs.
    /// </summary>
    /// <param name="background">The unchanged background rows.</param>
    /// <param name="foreground">The unchanged foreground rows, starting at the blend segment.</param>
    /// <param name="startX">The starting column within the background rows.</param>
    /// <param name="pixelCount">The number of samples to blend.</param>
    /// <param name="channelIndex">The color or extra-channel index.</param>
    /// <param name="blending">The channel's blend mode and alpha selection.</param>
    /// <param name="extraChannelInfo">The alpha representations selected by the metadata.</param>
    /// <param name="hasAlpha">Whether the mode can use an alpha row.</param>
    /// <param name="scratch">The destination scratch rows.</param>
    private static void BlendChannel(
        ReadOnlySpan<Memory<float>> background,
        ReadOnlySpan<Memory<float>> foreground,
        int startX,
        int pixelCount,
        int channelIndex,
        JxlPatchBlending blending,
        List<JxlExtraChannelInfo> extraChannelInfo,
        bool hasAlpha,
        JxlPlane<float> scratch)
    {
        ReadOnlySpan<float> backgroundValues = background[channelIndex].Span.Slice(startX, pixelCount);
        ReadOnlySpan<float> foregroundValues = foreground[channelIndex].Span[..pixelCount];
        Span<float> destination = scratch.GetRow(channelIndex)[..pixelCount];

        switch (blending.Mode)
        {
            case JxlPatchBlendMode.None:
                backgroundValues.CopyTo(destination);
                break;
            case JxlPatchBlendMode.Replace:
                foregroundValues.CopyTo(destination);
                break;
            case JxlPatchBlendMode.Add:
                TensorPrimitives.Add(backgroundValues, foregroundValues, destination);
                break;
            case JxlPatchBlendMode.Multiply:
                Multiply(backgroundValues, foregroundValues, destination, blending.Clamp);
                break;
            case JxlPatchBlendMode.BlendAbove:
            case JxlPatchBlendMode.BlendBelow:
            case JxlPatchBlendMode.AlphaWeightedAddAbove:
            case JxlPatchBlendMode.AlphaWeightedAddBelow:
            {
                bool sourceOver = blending.Mode is JxlPatchBlendMode.BlendAbove or JxlPatchBlendMode.BlendBelow;

                if (!hasAlpha)
                {
                    // Without alpha, native JPEG XL treats source-over as replacement and weighted addition as ordinary addition.
                    if (sourceOver)
                    {
                        foregroundValues.CopyTo(destination);
                    }
                    else
                    {
                        TensorPrimitives.Add(backgroundValues, foregroundValues, destination);
                    }

                    break;
                }

                int alphaIndex = 3 + blending.AlphaChannel;
                ReadOnlySpan<float> backgroundAlpha = background[alphaIndex].Span.Slice(startX, pixelCount);
                ReadOnlySpan<float> foregroundAlpha = foreground[alphaIndex].Span[..pixelCount];

                // Below reverses both samples and their alpha rows. Swapping only color would apply the wrong coverage.
                if (blending.Mode is JxlPatchBlendMode.BlendBelow or JxlPatchBlendMode.AlphaWeightedAddBelow)
                {
                    ReadOnlySpan<float> values = backgroundValues;
                    backgroundValues = foregroundValues;
                    foregroundValues = values;
                    ReadOnlySpan<float> alpha = backgroundAlpha;
                    backgroundAlpha = foregroundAlpha;
                    foregroundAlpha = alpha;
                }

                if (sourceOver)
                {
                    bool associatedAlpha = extraChannelInfo[blending.AlphaChannel].AlphaAssociated;
                    SourceOver(
                        backgroundValues,
                        backgroundAlpha,
                        foregroundValues,
                        foregroundAlpha,
                        destination,
                        associatedAlpha,
                        blending.Clamp);
                }
                else
                {
                    AddWeighted(backgroundValues, foregroundValues, foregroundAlpha, destination, blending.Clamp);
                }

                break;
            }
        }
    }
}
