// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Heif;

/// <summary>
/// Owns a benchmark-only libaom encoder through a C adapter compiled against the reference's actual headers.
/// </summary>
internal sealed unsafe partial class LibaomBenchmarkEncoder : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>
    /// The benchmark adapter's platform-independent library name.
    /// </summary>
    private const string LibraryName = "imagesharp_aom_benchmark";

    /// <summary>
    /// Initializes an empty handle for the native create call's generated marshaller.
    /// </summary>
    public LibaomBenchmarkEncoder()
        : base(ownsHandle: true)
    {
    }

    /// <summary>
    /// Opens one native sequence with a single coding thread, no lookahead, and the requested quality and speed.
    /// </summary>
    public static LibaomBenchmarkEncoder Open(int width, int height, int quality, int speed)
    {
        CheckStatus(Create((uint)width, (uint)height, (uint)quality, speed, out LibaomBenchmarkEncoder encoder));
        return encoder;
    }

    /// <summary>
    /// Encodes the converted source planes and consumes every output packet before those bytes can be invalidated.
    /// </summary>
    public void Encode(Av1EncoderFrame<byte> frame, long frameIndex, Stream output)
    {
        Buffer2DRegion<byte> y = frame.View.GetPlane(Av1Plane.Y);
        Buffer2DRegion<byte> u = frame.View.GetPlane(Av1Plane.U);
        Buffer2DRegion<byte> v = frame.View.GetPlane(Av1Plane.V);
        fixed (byte* yPointer = y.DangerousGetRowSpan(0), uPointer = u.DangerousGetRowSpan(0), vPointer = v.DangerousGetRowSpan(0))
        {
            // The native call is synchronous. Its input descriptors borrow these pinned rows only until
            // EncodeFrame returns; the encoder owns any retained reference and lookahead storage itself.
            CheckStatus(EncodeFrame(this, yPointer, uPointer, vPointer, y.Stride, u.Stride, frameIndex));
        }

        this.WritePackets(output);
    }

    /// <summary>
    /// Finishes the sequence and writes any remaining coded packets.
    /// </summary>
    public void Finish(Stream output)
    {
        do
        {
            CheckStatus(Flush(this));
        }
        while (this.WritePackets(output));
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle() => Destroy(this.handle) == 0;

    /// <summary>
    /// Writes borrowed packet memory before the next call into the native codec invalidates it.
    /// </summary>
    private bool WritePackets(Stream output)
    {
        bool wrotePacket = false;
        while (NextPacket(this, out byte* data, out nuint length) != 0)
        {
            // Packet lengths are bounded by the benchmark's image dimensions. Stream.Write consumes the
            // borrowed bytes synchronously, without retaining a native pointer or creating a managed array.
            output.Write(new ReadOnlySpan<byte>(data, (int)length));
            wrotePacket = true;
        }

        return wrotePacket;
    }

    /// <summary>
    /// Converts a native codec error into a managed benchmark failure instead of accepting invalid timing data.
    /// </summary>
    private static void CheckStatus(int status)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(Marshal.PtrToStringUTF8(ErrorString(status)));
        }
    }

    /// <summary>
    /// Creates an owned opaque context; no libaom structure layout crosses the managed boundary.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_create")]
    private static partial int Create(uint width, uint height, uint quality, int speed, out LibaomBenchmarkEncoder encoder);

    /// <summary>
    /// Borrows three pinned planes for one synchronous native encode call.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_encode")]
    private static partial int EncodeFrame(LibaomBenchmarkEncoder encoder, byte* y, byte* u, byte* v, int yStride, int uvStride, long frameIndex);

    /// <summary>
    /// Signals the end of the native sequence.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_flush")]
    private static partial int Flush(LibaomBenchmarkEncoder encoder);

    /// <summary>
    /// Returns borrowed native packet storage and its pointer-sized length.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_next_packet")]
    private static partial int NextPacket(LibaomBenchmarkEncoder encoder, out byte* data, out nuint length);

    /// <summary>
    /// Destroys the context through the same native library that allocated it.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_destroy")]
    private static partial int Destroy(nint encoder);

    /// <summary>
    /// Returns a static UTF-8 error message owned by libaom.
    /// </summary>
    [LibraryImport(LibraryName, EntryPoint = "benchmark_error_string")]
    private static partial nint ErrorString(int status);
}
