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
internal sealed class ZlibInflateStream : IDisposable
{
    /// <summary>
    /// Used to read the Adler-32 and Crc-32 checksums.
    /// We don't actually use this for anything so it doesn't
    /// have to be threadsafe.
    /// </summary>
    private static readonly byte[] ChecksumBuffer = new byte[4];

    private readonly ChunkedReadStream segmentStream;

    public ZlibInflateStream(BufferedReadStream innerStream)
        => this.segmentStream = new ChunkedReadStream(innerStream);

    public ZlibInflateStream(BufferedReadStream innerStream, Func<int> getData)
        => this.segmentStream = new ChunkedReadStream(innerStream, getData);

    /// <summary>
    /// Gets the compressed stream over the deframed inner stream.
    /// </summary>
    public DeflateStream? CompressedStream { get; private set; }

    /// <summary>
    /// Sets the length of the next segment of compressed input and, on first
    /// call, parses the zlib header.
    /// </summary>
    /// <param name="bytes">The remaining data length for the current segment.</param>
    /// <param name="isCriticalChunk">Whether to throw on a malformed zlib header.</param>
    /// <returns>The <see cref="bool"/>.</returns>
    [MemberNotNullWhen(true, nameof(CompressedStream))]
    public bool AllocateNewBytes(int bytes, bool isCriticalChunk)
    {
        this.segmentStream.SetCurrentSegmentLength(bytes);
        if (this.CompressedStream is null)
        {
            return this.InitializeInflateStream(isCriticalChunk);
        }

        return true;
    }

    public void Dispose()
    {
        this.CompressedStream?.Dispose();
        this.segmentStream?.Dispose();
    }

    [MemberNotNullWhen(true, nameof(CompressedStream))]
    private bool InitializeInflateStream(bool isCriticalChunk)
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

            if (cinfo > 7)
            {
                if (isCriticalChunk)
                {
                    // Values of CINFO above 7 are not allowed in RFC1950.
                    // CINFO is not defined in this specification for CM not equal to 8.
                    throw new ImageFormatException($"Invalid window size for ZLIB header: cinfo={cinfo}");
                }

                return false;
            }
        }
        else if (isCriticalChunk)
        {
            throw new ImageFormatException($"Bad method for ZLIB header: cmf={cmf}");
        }
        else
        {
            return false;
        }

        // The preset dictionary.
        bool fdict = (flag & 32) != 0;
        if (fdict)
        {
            // We don't need this for inflate so simply skip by the next four bytes.
            // https://tools.ietf.org/html/rfc1950#page-6
            if (this.segmentStream.Read(ChecksumBuffer, 0, 4) != 4)
            {
                return false;
            }
        }

        this.CompressedStream = new DeflateStream(this.segmentStream, CompressionMode.Decompress, leaveOpen: true);

        return true;
    }
}
