// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

/// <summary>
/// Exposes every stateless packed-pixel shuffle operator for assembly inspection.
/// </summary>
/// <remarks>
/// Seven pixels expose the short-input and vector-tail paths. Seventeen pixels execute one
/// full intrinsic group and leave one complete pixel for the scalar remainder, making every
/// part of each generated traversal observable.
/// </remarks>
[Config(typeof(Config.Analysis))]
public class PackedPixelConversionAssembly
{
    private byte[] source3;
    private byte[] source4;
    private byte[] destination3;
    private byte[] destination4;

    /// <summary>
    /// Gets or sets the number of pixels converted by each invocation.
    /// </summary>
    [Params(7, 17)]
    public int Count { get; set; }

    /// <summary>
    /// Populates the source buffers with deterministic non-uniform channel values.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.source3 = new byte[this.Count * 3];
        this.source4 = new byte[this.Count * 4];
        this.destination3 = new byte[this.Count * 3];
        this.destination4 = new byte[this.Count * 4];

        new Random(42).NextBytes(this.source3);
        new Random(42).NextBytes(this.source4);
    }

    /// <summary>
    /// Executes the WXYZ four-to-four operator.
    /// </summary>
    [Benchmark]
    public void Shuffle4Wxyz() => SimdUtils.Shuffle4<WXYZShuffle4>(this.source4, this.destination4);

    /// <summary>
    /// Executes the WZYX four-to-four operator.
    /// </summary>
    [Benchmark]
    public void Shuffle4Wzyx() => SimdUtils.Shuffle4<WZYXShuffle4>(this.source4, this.destination4);

    /// <summary>
    /// Executes the YZWX four-to-four operator.
    /// </summary>
    [Benchmark]
    public void Shuffle4Yzwx() => SimdUtils.Shuffle4<YZWXShuffle4>(this.source4, this.destination4);

    /// <summary>
    /// Executes the ZYXW four-to-four operator.
    /// </summary>
    [Benchmark]
    public void Shuffle4Zyxw() => SimdUtils.Shuffle4<ZYXWShuffle4>(this.source4, this.destination4);

    /// <summary>
    /// Executes the XWZY four-to-four operator.
    /// </summary>
    [Benchmark]
    public void Shuffle4Xwzy() => SimdUtils.Shuffle4<XWZYShuffle4>(this.source4, this.destination4);

    /// <summary>
    /// Executes the XYZ four-to-three operator.
    /// </summary>
    [Benchmark]
    public void Slice3Xyz() => SimdUtils.Shuffle4Slice3<XYZWShuffle4Slice3>(this.source4, this.destination3);

    /// <summary>
    /// Executes the YZW four-to-three operator.
    /// </summary>
    [Benchmark]
    public void Slice3Yzw() => SimdUtils.Shuffle4Slice3<YZWXShuffle4Slice3>(this.source4, this.destination3);

    /// <summary>
    /// Executes the WZY four-to-three operator.
    /// </summary>
    [Benchmark]
    public void Slice3Wzy() => SimdUtils.Shuffle4Slice3<WZYXShuffle4Slice3>(this.source4, this.destination3);

    /// <summary>
    /// Executes the ZYX four-to-three operator.
    /// </summary>
    [Benchmark]
    public void Slice3Zyx() => SimdUtils.Shuffle4Slice3<ZYXWShuffle4Slice3>(this.source4, this.destination3);

    /// <summary>
    /// Executes the XYZW three-to-four operator.
    /// </summary>
    [Benchmark]
    public void Pad4Xyzw() => SimdUtils.Pad3Shuffle4<XYZWPad3Shuffle4>(this.source3, this.destination4);

    /// <summary>
    /// Executes the WXYZ three-to-four operator.
    /// </summary>
    [Benchmark]
    public void Pad4Wxyz() => SimdUtils.Pad3Shuffle4<WXYZPad3Shuffle4>(this.source3, this.destination4);

    /// <summary>
    /// Executes the WZYX three-to-four operator.
    /// </summary>
    [Benchmark]
    public void Pad4Wzyx() => SimdUtils.Pad3Shuffle4<WZYXPad3Shuffle4>(this.source3, this.destination4);

    /// <summary>
    /// Executes the ZYXW three-to-four operator.
    /// </summary>
    [Benchmark]
    public void Pad4Zyxw() => SimdUtils.Pad3Shuffle4<ZYXWPad3Shuffle4>(this.source3, this.destination4);

    /// <summary>
    /// Executes the ZYX three-to-three operator.
    /// </summary>
    [Benchmark]
    public void Shuffle3Zyx() => SimdUtils.Shuffle3<ZYXShuffle3>(this.source3, this.destination3);
}
