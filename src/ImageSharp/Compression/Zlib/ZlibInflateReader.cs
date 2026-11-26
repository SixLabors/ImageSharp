// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Compression.Zlib;

/// <summary>
/// Reads chunked input, parses the zlib CMF/FLG header, and exposes a
/// <see cref="DeflateStream"/> over the remaining DEFLATE payload. The
/// Adler-32 trailer is not validated.
/// </summary>
internal sealed class ZlibInflateReader : IDisposable
{
    private readonly ChunkedReadStream segmentStream;

    public ZlibInflateReader(BufferedReadStream innerStream)
        => this.segmentStream = new ChunkedReadStream(innerStream);

    public ZlibInflateReader(BufferedReadStream innerStream, Func<int> getData)
        => this.segmentStream = new ChunkedReadStream(innerStream, getData);

    /// <summary>
    /// Gets the compressed stream over the deframed inner stream.
    /// </summary>
    public DeflateStream? CompressedStream { get; private set; }

    /// <summary>
    /// Sets the length of the next segment of compressed input. On the first call, the method also parses the zlib header.
    /// </summary>
    /// <param name="bytes">The remaining data length for the current segment.</param>
    /// <returns>
    /// <see langword="true"/> if the inflate stream is ready. <see langword="false"/> if the zlib header is missing or not valid. The caller applies its own
    /// integrity policy.
    /// </returns>
    [MemberNotNullWhen(true, nameof(CompressedStream))]
    public bool AllocateNewBytes(int bytes)
    {
        this.segmentStream.SetCurrentSegmentLength(bytes);
        if (this.CompressedStream is null)
        {
            return this.InitializeInflateStream();
        }

        return true;
    }

    public void Dispose()
    {
        this.CompressedStream?.Dispose();
        this.segmentStream?.Dispose();
    }

    [MemberNotNullWhen(true, nameof(CompressedStream))]
    private bool InitializeInflateStream()
    {
        // Read the zlib header : http://tools.ietf.org/html/rfc1950
        // CMF(Compression Method and flags)
        // This byte is divided into a 4 - bit compression method and a
        // 4-bit information field depending on the compression method.
        // bits 0 to 3  CM Compression method
        // bits 4 to 7  CINFO Compression info
        //
        //   0   1
        // +---+---+
        // |CMF|FLG|
        // +---+---+
        int cmf = this.segmentStream.ReadByte();
        int flag = this.segmentStream.ReadByte();
        if (cmf == -1 || flag == -1)
        {
            return false;
        }

        if ((cmf & 0x0F) == 8)
        {
            // CINFO is the base-2 logarithm of the LZ77 window size, minus eight.
            int cinfo = (cmf & 0xF0) >> 4;

            // RFC 1950, section 2.2, does not allow a CINFO value of more than 7.
            if (cinfo > 7)
            {
                return false;
            }
        }
        else
        {
            // RFC 1950 defines only compression method 8 (deflate).
            return false;
        }

        // The preset dictionary.
        bool fdict = (flag & 32) != 0;
        if (fdict)
        {
            // We don't need this for inflate so simply skip by the next four bytes.
            // https://tools.ietf.org/html/rfc1950#page-6
            InlineArray4<byte> checksumBuffer = default;

            if (this.segmentStream.Read(checksumBuffer) != 4)
            {
                return false;
            }
        }

        this.CompressedStream = new DeflateStream(this.segmentStream, CompressionMode.Decompress, leaveOpen: true);

        return true;
    }
}
