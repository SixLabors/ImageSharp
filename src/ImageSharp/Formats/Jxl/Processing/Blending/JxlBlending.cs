// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

internal static class JxlBlending
{
    public static bool NeedsBlending(JxlFrameHeader header)
    {
        if (header.FrameType is not JxlFrameType.RegularFrame and not JxlFrameType.SkipProgressive)
        {
            return false;
        }

        JxlBlendingInfo? blendingInfo = header.BlendingInfo;
        if (blendingInfo is null)
        {
            return false;
        }

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

    public static void PerformBlending(
        Configuration configuration,
        Buffer2D<float> bg,
        Buffer2D<float> fg,
        Buffer2D<float> output,
        int x0,
        int xsize,
        JxlPatchBlending colorBlending,
        Span<JxlPatchBlending> ecBlending,
        List<JxlExtraChannelInfo> extraChannelInfo)
    {
        bool hasAlpha = extraChannelInfo.Any(x => x.Type == JxlExtraChannel.Alpha);

        int numEc = extraChannelInfo.Count;
        using JxlImageF tmp = new(configuration, xsize, 3 + numEc);

        for (int i = 0; i < numEc; i++)
        {
            int i3 = 3 + i;

            switch (ecBlending[i].Mode)
            {
                case JxlPatchBlendMode.Add:
                {
                    Span<float> row = tmp.GetRow(i3);
                    for (int x = 0; x < xsize; x++)
                    {
                        row[x] = bg[i3, x + x0] + fg[i3, x + x0];
                    }

                    continue;
                }

                case JxlPatchBlendMode.BlendAbove:
                {
                    int alpha = ecBlending[i].AlphaChannel;
                    bool isPremultiplied = extraChannelInfo[alpha].AlphaAssociated;

                    Span<float> bgSpan3 = bg.DangerousGetRowSpan(i3)[x0..];
                    Span<float> bgSpan3Alpha = bg.DangerousGetRowSpan(3 + alpha)[x0..];
                    Span<float> fgSpan3 = fg.DangerousGetRowSpan(i3)[x0..];
                    Span<float> fgSpan3Alpha = fg.DangerousGetRowSpan(3 + alpha)[x0..];

                    JxlAlphaHelper.PerformAlphaBlending(
                        bgSpan3,
                        bgSpan3Alpha,
                        fgSpan3,
                        fgSpan3Alpha,
                        tmp.GetRow(i3),
                        xsize,
                        isPremultiplied,
                        ecBlending[i].Clamp);

                    continue;
                }

                case JxlPatchBlendMode.BlendBelow:
                {
                    int alpha = ecBlending[i].AlphaChannel;
                    bool isPremultiplied = extraChannelInfo[alpha].AlphaAssociated;

                    Span<float> bgSpan3 = bg.DangerousGetRowSpan(i3)[x0..];
                    Span<float> bgSpan3Alpha = bg.DangerousGetRowSpan(3 + alpha)[x0..];
                    Span<float> fgSpan3 = fg.DangerousGetRowSpan(i3)[x0..];
                    Span<float> fgSpan3Alpha = fg.DangerousGetRowSpan(3 + alpha)[x0..];

                    JxlAlphaHelper.PerformAlphaBlending(
                        bgSpan3,
                        bgSpan3Alpha,
                        fgSpan3,
                        fgSpan3Alpha,
                        tmp.GetRow(3 + i),
                        xsize,
                        isPremultiplied,
                        ecBlending[i].Clamp);

                    continue;
                }

                case JxlPatchBlendMode.AlphaWeightedAddAbove:
                {
                    int alpha = ecBlending[i].AlphaChannel;

                    Span<float> bgSpan3 = bg.DangerousGetRowSpan(i3)[x0..];
                    Span<float> bgSpan3Alpha = bg.DangerousGetRowSpan(3 + alpha)[x0..];
                    Span<float> fgSpan3 = fg.DangerousGetRowSpan(i3)[x0..];

                    JxlAlphaHelper.PerformAlphaWeightedAdd(
                        bgSpan3,
                        fgSpan3,
                        bgSpan3Alpha,
                        tmp.GetRow(3 + i),
                        xsize,
                        ecBlending[i].Clamp);

                    continue;
                }

                case JxlPatchBlendMode.AlphaWeightedAddBelow:
                {
                    int alpha = ecBlending[i].AlphaChannel;

                    Span<float> bgSpan3 = bg.DangerousGetRowSpan(i3)[x0..];
                    Span<float> bgSpan3Alpha = bg.DangerousGetRowSpan(3 + alpha)[x0..];
                    Span<float> fgSpan3 = fg.DangerousGetRowSpan(i3)[x0..];

                    JxlAlphaHelper.PerformAlphaWeightedAdd(
                        fgSpan3,
                        bgSpan3,
                        bgSpan3Alpha,
                        tmp.GetRow(3 + i),
                        xsize,
                        ecBlending[i].Clamp);

                    continue;
                }

                case JxlPatchBlendMode.Multiply:
                {
                    Span<float> bgSpan3 = bg.DangerousGetRowSpan(i3)[x0..];
                    Span<float> fgSpan3 = fg.DangerousGetRowSpan(i3)[x0..];

                    JxlAlphaHelper.PerformMultiplyBlending(
                        bgSpan3,
                        fgSpan3,
                        tmp.GetRow(i3),
                        xsize,
                        ecBlending[i].Clamp);

                    continue;
                }

                case JxlPatchBlendMode.Replace:
                    if (xsize > 0)
                    {
                        Span<float> fgSpan3 = fg.DangerousGetRowSpan(i3)[x0..];
                        fgSpan3.Slice(0, xsize).CopyTo(tmp.GetRow(i3));
                    }

                    continue;

                case JxlPatchBlendMode.None:
                    if (xsize > 0)
                    {
                        Span<float> bgSpan3 = bg.DangerousGetRowSpan(i3)[x0..];
                        bgSpan3.Slice(0, xsize).CopyTo(tmp.GetRow(i3));
                    }

                    continue;
            }
        }

        int colorBlendingAlpha = colorBlending.AlphaChannel;

        void Add()
        {
            for (int p = 0; p < 3; p++)
            {
                Span<float> output = tmp.GetRow(p);
                Span<float> bgSpan = bg.DangerousGetRowSpan(p);
                Span<float> fgSpan = fg.DangerousGetRowSpan(p);

                for (int x = 0; x < xsize; x++)
                {
                    int xPlusX0 = x + x0;

                    output[x] = bgSpan[xPlusX0] + fgSpan[xPlusX0];
                }
            }
        }

        void BlendWeighted(Span<float> bottom, Span<float> top)
        {
            bool isPremultiplied = extraChannelInfo[colorBlendingAlpha].AlphaAssociated;

            JxlAlphaHelper.PerformAlphaBlending(
                new JxlAlphaBlendingInputLayer()
                {
                    R = bottom[x0..],
                    G = bottom[(x0 + 1)..],
                    B = bottom[(2 + x0)..],
                    A = bottom[(3 + colorBlendingAlpha + x0)..]
                },
                new JxlAlphaBlendingInputLayer()
                {
                    R = top[x0..],
                    G = top[(x0 + 1)..],
                    B = top[(x0 + 2)..],
                    A = top[(3 + colorBlendingAlpha + x0)..]
                },
                new JxlAlphaBlendingOutput()
                {
                    R = tmp.GetRow(0),
                    G = tmp.GetRow(1),
                    B = tmp.GetRow(2),
                    A = tmp.GetRow(3)
                },
                xsize,
                isPremultiplied,
                colorBlending.Clamp);
        }

        void AddWeighted(Span<float> bottom, Span<float> top)
        {
            for (int c = 0; c < 3; c++)
            {
                JxlAlphaHelper.PerformAlphaWeightedAdd(bottom[(c + x0)..], top[(c + x0)..], top[(3 + colorBlendingAlpha + x0)..], tmp.GetRow(c), xsize, colorBlending.Clamp);
            }
        }

        void Copy(Span<float> src)
        {
            for (int p = 0; p < 3; p++)
            {
                src.Slice(p + x0, xsize).CopyTo(tmp.GetRow(p));
            }
        }

        switch (colorBlending.Mode)
        {
            case JxlPatchBlendMode.Add:
            {
                Add();
                break;
            }

            case JxlPatchBlendMode.AlphaWeightedAddAbove:
            {
                if (hasAlpha)
                {
                    AddWeighted(bg.DangerousGetSingleSpan(), fg.DangerousGetSingleSpan());
                }
                else
                {
                    Add();
                }

                break;
            }

            case JxlPatchBlendMode.AlphaWeightedAddBelow:
            {
                if (hasAlpha)
                {
                    AddWeighted(fg.DangerousGetSingleSpan(), bg.DangerousGetSingleSpan());
                }
                else
                {
                    Add();
                }

                break;
            }

            case JxlPatchBlendMode.BlendAbove:
            {
                if (hasAlpha)
                {
                    BlendWeighted(bg.DangerousGetSingleSpan(), fg.DangerousGetSingleSpan());
                }
                else
                {
                    Copy(fg.DangerousGetSingleSpan());
                }

                break;
            }

            case JxlPatchBlendMode.BlendBelow:
            {
                if (hasAlpha)
                {
                    BlendWeighted(fg.DangerousGetSingleSpan(), bg.DangerousGetSingleSpan());
                }
                else
                {
                    Copy(fg.DangerousGetSingleSpan());
                }

                break;
            }

            case JxlPatchBlendMode.Multiply:
            {
                Span<float> bgSpan = bg.DangerousGetSingleSpan();
                Span<float> fgSpan = fg.DangerousGetSingleSpan();

                for (int p = 0; p < 3; p++)
                {
                    JxlAlphaHelper.PerformMultiplyBlending(
                        bgSpan[(p + x0)..],
                        fgSpan[(p + x0)..],
                        tmp.GetRow(p),
                        xsize,
                        colorBlending.Clamp);
                }

                break;
            }

            case JxlPatchBlendMode.Replace:
            {
                Copy(fg.DangerousGetSingleSpan());
                break;
            }

            case JxlPatchBlendMode.None:
            {
                Copy(bg.DangerousGetSingleSpan());
                break;
            }
        }

        if (xsize != 0)
        {
            Span<float> outputSpan = output.DangerousGetSingleSpan();

            for (int i = 0; i < 3; i++)
            {
                tmp.GetRow(i).Slice(0, xsize).CopyTo(outputSpan[(i + x0)..]);
            }
        }
    }
}
