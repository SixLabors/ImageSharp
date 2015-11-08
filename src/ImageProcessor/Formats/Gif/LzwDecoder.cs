// <copyright file="LzwDecoder.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Decompresses data using the LZW algorithms.
    /// </summary>
    internal sealed class LzwDecoder
    {
        /// <summary>
        /// One more than the maximum value 12 bit integer.
        /// </summary>
        private const int MaxStackSize = 4096;

        /// <summary>
        /// The null code.
        /// </summary>
        private const int NullCode = -1;

        /// <summary>
        /// The stream.
        /// </summary>
        private readonly Stream stream;

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwDecoder"/> class
        /// and sets the stream, where the compressed data should be read from.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is null.</exception>
        public LzwDecoder(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            this.stream = stream;
        }

        /// <summary>
        /// Decodes and decompresses all pixel indices from the stream.
        /// <remarks>
        /// </remarks>
        /// </summary>
        /// <param name="width">The width of the pixel index array.</param>
        /// <param name="height">The height of the pixel index array.</param>
        /// <param name="dataSize">Size of the data.</param>
        /// <returns>The decoded and uncompressed array.</returns>
        public byte[] DecodePixels(int width, int height, int dataSize)
        {
            Guard.MustBeLessThan(dataSize, int.MaxValue, nameof(dataSize));

            // The resulting index table.
            byte[] pixels = new byte[width * height];

            // Calculate the clear code. The value of the clear code is 2 ^ dataSize
            int clearCode = 1 << dataSize;

            int codeSize = dataSize + 1;

            // Calculate the end code
            int endCode = clearCode + 1;

            // Calculate the available code.
            int availableCode = clearCode + 2;

            // Jillzhangs Code (Not From Me) see: http://giflib.codeplex.com/
            // TODO: It's imperative that this code is cleaned up and commented properly.
            // TODO: Unfortunately I can't figure out the character encoding to translate from the original Chinese.
            int code; // ÓÃÓÚ´æ´¢µ±Ç°µÄ±àÂëÖµ
            int oldCode = NullCode; // ÓÃÓÚ´æ´¢ÉÏÒ»´ÎµÄ±àÂëÖµ
            int codeMask = (1 << codeSize) - 1; // ±íÊ¾±àÂëµÄ×î´óÖµ£¬Èç¹ûcodeSize=5,Ôòcode_mask=31
            int bits = 0; // ÔÚ±àÂëÁ÷ÖÐÊý¾ÝµÄ±£´æÐÎÊ½Îªbyte£¬¶øÊµ¼Ê±àÂë¹ý³ÌÖÐÊÇÕÒÊµ¼Ê±àÂëÎ»À´´æ´¢µÄ£¬±ÈÈçµ±codeSize=5µÄÊ±ºò£¬ÄÇÃ´Êµ¼ÊÉÏ5bitµÄÊý¾Ý¾ÍÓ¦¸Ã¿ÉÒÔ±íÊ¾Ò»¸ö±àÂë£¬ÕâÑùÈ¡³öÀ´µÄ1¸ö×Ö½Ú¾Í¸»ÓàÁË3¸öbit£¬Õâ3¸öbitÓÃÓÚºÍµÚ¶þ¸ö×Ö½ÚµÄºóÁ½¸öbit½øÐÐ×éºÏ£¬ÔÙ´ÎÐÎ³É±àÂëÖµ£¬Èç´ËÀàÍÆ

            int[] prefix = new int[MaxStackSize]; // ÓÃÓÚ±£´æÇ°×ºµÄ¼¯ºÏ
            int[] suffix = new int[MaxStackSize]; // ÓÃÓÚ±£´æºó×º
            int[] pixelStatck = new int[MaxStackSize + 1]; // ÓÃÓÚÁÙÊ±±£´æÊý¾ÝÁ÷

            int top = 0;
            int count = 0; // ÔÚÏÂÃæµÄÑ­»·ÖÐ£¬Ã¿´Î»á»ñÈ¡Ò»¶¨Á¿µÄ±àÂëµÄ×Ö½ÚÊý×é£¬¶ø´¦ÀíÕâÐ(c)Êý×éµÄÊ±ºòÐèÒª1¸ö¸ö×Ö½ÚÀ´´¦Àí£¬count¾ÍÊÇ±íÊ¾»¹Òª´¦ÀíµÄ×Ö½ÚÊýÄ¿
            int bi = 0; // count±íÊ¾»¹Ê£¶àÉÙ×Ö½ÚÐèÒª´¦Àí£¬¶øbiÔò±íÊ¾±¾´ÎÒÑ¾­´¦ÀíµÄ¸öÊý
            int xyz = 0; // i´ú±íµ±Ç°´¦ÀíµÃµ½ÏñËØÊý

            int data = 0; // ±íÊ¾µ±Ç°´¦ÀíµÄÊý¾ÝµÄÖµ
            int first = 0; // Ò»¸ö×Ö·û´®ÖØµÄµÚÒ»¸ö×Ö½Ú

            // ÏÈÉú³ÉÔªÊý¾ÝµÄÇ°×º¼¯ºÏºÍºó×º¼¯ºÏ£¬ÔªÊý¾ÝµÄÇ°×º¾ùÎª0£¬¶øºó×ºÓëÔªÊý¾ÝÏàµÈ£¬Í¬Ê±±àÂëÒ²ÓëÔªÊý¾ÝÏàµÈ
            for (code = 0; code < clearCode; code++)
            {
                // Ç°×º³õÊ¼Îª0
                prefix[code] = 0;

                // ºó×º=ÔªÊý¾Ý=±àÂë
                suffix[code] = (byte)code;
            }

            byte[] buffer = null;
            while (xyz < pixels.Length)
            {
                // ×î´óÏñËØÊýÒÑ¾­È·¶¨ÎªpixelCount = width * width
                if (top == 0)
                {
                    if (bits < codeSize)
                    {
                        // Èç¹ûµ±Ç°µÄÒª´¦ÀíµÄbitÊýÐ¡ÓÚ±àÂëÎ»´óÐ¡£¬ÔòÐèÒª¼ÓÔØÊý¾Ý
                        if (count == 0)
                        {
                            // Èç¹ûcountÎª0£¬±íÊ¾Òª´Ó±àÂëÁ÷ÖÐ¶ÁÒ»¸öÊý¾Ý¶ÎÀ´½øÐÐ·ÖÎö
                            buffer = this.ReadBlock();
                            count = buffer.Length;
                            if (count == 0)
                            {
                                // ÔÙ´ÎÏë¶ÁÈ¡Êý¾Ý¶Î£¬È´Ã»ÓÐ¶Áµ½Êý¾Ý£¬´ËÊ±¾Í±íÃ÷ÒÑ¾­´¦ÀíÍêÁË
                                break;
                            }

                            // ÖØÐÂ¶ÁÈ¡Ò»¸öÊý¾Ý¶Îºó£¬Ó¦¸Ã½«ÒÑ¾­´¦ÀíµÄ¸öÊýÖÃ0
                            bi = 0;
                        }

                        // »ñÈ¡±¾´ÎÒª´¦ÀíµÄÊý¾ÝµÄÖµ
                        if (buffer != null)
                        {
                            data += buffer[bi] << bits; // ´Ë´¦ÎªºÎÒªÒÆÎ»ÄØ£¬±ÈÈçµÚÒ»´Î´¦ÀíÁË1¸ö×Ö½ÚÎª176£¬µÚÒ»´ÎÖ»Òª´¦Àí5bit¾Í¹»ÁË£¬Ê£ÏÂ3bitÁô¸øÏÂ¸ö×Ö½Ú½øÐÐ×éºÏ¡£Ò²¾ÍÊÇµÚ¶þ¸ö×Ö½ÚµÄºóÁ½Î»+µÚÒ»¸ö×Ö½ÚµÄÇ°ÈýÎ»×é³ÉµÚ¶þ´ÎÊä³öÖµ
                        }

                        bits += 8; // ±¾´ÎÓÖ´¦ÀíÁËÒ»¸ö×Ö½Ú£¬ËùÒÔÐèÒª+8
                        bi++; // ½«´¦ÀíÏÂÒ»¸ö×Ö½Ú
                        count--; // ÒÑ¾­´¦Àí¹ýµÄ×Ö½ÚÊý+1
                        continue;
                    }

                    // Èç¹ûÒÑ¾­ÓÐ×ã¹»µÄbitÊý¿É¹(c)´¦Àí£¬ÏÂÃæ¾ÍÊÇ´¦Àí¹ý³Ì
                    // »ñÈ¡±àÂë
                    code = data & codeMask; // »ñÈ¡dataÊý¾ÝµÄ±àÂëÎ»´óÐ¡bitµÄÊý¾Ý
                    data >>= codeSize; // ½«±àÂëÊý¾Ý½ØÈ¡ºó£¬Ô­À´µÄÊý¾Ý¾ÍÊ£ÏÂ¼¸¸öbitÁË£¬´ËÊ±½«ÕâÐ(c)bitÓÒÒÆ£¬ÎªÏÂ´Î×÷×¼±¸
                    bits -= codeSize; // Í¬Ê±ÐèÒª½«µ±Ç°Êý¾ÝµÄbitÊý¼õÈ¥±àÂëÎ»³¤£¬ÒòÎªÒÑ¾­µÃµ½ÁË´¦Àí¡£

                    // ÏÂÃæ¸ù¾Ý»ñÈ¡µÄcodeÖµÀ´½øÐÐ´¦Àí
                    if (code > availableCode || code == endCode)
                    {
                        // µ±±àÂëÖµ´óÓÚ×î´ó±àÂëÖµ»òÕßÎª½áÊø±ê¼ÇµÄÊ±ºò£¬Í£Ö¹´¦Àí
                        break;
                    }

                    if (code == clearCode)
                    {
                        // Èç¹ûµ±Ç°ÊÇÇå³ý±ê¼Ç£¬ÔòÖØÐÂ³õÊ¼»¯±äÁ¿£¬ºÃÖØÐÂÔÙÀ´
                        codeSize = dataSize + 1;

                        // ÖØÐÂ³õÊ¼»¯×î´ó±àÂëÖµ
                        codeMask = (1 << codeSize) - 1;

                        // ³õÊ¼»¯ÏÂÒ»²½Ó¦¸Ã´¦ÀíµÃ±àÂëÖµ
                        availableCode = clearCode + 2;

                        // ½«±£´æµ½old_codeÖÐµÄÖµÇå³ý£¬ÒÔ±ãÖØÍ·ÔÙÀ´
                        oldCode = NullCode;
                        continue;
                    }

                    // ÏÂÃæÊÇcodeÊôÓÚÄÜÑ¹ËõµÄ±àÂë·¶Î§ÄÚµÄµÄ´¦Àí¹ý³Ì
                    if (oldCode == NullCode)
                    {
                        // Èç¹ûµ±Ç°±àÂëÖµÎª¿Õ,±íÊ¾ÊÇµÚÒ»´Î»ñÈ¡±àÂë
                        pixelStatck[top++] = suffix[code]; // »ñÈ¡µ½1¸öÊý¾ÝÁ÷µÄÊý¾Ý

                        // ±¾´Î±àÂë´¦ÀíÍê³É£¬½«±àÂëÖµ±£´æµ½old_codeÖÐ
                        oldCode = code;

                        // µÚÒ»¸ö×Ö·ûÎªµ±Ç°±àÂë
                        first = code;
                        continue;
                    }

                    int inCode = code; // ÔÚlzwÖÐ£¬Èç¹ûÈÏÊ¶ÁËÒ»¸ö±àÂëËù´ú±íµÄÊý¾Ýentry£¬Ôò½«±àÂë×÷ÎªÏÂÒ»´ÎµÄprefix£¬´Ë´¦inCode´ú±í´«µÝ¸øÏÂÒ»´Î×÷ÎªÇ°×ºµÄ±àÂëÖµ
                    if (code == availableCode)
                    {
                        // Èç¹ûµ±Ç°±àÂëºÍ±¾´ÎÓ¦¸ÃÉú³ÉµÄ±àÂëÏàÍ¬
                        // ÄÇÃ´ÏÂÒ»¸öÊý¾Ý×Ö½Ú¾ÍµÈÓÚµ±Ç°´¦Àí×Ö·û´®µÄµÚÒ»¸ö×Ö½Ú
                        pixelStatck[top++] = (byte)first;

                        code = oldCode; // »ØËÝµ½ÉÏÒ»¸ö±àÂë
                    }

                    while (code > clearCode)
                    {
                        // Èç¹ûµ±Ç°±àÂë´óÓÚÇå³ý±ê¼Ç£¬±íÊ¾±àÂëÖµÊÇÄÜÑ¹ËõÊý¾ÝµÄ
                        pixelStatck[top++] = suffix[code];
                        code = prefix[code]; // »ØËÝµ½ÉÏÒ»¸ö±àÂë
                    }

                    first = suffix[code];

                    // »ñÈ¡ÏÂÒ»¸öÊý¾Ý
                    pixelStatck[top++] = suffix[code];

                    // Fix for Gifs that have "deferred clear code" as per here :
                    // https://bugzilla.mozilla.org/show_bug.cgi?id=55918
                    if (availableCode < MaxStackSize)
                    {
                        // ÉèÖÃµ±Ç°Ó¦¸Ã±àÂëÎ»ÖÃµÄÇ°×º
                        prefix[availableCode] = oldCode;

                        // ÉèÖÃµ±Ç°Ó¦¸Ã±àÂëÎ»ÖÃµÄºó×º
                        suffix[availableCode] = first;

                        // ÏÂ´ÎÓ¦¸ÃµÃµ½µÄ±àÂëÖµ
                        availableCode++;
                        if (availableCode == codeMask + 1 && availableCode < MaxStackSize)
                        {
                            // Ôö¼Ó±àÂëÎ»Êý
                            codeSize++;

                            // ÖØÉè×î´ó±àÂëÖµ
                            codeMask = (1 << codeSize) - 1;
                        }
                    }

                    // »¹Ô­old_code
                    oldCode = inCode;
                }

                // »ØËÝµ½ÉÏÒ»¸ö´¦ÀíÎ»ÖÃ
                top--;

                // »ñÈ¡ÔªÊý¾Ý
                pixels[xyz++] = (byte)pixelStatck[top];
            }

            return pixels;
        }

        /// <summary>
        /// Reads the next data block from the stream. A data block begins with a byte,
        /// which defines the size of the block, followed by the block itself.
        /// </summary>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        private byte[] ReadBlock()
        {
            int blockSize = this.stream.ReadByte();
            return this.ReadBytes(blockSize);
        }

        /// <summary>
        /// Reads the specified number of bytes from the data stream.
        /// </summary>
        /// <param name="length">
        /// The number of bytes to read.
        /// </param>
        /// <returns>
        /// The <see cref="T:byte[]"/>.
        /// </returns>
        private byte[] ReadBytes(int length)
        {
            byte[] buffer = new byte[length];
            this.stream.Read(buffer, 0, length);
            return buffer;
        }
    }
}
