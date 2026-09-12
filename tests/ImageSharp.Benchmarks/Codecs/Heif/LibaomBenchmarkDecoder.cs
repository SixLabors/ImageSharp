// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.Metadata.Profiles.Cicp;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Owns the benchmark-only native decoder and converts its borrowed output through the production HEIF converter.
/// </summary>
internal sealed unsafe partial class LibaomBenchmarkDecoder : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// The existing benchmark adapter, linked against the verified optimized libaom build.
    /// </summary>
    private const string LibraryName = "imagesharp_aom_benchmark";

    /// <summary>
    /// Initializes the empty handle populated by the generated native marshaller.
    /// </summary>
    public LibaomBenchmarkDecoder()
        : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Opens one single-threaded native decoder.
    /// </summary>
    public static LibaomBenchmarkDecoder Open()
    {
        CheckStatus(Create(out LibaomBenchmarkDecoder decoder));
        return decoder;
    }

    /// <summary>
    /// Decodes one complete picture and returns independently owned packed pixels.
    /// </summary>
    public ImageFrame<Rgb48> Decode(Configuration configuration, ReadOnlySpan<byte> payload)
    {
        NativeFrame frame;
        fixed (byte* data = payload)
        {
            CheckStatus(DecodeNative(this, data, (nuint)payload.Length, out frame));
        }

        HeifColorConversionParameters parameters = HeifColorConversionParameters.Create(
            (CicpColorPrimaries)frame.Primaries,
            (CicpTransferCharacteristics)frame.Transfer,
            (CicpMatrixCoefficients)frame.Matrix,
            frame.FullRange != 0,
            frame.BitDepth,
            frame.BitDepth,
            frame.Monochrome != 0,
            frame.SubsamplingX == 0 && frame.SubsamplingY == 0,
            out HeifColorConversionMode mode);

        ImageFrame<Rgb48> result = new(configuration, frame.Width, frame.Height);

        // The native context stays alive throughout conversion. Each row view borrows the codec's plane
        // with its real stride; no pixel-sized allocation or copy precedes the shared SIMD converter.
        if (frame.HighBitDepth == 0)
        {
            HeifPlanarColorConverter.ConvertToRgb<Rgb48, NativeSampleBuffer<byte>, byte, HeifByteSampleConverter>(
                configuration,
                new NativeSampleBuffer<byte>(frame),
                result.PixelBuffer.GetRegion(result.Bounds),
                in parameters,
                mode,
                0,
                0,
                result.Size,
                default,
                null,
                null,
                false,
                SixLabors.ImageSharp.Formats.Heif.HeifChromaUpsampling.Auto);
        }
        else
        {
            HeifPlanarColorConverter.ConvertToRgb<Rgb48, NativeSampleBuffer<ushort>>(
                configuration,
                new NativeSampleBuffer<ushort>(frame),
                result.PixelBuffer.GetRegion(result.Bounds),
                in parameters,
                mode,
                0,
                0,
                result.Size,
                default,
                null,
                null,
                false,
                SixLabors.ImageSharp.Formats.Heif.HeifChromaUpsampling.Auto);
        }

        GC.KeepAlive(this);
        return result;
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle() => Destroy(this.handle) == 0;

    /// <summary>
    /// Rejects failed native operations rather than measuring an incomplete decode.
    /// </summary>
    private static void CheckStatus(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(Marshal.PtrToStringUTF8(ErrorString(status)));
        }
    }

    /// <summary>
    /// Creates an owned opaque decoder through the C adapter.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_decoder_create")]
    private static partial int Create(out LibaomBenchmarkDecoder decoder);

    /// <summary>
    /// Borrows pinned encoded bytes and returns the adapter's fixed-layout native plane descriptor.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_decoder_decode")]
    private static partial int DecodeNative(LibaomBenchmarkDecoder decoder, byte* data, nuint length, out NativeFrame frame);

    /// <summary>
    /// Releases the native context through its allocating library.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_decoder_destroy")]
    private static partial int Destroy(nint decoder);

    /// <summary>
    /// Returns a static native-owned UTF-8 error description.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_error_string")]
    private static partial nint ErrorString(int status);

    /// <summary>
    /// Matches benchmark_decoded_frame in the C adapter: three pointers followed by fixed-width integer metadata.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeFrame
    {
        public byte* Y;
        public byte* U;
        public byte* V;
        public int YStride;
        public int UStride;
        public int VStride;
        public int Width;
        public int Height;
        public int BitDepth;
        public int HighBitDepth;
        public int SubsamplingX;
        public int SubsamplingY;
        public int Monochrome;
        public int Primaries;
        public int Transfer;
        public int Matrix;
        public int FullRange;
        public int ChromaPosition;
    }

    /// <summary>
    /// Adapts borrowed native rows to the existing closed-generic color-conversion contract.
    /// </summary>
    private readonly struct NativeSampleBuffer<TSample> : IHeifPlanarSampleBuffer<TSample>
        where TSample : unmanaged
    {
        private readonly NativeFrame frame;

        /// <summary>
        /// Retains only a non-owning plane descriptor for the synchronous conversion.
        /// </summary>
        public NativeSampleBuffer(NativeFrame frame) => this.frame = frame;

        /// <inheritdoc/>
        public int Width => this.frame.Width;

        /// <inheritdoc/>
        public int Height => this.frame.Height;

        /// <inheritdoc/>
        public int LumaBitDepth => this.frame.BitDepth;

        /// <inheritdoc/>
        public int ChromaBitDepth => this.frame.BitDepth;

        /// <inheritdoc/>
        public bool IsMonochrome => this.frame.Monochrome != 0;

        /// <inheritdoc/>
        public int ChromaSubsamplingX => this.frame.SubsamplingX;

        /// <inheritdoc/>
        public int ChromaSubsamplingY => this.frame.SubsamplingY;

        /// <inheritdoc/>
        public int ChromaPositionX
            => this.ChromaSubsamplingX != 0 && (this.ChromaSubsamplingY == 0 || this.frame.ChromaPosition == (int)ObuChromoSamplePosition.Unknown) ? 1 : 0;

        /// <inheritdoc/>
        public int ChromaPositionY => this.ChromaSubsamplingY != 0 && this.frame.ChromaPosition != (int)ObuChromoSamplePosition.Colocated ? 1 : 0;

        /// <inheritdoc/>
        public Span<TSample> GetLumaRowSpan(int row)
            => new(this.frame.Y + (row * this.frame.YStride), this.Width);

        /// <inheritdoc/>
        public Span<TSample> GetChromaBlueRowSpan(int row)
            => new(this.frame.U + (row * this.frame.UStride), (this.Width + this.ChromaSubsamplingX) >> this.ChromaSubsamplingX);

        /// <inheritdoc/>
        public Span<TSample> GetChromaRedRowSpan(int row)
            => new(this.frame.V + (row * this.frame.VStride), (this.Width + this.ChromaSubsamplingX) >> this.ChromaSubsamplingX);
    }
}
