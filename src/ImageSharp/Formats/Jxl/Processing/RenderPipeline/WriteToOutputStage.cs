// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal sealed class WriteToOutputStage : RenderPipelineStageBase, IDisposable
{
    private const int ChunkSize = 1024;

    private readonly int width;
    private readonly int height;
    private readonly IJxlImageOutput main;
    private readonly int numColors;
    private readonly bool wantAlpha;
    private readonly bool hasAlpha;
    private readonly bool unpremultiplyAlpha;
    private readonly int alphaC;
    private readonly bool flipX;
    private readonly bool flipY;
    private readonly bool transpose;
    private readonly List<IJxlImageOutput> extraChannels = [];
    private readonly List<float> opaqueAlpha = [];
    private readonly List<Memory<byte>> tempIn = [];
    private readonly List<Memory<byte>> tempOut = [];

    // For Dispose()
    private readonly List<IMemoryOwner<byte>> disposables = [];

    public WriteToOutputStage(Configuration configuration, IJxlImageOutput output, int width, int height, bool hasAlpha, bool unpremultiplyAlpha, int alphaC, JxlExifOrientation undoOrientation, List<IJxlImageOutput> extraChannel, Func<IJxlImageOutput, IJxlImageOutput> extraFactory)
        : base(configuration)
    {
        this.width = width;
        this.height = height;
        this.main = output;
        this.numColors = this.main.PixelFormat.Channels < 3 ? 1 : 3;
        this.wantAlpha = this.main.PixelFormat.Channels is 2 or 4;
        this.hasAlpha = hasAlpha;
        this.unpremultiplyAlpha = unpremultiplyAlpha;
        this.alphaC = alphaC;
        this.flipX = ShouldFlipX(undoOrientation);
        this.flipY = ShouldFlipY(undoOrientation);
        this.transpose = ShouldTranspose(undoOrientation);
        this.opaqueAlpha = new(ChunkSize);
        CollectionsMarshal.AsSpan(this.opaqueAlpha).Fill(1.0f);

        for (int ec = 0; ec < this.extraChannels.Count; ec++)
        {
            if (extraChannel[ec].IsPresent)
            {
                IJxlImageOutput extra = extraFactory(extraChannel[ec]);
                extra.ChannelIndex = 3 + ec;
                this.extraChannels.Add(extra);
            }
        }
    }

    public override string Name => "WritePixelCB";

    /// <summary>
    /// Gets the 32x32 blue noise dithering pattern lookup
    /// table (from <see href="https://github.com/MomentsInGraphics/BlueNoise"/>),
    /// scaled to have an average of 0 and be fully contained in (0.49219
    /// to -0.49219). Rows are padded to 48 (32 + 16) to allow SIMD to wrap around
    /// horizontally.
    /// </summary>
    private static ReadOnlySpan<float> DitheringPattern =>
    [
        -0.26057f, 0.32619f, 0.21039f, -0.03281f, -0.10616f, 0.16792f, 0.43042f, -0.48061f,
        -0.00965f, -0.31075f, 0.24899f, -0.35322f, -0.02509f, -0.25285f, 0.02895f, 0.10230f,
        -0.28373f, -0.00193f, 0.23355f, 0.43428f, -0.23741f, 0.18336f, -0.31847f, -0.11002f,
        -0.36094f, 0.26057f, -0.19108f, -0.29531f, 0.40726f, -0.09458f, 0.11002f, -0.48833f,
        -0.26057f, 0.32619f, 0.21039f, -0.03281f, -0.10616f, 0.16792f, 0.43042f, -0.48061f,
        -0.00965f, -0.31075f, 0.24899f, -0.35322f, -0.02509f, -0.25285f, 0.02895f, 0.10230f,
        0.16020f, -0.35708f, -0.18336f, 0.36094f, -0.28373f, -0.34550f, -0.20267f, 0.07914f,
        0.35708f, -0.41498f, 0.47675f, -0.21811f, -0.12546f, 0.44200f, -0.41884f, -0.17178f,
        0.39954f, 0.33778f, -0.33778f, 0.04053f, -0.46517f, 0.27215f, -0.16792f, 0.39182f,
        0.20653f, -0.43814f, -0.02895f, 0.17950f, -0.41498f, 0.01737f, 0.24899f, 0.49219f,
        0.16020f, -0.35708f, -0.18336f, 0.36094f, -0.28373f, -0.34550f, -0.20267f, 0.07914f,
        0.35708f, -0.41498f, 0.47675f, -0.21811f, -0.12546f, 0.44200f, -0.41884f, -0.17178f,
        -0.00965f, 0.08300f, 0.41112f, -0.46903f, 0.04053f, 0.47289f, 0.26057f, -0.05983f,
        -0.13704f, 0.14862f, 0.03281f, 0.29531f, -0.45744f, 0.22583f, 0.14862f, -0.09072f,
        -0.37638f, 0.19881f, -0.14476f, 0.14476f, -0.09072f, 0.48447f, -0.39954f, 0.06369f,
        -0.05983f, -0.26829f, 0.43428f, -0.12546f, 0.28759f, -0.22969f, -0.32619f, -0.15248f,
        -0.00965f, 0.08300f, 0.41112f, -0.46903f, 0.04053f, 0.47289f, 0.26057f, -0.05983f,
        -0.13704f, 0.14862f, 0.03281f, 0.29531f, -0.45744f, 0.22583f, 0.14862f, -0.09072f,
        -0.42270f, 0.23741f, -0.23355f, -0.11774f, 0.18722f, 0.11388f, -0.43814f, -0.24899f,
        0.41884f, 0.21039f, -0.28373f, -0.06756f, 0.07914f, 0.36480f, -0.31075f, 0.30303f,
        -0.03281f, 0.07142f, -0.42656f, 0.38024f, -0.27987f, 0.00579f, 0.12546f, -0.22197f,
        0.29917f, 0.36866f, 0.13704f, -0.47289f, 0.09072f, 0.35708f, -0.04825f, 0.38796f,
        -0.42270f, 0.23741f, -0.23355f, -0.11774f, 0.18722f, 0.11388f, -0.43814f, -0.24899f,
        0.41884f, 0.21039f, -0.28373f, -0.06756f, 0.07914f, 0.36480f, -0.31075f, 0.30303f,
        -0.28759f, -0.07142f, 0.44200f, 0.27601f, -0.38024f, -0.16020f, -0.01737f, 0.30303f,
        -0.33006f, -0.40340f, -0.16792f, 0.40726f, -0.36480f, -0.00579f, -0.19108f, 0.41498f,
        -0.26443f, 0.46903f, -0.21811f, 0.28759f, -0.04053f, 0.22197f, 0.34550f, -0.44972f,
        -0.14476f, -0.34164f, 0.04053f, -0.19494f, 0.45358f, -0.37252f, 0.21425f, 0.05597f,
        -0.28759f, -0.07142f, 0.44200f, 0.27601f, -0.38024f, -0.16020f, -0.01737f, 0.30303f,
        -0.33006f, -0.40340f, -0.16792f, 0.40726f, -0.36480f, -0.00579f, -0.19108f, 0.41498f,
        0.31075f, 0.14090f, -0.33778f, 0.00579f, 0.34550f, -0.29917f, 0.38796f, 0.13704f,
        0.05983f, -0.10230f, 0.34164f, 0.10616f, -0.23741f, 0.19494f, -0.47675f, 0.04439f,
        -0.39568f, 0.24127f, 0.10616f, -0.49219f, -0.17950f, -0.36094f, -0.30303f, 0.45744f,
        -0.01351f, 0.24513f, -0.39182f, -0.07528f, 0.18722f, -0.26057f, -0.11002f, -0.45358f,
        0.31075f, 0.14090f, -0.33778f, 0.00579f, 0.34550f, -0.29917f, 0.38796f, 0.13704f,
        0.05983f, -0.10230f, 0.34164f, 0.10616f, -0.23741f, 0.19494f, -0.47675f, 0.04439f,
        0.46903f, -0.17178f, -0.41112f, 0.07528f, -0.09458f, 0.21811f, -0.20267f, -0.48833f,
        0.44972f, 0.00965f, 0.24127f, -0.42656f, 0.48447f, -0.11774f, 0.26443f, 0.14090f,
        -0.15634f, -0.07142f, -0.32233f, 0.36094f, 0.42270f, 0.19108f, 0.07142f, -0.11002f,
        0.15634f, 0.38024f, -0.28759f, 0.27987f, -0.00193f, 0.33006f, 0.11388f, -0.21039f,
        0.46903f, -0.17178f, -0.41112f, 0.07528f, -0.09458f, 0.21811f, -0.20267f, -0.48833f,
        0.44972f, 0.00965f, 0.24127f, -0.42656f, 0.48447f, -0.11774f, 0.26443f, 0.14090f,
        0.02123f, 0.17950f, 0.38024f, -0.24127f, -0.44586f, 0.48833f, -0.03667f, 0.26829f,
        -0.36866f, -0.22583f, 0.17178f, -0.30689f, 0.29145f, -0.04825f, -0.35322f, 0.43042f,
        0.34936f, 0.00193f, 0.16792f, -0.12932f, 0.03667f, -0.06756f, 0.31847f, -0.40726f,
        -0.24513f, 0.09458f, -0.17564f, 0.47675f, -0.43042f, -0.32233f, 0.40340f, 0.26057f,
        0.02123f, 0.17950f, 0.38024f, -0.24127f, -0.44586f, 0.48833f, -0.03667f, 0.26829f,
        -0.36866f, -0.22583f, 0.17178f, -0.30689f, 0.29145f, -0.04825f, -0.35322f, 0.43042f,
        -0.47675f, -0.12160f, -0.04825f, 0.28759f, 0.10230f, 0.15634f, -0.14862f, -0.27601f,
        0.36094f, -0.12932f, -0.05983f, -0.45358f, -0.17950f, 0.01737f, 0.09458f, -0.29145f,
        -0.22969f, -0.43428f, 0.45744f, -0.38796f, -0.27601f, -0.21039f, -0.46131f, 0.22969f,
        0.41112f, -0.05211f, -0.48061f, 0.16406f, 0.05211f, -0.14862f, -0.03281f, -0.36866f,
        -0.47675f, -0.12160f, -0.04825f, 0.28759f, 0.10230f, 0.15634f, -0.14862f, -0.27601f,
        0.36094f, -0.12932f, -0.05983f, -0.45358f, -0.17950f, 0.01737f, 0.09458f, -0.29145f,
        -0.27215f, 0.34164f, -0.31075f, 0.42656f, -0.38410f, -0.32619f, 0.02895f, 0.19881f,
        0.08300f, 0.42270f, 0.31461f, 0.13318f, 0.45744f, 0.37638f, -0.40726f, 0.31847f,
        -0.08686f, 0.21425f, 0.29917f, 0.07914f, 0.26829f, 0.13704f, 0.48447f, -0.15248f,
        0.02509f, -0.34936f, 0.34936f, -0.10230f, 0.42656f, -0.23741f, 0.22583f, 0.09072f,
        -0.27215f, 0.34164f, -0.31075f, 0.42656f, -0.38410f, -0.32619f, 0.02895f, 0.19881f,
        0.08300f, 0.42270f, 0.31461f, 0.13318f, 0.45744f, 0.37638f, -0.40726f, 0.31847f,
        0.44972f, 0.20267f, 0.04825f, -0.21425f, 0.24513f, -0.07142f, 0.39954f, -0.46131f,
        -0.39568f, -0.01351f, -0.33392f, 0.05597f, -0.26443f, 0.22197f, -0.20653f, 0.15248f,
        0.04439f, -0.46517f, -0.16406f, -0.04439f, -0.34936f, 0.37252f, -0.01351f, -0.30689f,
        0.29917f, 0.20653f, -0.26829f, 0.26443f, 0.13318f, -0.39954f, 0.30303f, -0.08686f,
        0.44972f, 0.20267f, 0.04825f, -0.21425f, 0.24513f, -0.07142f, 0.39954f, -0.46131f,
        -0.39568f, -0.01351f, -0.33392f, 0.05597f, -0.26443f, 0.22197f, -0.20653f, 0.15248f,
        -0.42656f, 0.12932f, -0.14476f, -0.46903f, -0.00579f, 0.34936f, -0.18722f, 0.28373f,
        -0.23741f, 0.22969f, -0.16020f, -0.38024f, -0.08300f, -0.48447f, -0.02123f, -0.14862f,
        0.48061f, -0.31847f, 0.39568f, -0.24899f, 0.18722f, -0.41884f, 0.10230f, -0.08300f,
        -0.38796f, 0.06369f, -0.19881f, -0.44972f, 0.00579f, -0.33392f, 0.37252f, -0.19108f,
        -0.42656f, 0.12932f, -0.14476f, -0.46903f, -0.00579f, 0.34936f, -0.18722f, 0.28373f,
        -0.23741f, 0.22969f, -0.16020f, -0.38024f, -0.08300f, -0.48447f, -0.02123f, -0.14862f,
        -0.02509f, -0.35708f, 0.32619f, 0.46517f, 0.17178f, -0.28373f, 0.10616f, 0.47675f,
        -0.09458f, 0.15248f, 0.43428f, 0.35322f, 0.17564f, 0.27215f, 0.41112f, -0.36480f,
        0.24899f, 0.11774f, 0.01351f, 0.33006f, -0.11388f, -0.18336f, 0.41884f, -0.23355f,
        0.16406f, 0.46131f, 0.38410f, -0.04825f, -0.15634f, 0.49219f, 0.17564f, 0.03667f,
        -0.02509f, -0.35708f, 0.32619f, 0.46517f, 0.17178f, -0.28373f, 0.10616f, 0.47675f,
        -0.09458f, 0.15248f, 0.43428f, 0.35322f, 0.17564f, 0.27215f, 0.41112f, -0.36480f,
        0.40726f, 0.23355f, -0.25285f, -0.08300f, -0.41112f, -0.12160f, -0.35708f, 0.05211f,
        -0.41884f, -0.29531f, 0.02123f, -0.21425f, 0.09844f, -0.30689f, -0.11388f, 0.34550f,
        -0.26443f, -0.07142f, -0.39954f, 0.44586f, 0.05983f, -0.48833f, 0.24127f, 0.34936f,
        -0.44200f, -0.12546f, 0.12160f, -0.30303f, 0.27215f, 0.07528f, -0.48447f, -0.29145f,
        0.40726f, 0.23355f, -0.25285f, -0.08300f, -0.41112f, -0.12160f, -0.35708f, 0.05211f,
        -0.41884f, -0.29531f, 0.02123f, -0.21425f, 0.09844f, -0.30689f, -0.11388f, 0.34550f,
        0.28373f, -0.17564f, 0.09458f, 0.02123f, 0.30689f, 0.41884f, 0.20653f, -0.03667f,
        0.32233f, 0.25671f, -0.45744f, -0.05597f, 0.46517f, -0.41498f, 0.00965f, 0.07142f,
        -0.44586f, 0.16406f, -0.20653f, 0.21811f, -0.29917f, 0.28759f, -0.05597f, 0.03281f,
        -0.32619f, -0.00965f, 0.31847f, -0.37252f, 0.18722f, -0.11002f, -0.22969f, -0.06369f,
        0.28373f, -0.17564f, 0.09458f, 0.02123f, 0.30689f, 0.41884f, 0.20653f, -0.03667f,
        0.32233f, 0.25671f, -0.45744f, -0.05597f, 0.46517f, -0.41498f, 0.00965f, 0.07142f,
        -0.39568f, 0.36866f, -0.45744f, -0.31847f, 0.14476f, -0.22583f, -0.49219f, 0.37638f,
        -0.19494f, -0.13318f, 0.39182f, -0.35322f, 0.29531f, -0.24127f, 0.21039f, -0.18722f,
        0.45358f, 0.31461f, -0.13318f, -0.01737f, -0.36094f, 0.12932f, -0.25671f, 0.43814f,
        -0.16792f, 0.23355f, -0.22197f, 0.44972f, -0.42270f, 0.33392f, 0.42656f, 0.11774f,
        -0.39568f, 0.36866f, -0.45744f, -0.31847f, 0.14476f, -0.22583f, -0.49219f, 0.37638f,
        -0.19494f, -0.13318f, 0.39182f, -0.35322f, 0.29531f, -0.24127f, 0.21039f, -0.18722f,
        -0.13318f, 0.19494f, -0.03667f, 0.44972f, 0.24513f, -0.15248f, 0.08300f, -0.33006f,
        0.00579f, 0.12546f, 0.19494f, 0.05983f, -0.15634f, 0.14476f, 0.36480f, -0.04053f,
        -0.33006f, 0.25671f, -0.46903f, 0.37252f, 0.48833f, -0.09458f, -0.41112f, 0.19108f,
        0.08686f, -0.46903f, -0.07528f, 0.04053f, -0.26829f, -0.02895f, 0.22197f, -0.34164f,
        -0.13318f, 0.19494f, -0.03667f, 0.44972f, 0.24513f, -0.15248f, 0.08300f, -0.33006f,
        0.00579f, 0.12546f, 0.19494f, 0.05983f, -0.15634f, 0.14476f, 0.36480f, -0.04053f,
        0.47289f, -0.21811f, 0.06756f, -0.38410f, -0.27987f, -0.06369f, 0.27987f, 0.43814f,
        -0.25671f, -0.39182f, 0.49219f, -0.27601f, -0.07914f, -0.48061f, 0.42656f, -0.38410f,
        0.11002f, 0.03667f, -0.27215f, 0.15634f, 0.07528f, -0.22197f, 0.33006f, 0.38410f,
        -0.34936f, 0.27987f, 0.15248f, 0.40340f, 0.09844f, -0.16406f, -0.46131f, 0.03281f,
        0.47289f, -0.21811f, 0.06756f, -0.38410f, -0.27987f, -0.06369f, 0.27987f, 0.43814f,
        -0.25671f, -0.39182f, 0.49219f, -0.27601f, -0.07914f, -0.48061f, 0.42656f, -0.38410f,
        -0.29531f, 0.31461f, -0.10616f, 0.39954f, 0.01351f, 0.33778f, -0.43814f, 0.17178f,
        -0.08686f, 0.23741f, -0.44586f, 0.33778f, -0.00193f, -0.31461f, 0.23741f, -0.12932f,
        -0.22583f, -0.06756f, 0.40340f, -0.16792f, -0.43428f, 0.01351f, -0.14476f, -0.04053f,
        -0.29145f, 0.46517f, -0.13704f, -0.39182f, -0.32233f, 0.29531f, 0.38410f, 0.16020f,
        -0.29531f, 0.31461f, -0.10616f, 0.39954f, 0.01351f, 0.33778f, -0.43814f, 0.17178f,
        -0.08686f, 0.23741f, -0.44586f, 0.33778f, -0.00193f, -0.31461f, 0.23741f, -0.12932f,
        -0.44200f, 0.26443f, 0.12546f, -0.42270f, 0.21425f, -0.19881f, -0.35708f, 0.04825f,
        0.36480f, -0.02895f, -0.21425f, 0.09072f, 0.41498f, 0.18336f, 0.04439f, 0.29917f,
        0.47675f, -0.40340f, 0.27601f, -0.31461f, 0.31075f, 0.17564f, 0.24899f, -0.45744f,
        0.05597f, -0.19494f, 0.00193f, 0.36094f, 0.24127f, -0.09844f, -0.24513f, -0.00965f,
        -0.44200f, 0.26443f, 0.12546f, -0.42270f, 0.21425f, -0.19881f, -0.35708f, 0.04825f,
        0.36480f, -0.02895f, -0.21425f, 0.09072f, 0.41498f, 0.18336f, 0.04439f, 0.29917f,
        -0.17564f, -0.05597f, -0.34550f, -0.24899f, 0.48061f, 0.15248f, -0.11388f, 0.45358f,
        -0.16406f, -0.32233f, 0.31461f, -0.11774f, -0.36866f, -0.18722f, -0.25671f, -0.44200f,
        0.13318f, -0.02123f, 0.19881f, -0.10616f, 0.43042f, -0.36866f, -0.24899f, 0.41112f,
        0.11002f, 0.21425f, -0.25671f, -0.47675f, -0.04439f, 0.13704f, -0.37252f, 0.43814f,
        -0.17564f, -0.05597f, -0.34550f, -0.24899f, 0.48061f, 0.15248f, -0.11388f, 0.45358f,
        -0.16406f, -0.32233f, 0.31461f, -0.11774f, -0.36866f, -0.18722f, -0.25671f, -0.44200f,
        0.19108f, 0.03667f, 0.35708f, -0.14090f, 0.08300f, -0.02123f, -0.30303f, -0.48061f,
        0.11774f, 0.20267f, -0.43042f, 0.25285f, 0.14090f, -0.04439f, 0.38796f, 0.34550f,
        -0.34164f, -0.19494f, 0.05983f, -0.48447f, 0.09844f, -0.00579f, -0.07914f, 0.33778f,
        -0.41498f, -0.10230f, 0.30689f, 0.17178f, 0.48833f, -0.20267f, 0.07914f, 0.33392f,
        0.19108f, 0.03667f, 0.35708f, -0.14090f, 0.08300f, -0.02123f, -0.30303f, -0.48061f,
        0.11774f, 0.20267f, -0.43042f, 0.25285f, 0.14090f, -0.04439f, 0.38796f, 0.34550f,
        -0.48833f, -0.30689f, 0.41498f, 0.22969f, -0.44586f, 0.32233f, 0.25285f, 0.39182f,
        -0.23355f, 0.01737f, 0.42270f, -0.27987f, 0.46903f, -0.47289f, 0.02123f, -0.09072f,
        0.21811f, 0.44586f, -0.25285f, 0.36480f, -0.29145f, 0.47289f, -0.18722f, 0.14476f,
        -0.31461f, 0.43814f, -0.36094f, 0.04439f, -0.29917f, -0.41884f, 0.25285f, -0.11774f,
        -0.48833f, -0.30689f, 0.41498f, 0.22969f, -0.44586f, 0.32233f, 0.25285f, 0.39182f,
        -0.23355f, 0.01737f, 0.42270f, -0.27987f, 0.46903f, -0.47289f, 0.02123f, -0.09072f,
        0.46131f, 0.11388f, -0.21039f, -0.07528f, -0.38024f, -0.26057f, 0.06369f, -0.05983f,
        0.29145f, -0.40340f, -0.09072f, 0.06756f, -0.16020f, 0.27601f, -0.31075f, 0.10616f,
        -0.14090f, -0.43042f, 0.25671f, -0.05211f, -0.13318f, 0.23355f, -0.44972f, 0.02895f,
        0.26829f, -0.02895f, -0.17950f, 0.37252f, -0.13704f, 0.40726f, 0.01351f, -0.26443f,
        0.46131f, 0.11388f, -0.21039f, -0.07528f, -0.38024f, -0.26057f, 0.06369f, -0.05983f,
        0.29145f, -0.40340f, -0.09072f, 0.06756f, -0.16020f, 0.27601f, -0.31075f, 0.10616f,
        -0.03281f, -0.40340f, 0.27987f, 0.17564f, 0.02509f, 0.44200f, -0.15248f, -0.34550f,
        0.14862f, -0.19881f, -0.01351f, 0.36866f, -0.38796f, 0.19494f, -0.22197f, 0.32619f,
        -0.37638f, 0.00193f, 0.30689f, 0.12160f, -0.39182f, 0.16792f, -0.34550f, 0.39954f,
        -0.23355f, 0.09072f, -0.43428f, 0.22969f, -0.06369f, 0.12546f, -0.35322f, 0.30689f,
        -0.03281f, -0.40340f, 0.27987f, 0.17564f, 0.02509f, 0.44200f, -0.15248f, -0.34550f,
        0.14862f, -0.19881f, -0.01351f, 0.36866f, -0.38796f, 0.19494f, -0.22197f, 0.32619f,
        -0.09844f, 0.06756f, 0.38410f, -0.33392f, -0.18336f, 0.35322f, 0.21039f, -0.42270f,
        0.48833f, 0.33006f, 0.21811f, -0.33392f, 0.12932f, -0.05211f, 0.39568f, 0.04825f,
        0.48061f, 0.17950f, -0.31847f, -0.21811f, 0.38024f, 0.05211f, 0.32233f, -0.06756f,
        -0.12546f, 0.46131f, 0.16020f, -0.25285f, 0.29531f, -0.44972f, 0.17950f, -0.16406f,
        -0.09844f, 0.06756f, 0.38410f, -0.33392f, -0.18336f, 0.35322f, 0.21039f, -0.42270f,
        0.48833f, 0.33006f, 0.21811f, -0.33392f, 0.12932f, -0.05211f, 0.39568f, 0.04825f,
        0.22583f, -0.46131f, -0.27601f, -0.00579f, 0.12932f, -0.47289f, -0.09844f, 0.10230f,
        -0.28759f, -0.12160f, -0.49219f, -0.24127f, 0.44586f, -0.11388f, -0.45358f, -0.27215f,
        -0.17178f, -0.07528f, -0.47675f, 0.43042f, -0.02509f, -0.27215f, -0.19108f, 0.19881f,
        -0.49219f, -0.37252f, 0.33392f, -0.00193f, -0.33006f, -0.20267f, 0.48061f, 0.34164f,
        0.22583f, -0.46131f, -0.27601f, -0.00579f, 0.12932f, -0.47289f, -0.09844f, 0.10230f,
        -0.28759f, -0.12160f, -0.49219f, -0.24127f, 0.44586f, -0.11388f, -0.45358f, -0.27215f,
        -0.22969f, 0.42270f, -0.12160f, 0.31075f, 0.46903f, -0.22583f, 0.27215f, -0.02509f,
        0.03281f, 0.40340f, 0.25671f, 0.08686f, 0.00965f, 0.29145f, -0.41112f, 0.14090f,
        0.24513f, 0.34164f, 0.08686f, -0.14862f, 0.27601f, -0.42656f, 0.48447f, 0.09844f,
        0.26443f, -0.27987f, 0.05597f, -0.10230f, 0.43428f, 0.08686f, 0.02895f, -0.38024f,
        -0.22969f, 0.42270f, -0.12160f, 0.31075f, 0.46903f, -0.22583f, 0.27215f, -0.02509f,
        0.03281f, 0.40340f, 0.25671f, 0.08686f, 0.00965f, 0.29145f, -0.41112f, 0.14090f,
        0.15634f, 0.09458f, -0.36480f, 0.18336f, -0.05211f, -0.40726f, 0.36866f, -0.33778f,
        -0.19881f, 0.16020f, -0.37638f, -0.16020f, -0.29917f, 0.20267f, 0.41884f, -0.01737f,
        -0.34936f, -0.24127f, 0.02509f, 0.20653f, -0.36480f, -0.08686f, 0.01737f, -0.33778f,
        0.41498f, -0.03667f, 0.37638f, -0.17178f, -0.47289f, 0.26829f, -0.28759f, -0.05597f,
        0.15634f, 0.09458f, -0.36480f, 0.18336f, -0.05211f, -0.40726f, 0.36866f, -0.33778f,
        -0.19881f, 0.16020f, -0.37638f, -0.16020f, -0.29917f, 0.20267f, 0.41884f, -0.01737f,
        0.35708f, 0.00193f, 0.25285f, -0.15634f, -0.30303f, 0.06369f, 0.22197f, 0.45358f,
        -0.43814f, 0.30303f, -0.04053f, 0.46517f, 0.35322f, -0.21039f, 0.06756f, -0.14090f,
        0.37638f, -0.43042f, 0.45744f, -0.29531f, 0.39568f, 0.14862f, 0.23741f, -0.13704f,
        -0.21425f, 0.16406f, -0.40726f, 0.22583f, 0.13318f, 0.38796f, -0.12932f, -0.43428f,
        0.35708f, 0.00193f, 0.25285f, -0.15634f, -0.30303f, 0.06369f, 0.22197f, 0.45358f,
        -0.43814f, 0.30303f, -0.04053f, 0.46517f, 0.35322f, -0.21039f, 0.06756f, -0.14090f,
        -0.31461f, -0.20653f, 0.46131f, -0.45358f, 0.39568f, -0.24513f, -0.14090f, 0.11002f,
        -0.08300f, -0.26829f, 0.05211f, -0.46517f, -0.09844f, -0.39568f, -0.32619f, -0.06369f,
        0.16792f, 0.28373f, 0.11388f, -0.04439f, -0.18336f, -0.44200f, 0.35322f, -0.26057f,
        -0.46517f, 0.31075f, -0.07914f, -0.34164f, -0.24513f, -0.02123f, 0.19108f, 0.44200f,
        -0.31461f, -0.20653f, 0.46131f, -0.45358f, 0.39568f, -0.24513f, -0.14090f, 0.11002f,
        -0.08300f, -0.26829f, 0.05211f, -0.46517f, -0.09844f, -0.39568f, -0.32619f, -0.06369f,
        0.04825f, -0.07914f, -0.39954f, 0.12160f, 0.29145f, 0.00965f, -0.37638f, 0.32233f,
        0.20267f, -0.17564f, 0.39182f, 0.12160f, 0.18336f, 0.32619f, 0.26057f, 0.49219f,
        -0.48447f, -0.20653f, -0.10616f, -0.38796f, 0.31847f, 0.07528f, -0.01737f, 0.44586f,
        0.11774f, 0.02509f, 0.47289f, 0.07142f, 0.33392f, -0.38410f, -0.17950f, 0.28373f,
        0.04825f, -0.07914f, -0.39954f, 0.12160f, 0.29145f, 0.00965f, -0.37638f, 0.32233f,
        0.20267f, -0.17564f, 0.39182f, 0.12160f, 0.18336f, 0.32619f, 0.26057f, 0.49219f
    ];

    private static bool ShouldFlipX(JxlExifOrientation orientation) =>
        orientation is JxlExifOrientation.FlipHorizontal or
            JxlExifOrientation.Rotate180 or
            JxlExifOrientation.Rotate270 or
            JxlExifOrientation.AntiTranspose;

    private static bool ShouldFlipY(JxlExifOrientation orientation) =>
        orientation is JxlExifOrientation.FlipVertical or
            JxlExifOrientation.Rotate180 or
            JxlExifOrientation.Rotate90 or
            JxlExifOrientation.AntiTranspose;

    private static bool ShouldTranspose(JxlExifOrientation orientation) =>
        orientation is JxlExifOrientation.Transpose or
            JxlExifOrientation.Rotate90 or
            JxlExifOrientation.Rotate270 or
            JxlExifOrientation.AntiTranspose;

    public override RenderPipelineChannelMode GetChannelMode(int channel)
    {
        if (channel < this.numColors || (this.hasAlpha && channel == this.alphaC))
        {
            return RenderPipelineChannelMode.Input;
        }

        foreach (IJxlImageOutput ec in this.extraChannels)
        {
            if (channel == ec.ChannelIndex)
            {
                return RenderPipelineChannelMode.Input;
            }
        }

        return RenderPipelineChannelMode.Ignored;
    }

    public override void Dispose()
    {
        base.Dispose();

        foreach (IDisposable disposables in this.disposables)
        {
            disposables.Dispose();
        }
    }

    public override void ProcessRow(Buffer2D<Memory<float>> inputRows, Buffer2D<Memory<float>> outputRows, int xExtraLeft, int xExtraRight, int width, int xPos, int yPos)
    {
        // HACK: to pass thread ID we use the mask in xExtraLeft, which shall be 0 anyway
        int threadId = xExtraLeft & ~7;

        if (yPos >= this.height)
        {
            return;
        }

        if (xPos >= this.width)
        {
            return;
        }

        if (this.flipY)
        {
            yPos = this.height - 1 - yPos;
        }

        int limit = Math.Min(width, this.width - xPos);

        for (int x0 = 0; x0 < limit; x0 += ChunkSize)
        {
            int xStart = xPos + x0;
            int length = Math.Min(ChunkSize, limit - x0);

            Span<Memory<float>> lineBuffers = [Memory<float>.Empty, Memory<float>.Empty, Memory<float>.Empty, Memory<float>.Empty];

            for (int c = 0; c < this.numColors; c++)
            {
                lineBuffers[c] = this.GetInputRowMemory(inputRows, c, 0)[x0..];
            }

            if (this.hasAlpha)
            {
                lineBuffers[this.numColors] = this.GetInputRowMemory(inputRows, this.alphaC, 0)[x0..];
            }
            else
            {
                lineBuffers[this.numColors] = JxlMemoryHelpers.MemoryFromList(this.opaqueAlpha);
            }

            if (this.hasAlpha && this.wantAlpha && this.unpremultiplyAlpha)
            {
                this.UnpremultiplyAlpha(threadId, length, lineBuffers);
            }

            this.OutputBuffers(this.main, threadId, yPos, ref xStart, length, lineBuffers[0], lineBuffers[1], lineBuffers[2], lineBuffers[3]);

            foreach (IJxlImageOutput extra in this.extraChannels)
            {
                lineBuffers[0] = this.GetInputRowMemory(inputRows, extra.ChannelIndex, 0)[x0..];
                this.OutputBuffers(extra, threadId, yPos, ref xStart, length, lineBuffers[0], lineBuffers[1], lineBuffers[2], lineBuffers[3]);
            }
        }
    }

    public void PrepareForThreads(Configuration configuration, int numThreads)
    {
        this.tempOut.Resize(numThreads);
        int allocSize = sizeof(float) * ChunkSize;

        for (int i = 0; i < this.tempOut.Count; i++)
        {
            IMemoryOwner<byte> owner = configuration.MemoryAllocator.Allocate<byte>(allocSize * this.main.PixelFormat.Channels);
            this.disposables.Add(owner);
            this.tempOut[i] = owner.Memory;
        }

        if ((this.hasAlpha && this.wantAlpha && this.unpremultiplyAlpha) || this.flipX)
        {
            this.tempIn.Resize(numThreads * this.main.PixelFormat.Channels);

            for (int i = 0; i < this.tempIn.Count; i++)
            {
                IMemoryOwner<byte> owner = configuration.MemoryAllocator.Allocate<byte>(allocSize);
                this.disposables.Add(owner);
                this.tempIn[i] = owner.Memory;
            }
        }
    }

    private unsafe void UnpremultiplyAlpha(int threadId, int len, Span<Memory<float>> lineBuffers)
    {
        // Highly unsafe code! ⚠️
        float** tempIn = stackalloc float*[4];
        Vector<float> one = Vector<float>.One;

        for (int c = 0; c < this.main.PixelFormat.Channels; ++c)
        {
            // size_t tix = thread_id * main_.num_channels_ + c;
            // temp_in[c] = temp_in_[tix].address<float>();
            // memcpy(temp_in[c], line_buffers[c], sizeof(float) * len);
            int tix = (threadId * this.main.PixelFormat.Channels) + c;

            tempIn[c] = (float*)Unsafe.AsPointer(ref MemoryMarshal.Cast<byte, float>(this.tempIn[tix].Span)[0]);

            lineBuffers[c].Span[..len]
                .CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.AsRef<float>(tempIn[c]), len));
        }

        Vector<float> smallAlpha = Vector.Create(SmallAlpha);

        for (int ix = 0; ix < len; ix += Vector<float>.Count)
        {
            float* ptr = tempIn[this.numColors + ix];

            // Using an aligned and unaligned branch
            // REVIEW: does the branch outweigh alignment? we will have to benchmark this
            // when the codec can build
            if (JxlUnsafe.IsSimdAligned(ptr))
            {
                Vector<float> alpha = Vector.LoadAlignedNonTemporal(tempIn[this.numColors] + ix);
                Vector<float> mul = one / Vector.Max(smallAlpha, alpha);

                for (int c = 0; c < this.numColors; ++c)
                {
                    float* currPtr = tempIn[c] + ix;

                    if (JxlUnsafe.IsSimdAligned(currPtr))
                    {
                        Vector<float> val = Vector.LoadAlignedNonTemporal(currPtr);
                        Vector.StoreAlignedNonTemporal(val * mul, currPtr);
                    }
                    else
                    {
                        Vector<float> val = Vector.Load(currPtr);
                        Vector.Store(val * mul, currPtr);
                    }
                }
            }
            else
            {
                Vector<float> alpha = Vector.Load(tempIn[this.numColors] + ix);
                Vector<float> mul = one / Vector.Max(smallAlpha, alpha);

                for (int c = 0; c < this.numColors; ++c)
                {
                    float* currPtr = tempIn[c] + ix;

                    if (JxlUnsafe.IsSimdAligned(currPtr))
                    {
                        Vector<float> val = Vector.LoadAlignedNonTemporal(currPtr);
                        Vector.StoreAlignedNonTemporal(val * mul, currPtr);
                    }
                    else
                    {
                        Vector<float> val = Vector.Load(currPtr);
                        Vector.Store(val * mul, currPtr);
                    }
                }
            }
        }

        for (int c = 0; c < this.main.PixelFormat.Channels; c++)
        {
            lineBuffers[c] = JxlMemoryHelpers.CastMemory<byte, float>(this.tempIn[c]);
        }
    }

    private static void StoreFloatRow(IJxlImageOutput output, Span<float> input0, Span<float> input1, Span<float> input2, Span<float> input3, int length, Span<float> result)
    {
        Span<float> bufferSpan = MemoryMarshal.Cast<byte, float>(output.Buffer.Span);

        if (output.PixelFormat.Channels == 1)
        {
            input0.CopyTo(result);
        }
        else if (output.PixelFormat.Channels == 2)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                JxlSimdUtils.StoreInterleaved(
                    Vector.Create<float>(input0[i..]),
                    Vector.Create<float>(input1[i..]),
                    ref bufferSpan[2 * i]);
            }
        }
        else if (output.PixelFormat.Channels == 3)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                JxlSimdUtils.StoreInterleaved(
                    Vector.Create<float>(input0[i..]),
                    Vector.Create<float>(input1[i..]),
                    Vector.Create<float>(input2[i..]),
                    ref bufferSpan[3 * i]);
            }
        }
        else if (output.PixelFormat.Channels == 4)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                JxlSimdUtils.StoreInterleaved(
                    Vector.Create<float>(input0[i..]),
                    Vector.Create<float>(input1[i..]),
                    Vector.Create<float>(input2[i..]),
                    Vector.Create<float>(input3[i..]),
                    ref bufferSpan[4 * i]);
            }
        }
    }

    private static void StoreFloat16Row(IJxlImageOutput output, Span<float> input0, Span<float> input1, Span<float> input2, Span<float> input3, int length, Span<ushort> result)
    {
        if (output.PixelFormat.Channels == 1)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                Vector<float> v0 = Vector.Create<float>(input0[i..]);
                JxlHalfUtils.ConvertSingleToHalf(v0).CopyTo(result[i..]);
            }
        }
        else if (output.PixelFormat.Channels == 2)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                Vector<float> v0 = Vector.Create<float>(input0[i..]);
                Vector<float> v1 = Vector.Create<float>(input1[i..]);
                JxlSimdUtils.StoreInterleaved(
                    JxlHalfUtils.ConvertSingleToHalf(v0),
                    JxlHalfUtils.ConvertSingleToHalf(v1),
                    ref result[i * 2]);
            }
        }
        else if (output.PixelFormat.Channels == 3)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                Vector<float> v0 = Vector.Create<float>(input0[i..]);
                Vector<float> v1 = Vector.Create<float>(input1[i..]);
                Vector<float> v2 = Vector.Create<float>(input2[i..]);
                JxlSimdUtils.StoreInterleaved(
                    JxlHalfUtils.ConvertSingleToHalf(v0),
                    JxlHalfUtils.ConvertSingleToHalf(v1),
                    JxlHalfUtils.ConvertSingleToHalf(v2),
                    ref result[i * 3]);
            }
        }
        else if (output.PixelFormat.Channels == 4)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                Vector<float> v0 = Vector.Create<float>(input0[i..]);
                Vector<float> v1 = Vector.Create<float>(input1[i..]);
                Vector<float> v2 = Vector.Create<float>(input2[i..]);
                Vector<float> v3 = Vector.Create<float>(input3[i..]);
                JxlSimdUtils.StoreInterleaved(
                    JxlHalfUtils.ConvertSingleToHalf(v0),
                    JxlHalfUtils.ConvertSingleToHalf(v1),
                    JxlHalfUtils.ConvertSingleToHalf(v2),
                    JxlHalfUtils.ConvertSingleToHalf(v3),
                    ref result[i * 4]);
            }
        }
    }

    private static void StoreUnsignedRow<TUnsigned>(IJxlImageOutput output, Span<float> input0, Span<float> input1, Span<float> input2, Span<float> input3, int length, Span<TUnsigned> result, int xStart, int yPos)
        where TUnsigned : unmanaged, INumber<TUnsigned>
    {
        Vector<float> mul = Vector.Create((1 << output.BitsPerSample) - 1.0f);

        if (output.PixelFormat.Channels == 1)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                MakeUnsigned<TUnsigned>(
                    Vector.Create<float>(input0[i..]),
                    xStart + i,
                    yPos,
                    mul,
                    0)
                .CopyTo(result[i..]);
            }
        }
        else if (output.PixelFormat.Channels == 2)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                JxlSimdUtils.StoreInterleaved(
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input0[i..]), xStart + i, yPos, mul, 0),
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input1[i..]), xStart + i, yPos, mul, 1),
                    ref result[i * 2]);
            }
        }
        else if (output.PixelFormat.Channels == 3)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                JxlSimdUtils.StoreInterleaved(
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input0[i..]), xStart + i, yPos, mul, 0),
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input1[i..]), xStart + i, yPos, mul, 1),
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input2[i..]), xStart + i, yPos, mul, 2),
                    ref result[i * 3]);
            }
        }
        else if (output.PixelFormat.Channels == 4)
        {
            for (int i = 0; i < length; i += Vector<float>.Count)
            {
                JxlSimdUtils.StoreInterleaved(
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input0[i..]), xStart + i, yPos, mul, 0),
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input1[i..]), xStart + i, yPos, mul, 1),
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input2[i..]), xStart + i, yPos, mul, 2),
                    MakeUnsigned<TUnsigned>(Vector.Create<float>(input3[i..]), xStart + i, yPos, mul, 3),
                    ref result[i * 4]);
            }
        }
    }

    private static Vector<T> MakeUnsigned<T>(Vector<float> v, int x0, int y0, Vector<float> mul, int c)
        where T : unmanaged
    {
        v *= mul;

        if (typeof(T) == typeof(byte))
        {
            int xOff = (x0 + (c * 23)) % 32;
            int yOff = (y0 + (c * 13)) % 32;
            int pos = (yOff * 48) + xOff;

            Vector<float> dither = Vector.Create(DitheringPattern[pos..]);
            v += dither;
        }

        v = Vector.Min(Vector.Max(Vector<float>.Zero, v), mul);

        Vector<int> ni = Vector.ConvertToInt32(v);
        Vector<uint> nu = Vector.AsVectorUInt32(ni);

        if (typeof(T) == typeof(uint))
        {
            return (Vector<T>)(object)nu;
        }

        return nu.As<uint, T>();
    }

    private void FlipX(IJxlImageOutput output, int threadId, int length, ref int xStart, Span<Memory<float>> lineBuffers)
    {
        InlineArray4<Memory<float>> temporaryInput = default;

        for (int c = 0; c < output.PixelFormat.Channels; c++)
        {
            int tix = (threadId * this.main.PixelFormat.Channels) + c;
            temporaryInput[c] = JxlMemoryHelpers.CastMemory<byte, float>(this.tempIn[tix]);

            lineBuffers[c][..length].Span.CopyTo(temporaryInput[c].Span);
        }

        int last = length - 1;
        int num = length / 2;

        for (int i = 0; i < num; i++)
        {
            for (int c = 0; c < output.PixelFormat.Channels; c++)
            {
                RuntimeUtility.Swap(ref temporaryInput[c].Span[i], ref temporaryInput[c].Span[last - i]);
            }
        }

        for (int c = 0; c < output.PixelFormat.Channels; c++)
        {
            lineBuffers[c] = temporaryInput[c];
        }

        xStart = this.width - xStart - length;
    }

    private unsafe void WriteToOutput<T>(IJxlImageOutput output, int yPos, int xStart, int length, Span<T> result)
        where T : unmanaged
    {
        if (this.transpose)
        {
            int stride = output.PixelFormat.Channels * sizeof(T);
            int offset = (xStart * output.Stride) + (yPos * stride);

            for (int i = 0, j = 0; i < length; i++, j += output.PixelFormat.Channels)
            {
                int ix = offset + (i * output.Stride);
                MemoryMarshal.Cast<T, byte>(result).Slice(j, stride).CopyTo(output.Buffer.Span[ix..]);
            }
        }
        else
        {
            int stride = output.PixelFormat.Channels * sizeof(T);
            int offset = (yPos * output.Stride) + (xStart * stride);
            MemoryMarshal.Cast<T, byte>(result)[..(length * stride)].CopyTo(output.Buffer.Span[offset..]);
        }
    }

    private void OutputBuffers(IJxlImageOutput output, int threadId, int ypos, ref int xstart, int length, Memory<float> input0, Memory<float> input1, Memory<float> input2, Memory<float> input3)
    {
        if (this.flipX)
        {
            Span<Memory<float>> buffers = [input0, input1, input2, input3];
            this.FlipX(output, threadId, length, ref xstart, buffers);
        }

        if (output.PixelFormat.DataType == IO.JxlDataType.Byte)
        {
            Span<byte> temp = this.tempOut[threadId].Span;
            StoreUnsignedRow(output, input0.Span, input1.Span, input2.Span, input3.Span, length, temp, xstart, ypos);
            this.WriteToOutput(output, ypos, xstart, length, temp);
        }
        else if (output.PixelFormat.DataType is IO.JxlDataType.UInt16 or IO.JxlDataType.Single)
        {
            Span<ushort> temp = MemoryMarshal.Cast<byte, ushort>(this.tempOut[threadId].Span);

            if (output.PixelFormat.DataType == IO.JxlDataType.UInt16)
            {
                StoreUnsignedRow(output, input0.Span, input1.Span, input2.Span, input3.Span, length, temp, xstart, ypos);
            }
            else
            {
                StoreFloat16Row(output, input0.Span, input1.Span, input2.Span, input3.Span, length, temp);
            }

            if (output.SwapEndianness)
            {
                int outputLength = length * output.PixelFormat.Channels;
                for (int j = 0; j < outputLength; j += Vector<ushort>.Count)
                {
                    Vector<ushort> v = Vector.Create<ushort>(temp[j..]);
                    Vector<ushort> vswap = Vector.ShiftRightLogical(v, 8) | (v << 8);
                    vswap.CopyTo(temp[j..]);
                }
            }

            this.WriteToOutput(output, ypos, xstart, length, temp);
        }
        else if (output.PixelFormat.DataType == IO.JxlDataType.Single)
        {
            Span<float> temp = MemoryMarshal.Cast<byte, float>(this.tempOut[threadId].Span);
            StoreFloatRow(output, input0.Span, input1.Span, input2.Span, input3.Span, length, temp);

            if (output.SwapEndianness)
            {
                int outputLength = length * output.PixelFormat.Channels;

                for (int j = 0; j < outputLength; j++)
                {
                    temp[j] = ReverseEndianness(temp[j]);
                }
            }

            this.WriteToOutput(output, ypos, xstart, length, temp);
        }
    }

    private static float ReverseEndianness(float value)
    {
        int bits = BitConverter.SingleToInt32Bits(value);
        bits = BinaryPrimitives.ReverseEndianness(bits);
        return BitConverter.Int32BitsToSingle(bits);
    }
}
