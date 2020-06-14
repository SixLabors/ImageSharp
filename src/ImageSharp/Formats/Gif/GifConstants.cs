// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Constants that define specific points within a Gif.
    /// </summary>
    internal static class GifConstants
    {
        /// <summary>
        /// The file type.
        /// </summary>
        public const string FileType = "GIF";

        /// <summary>
        /// The file version.
        /// </summary>
        public const string FileVersion = "89a";

        /// <summary>
        /// The extension block introducer <value>!</value>.
        /// </summary>
        public const byte ExtensionIntroducer = 0x21;

        /// <summary>
        /// The graphic control label.
        /// </summary>
        public const byte GraphicControlLabel = 0xF9;

        /// <summary>
        /// The application extension label.
        /// </summary>
        public const byte ApplicationExtensionLabel = 0xFF;

        /// <summary>
        /// The application block size.
        /// </summary>
        public const byte ApplicationBlockSize = 11;

        /// <summary>
        /// The application identification.
        /// </summary>
        public const string NetscapeApplicationIdentification = "NETSCAPE2.0";

        /// <summary>
        /// The Netscape looping application sub block size.
        /// </summary>
        public const byte NetscapeLoopingSubBlockSize = 3;

        /// <summary>
        /// The comment label.
        /// </summary>
        public const byte CommentLabel = 0xFE;

        /// <summary>
        /// The maximum length of a comment data sub-block is 255.
        /// </summary>
        public const int MaxCommentSubBlockLength = 255;

        /// <summary>
        /// The image descriptor label <value>,</value>.
        /// </summary>
        public const byte ImageDescriptorLabel = 0x2C;

        /// <summary>
        /// The plain text label.
        /// </summary>
        public const byte PlainTextLabel = 0x01;

        /// <summary>
        /// The image label introducer <value>,</value>.
        /// </summary>
        public const byte ImageLabel = 0x2C;

        /// <summary>
        /// The terminator.
        /// </summary>
        public const byte Terminator = 0;

        /// <summary>
        /// The end introducer trailer <value>;</value>.
        /// </summary>
        public const byte EndIntroducer = 0x3B;

        /// <summary>
        /// The character encoding to use when reading and writing comments - (ASCII 7bit).
        /// </summary>
        public static readonly Encoding Encoding = Encoding.ASCII;

        /// <summary>
        /// The collection of mimetypes that equate to a Gif.
        /// </summary>
        public static readonly IEnumerable<string> MimeTypes = new[] { "image/gif" };

        /// <summary>
        /// The collection of file extensions that equate to a Gif.
        /// </summary>
        public static readonly IEnumerable<string> FileExtensions = new[] { "gif" };

        /// <summary>
        /// Gets the ASCII encoded bytes used to identify the GIF file (combining <see cref="FileType"/> and <see cref="FileVersion"/>).
        /// </summary>
        internal static ReadOnlySpan<byte> MagicNumber => new[]
        {
            (byte)'G', (byte)'I', (byte)'F',
            (byte)'8', (byte)'9', (byte)'a'
        };

        /// <summary>
        /// Gets the ASCII encoded application identification bytes (representing <see cref="NetscapeApplicationIdentification"/>).
        /// </summary>
        internal static ReadOnlySpan<byte> NetscapeApplicationIdentificationBytes => new[]
        {
            (byte)'N', (byte)'E', (byte)'T',
            (byte)'S', (byte)'C', (byte)'A',
            (byte)'P', (byte)'E',
            (byte)'2', (byte)'.', (byte)'0'
        };
    }
}
