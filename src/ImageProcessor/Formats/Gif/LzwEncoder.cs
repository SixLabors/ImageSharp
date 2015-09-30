// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LzwEncoder.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Encodes an image pixels used on a method based on LZW compression.
//   <see href="http://matthewflickinger.com/lab/whatsinagif/lzw_image_data.asp"/>
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Formats
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Encodes an image pixels used on a method based on LZW compression.
    /// <see href="http://matthewflickinger.com/lab/whatsinagif/lzw_image_data.asp"/>
    /// </summary>
    internal class LzwEncoder
    {
        /// <summary>
        /// One more than the maximum value 12 bit integer.
        /// </summary>
        private const int MaxStackSize = 4096;

        /// <summary>
        /// The initial bit depth.
        /// </summary>
        private readonly byte initDataSize;

        /// <summary>
        /// The indexed pixels to encode.
        /// </summary>
        private readonly byte[] indexedPixels;

        /// <summary>
        /// The color depth in bits.
        /// </summary>
        private byte colorDepth;

        /// <summary>
        /// Initializes a new instance of the <see cref="LzwEncoder"/> class.
        /// </summary>
        /// <param name="indexedPixels">The array of indexed pixels.</param>
        /// <param name="colorDepth">The color depth in bits.</param>
        public LzwEncoder(byte[] indexedPixels, byte colorDepth)
        {
            this.indexedPixels = indexedPixels;
            this.colorDepth = colorDepth.Clamp<byte>(2, 8);
            this.initDataSize = this.colorDepth;
        }

        public void Encode(Stream stream)
        {
            // Whether it is a first step.
            bool first = true;

            // The initial suffix.
            int suffix = 0;

            // Indicator to reinitialize the code table.
            int clearCode = 1 << this.colorDepth;

            // End of information code
            int endOfInformation = clearCode + 1;

            // The code table for storing encoded colors.
            Dictionary<string, int> codeTable = new Dictionary<string, int>();

            // The current number of index bytes processed.
            int releaseCount = 0;

            // Calculate the code available.
            byte codeSize = (byte)(this.colorDepth + 1);
            int availableCode = endOfInformation + 1;

            // Initialise.
            BitEncoder bitEncoder = new BitEncoder(codeSize);
            stream.WriteByte(this.colorDepth);
            bitEncoder.Add(clearCode);

            while (releaseCount < this.indexedPixels.Length)
            {
                if (first)
                {
                    // If this is the first byte the suffix is set to the first byte index.
                    suffix = this.indexedPixels[releaseCount++];

                    if (releaseCount == this.indexedPixels.Length)
                    {
                        bitEncoder.Add(suffix);
                        bitEncoder.Add(endOfInformation);
                        bitEncoder.End();
                        stream.WriteByte((byte)bitEncoder.Length);
                        stream.Write(bitEncoder.ToArray(), 0, bitEncoder.Length);
                        bitEncoder.Clear();
                        break;
                    }

                    first = false;
                    continue;
                }

                // Switch
                int prefix = suffix;

                // Read the bytes at the index.
                suffix = this.indexedPixels[releaseCount++];

                // Acts as a key for code table entries.
                string key = $"{prefix},{suffix}";

                // Is index buffer + the index in our code table?
                if (!codeTable.ContainsKey(key))
                {
                    // If the current entity is not coded add the prefix.
                    bitEncoder.Add(prefix);

                    // Add the current bytes
                    codeTable.Add(key, availableCode++);

                    if (availableCode > (MaxStackSize - 3))
                    {
                        // Clear out and reset the wheel.
                        codeTable.Clear();
                        this.colorDepth = this.initDataSize;
                        codeSize = (byte)(this.colorDepth + 1);
                        availableCode = endOfInformation + 1;

                        bitEncoder.Add(clearCode);
                        bitEncoder.IntitialBit = codeSize;
                    }
                    else if (availableCode > (1 << codeSize))
                    {
                        // If the currently available coding is greater than the current value.
                        // the coded bits can represent.
                        this.colorDepth++;
                        codeSize = (byte)(this.colorDepth + 1);
                        bitEncoder.IntitialBit = codeSize;
                    }

                    if (bitEncoder.Length >= 255)
                    {
                        stream.WriteByte(255);
                        stream.Write(bitEncoder.ToArray(), 0, 255);
                        if (bitEncoder.Length > 255)
                        {
                            byte[] leftBuffer = new byte[bitEncoder.Length - 255];
                            bitEncoder.CopyTo(255, leftBuffer, 0, leftBuffer.Length);
                            bitEncoder.Clear();
                            bitEncoder.AddRange(leftBuffer);
                        }
                        else
                        {
                            bitEncoder.Clear();
                        }
                    }
                }
                else
                {
                    // Set the suffix to the current byte.
                    suffix = codeTable[key];
                }

                // Output code for contents of index buffer.
                // Output end-of-information code.
                if (releaseCount == this.indexedPixels.Length)
                {
                    bitEncoder.Add(suffix);
                    bitEncoder.Add(endOfInformation);
                    bitEncoder.End();
                    if (bitEncoder.Length > 255)
                    {
                        byte[] leftBuffer = new byte[bitEncoder.Length - 255];
                        bitEncoder.CopyTo(255, leftBuffer, 0, leftBuffer.Length);
                        bitEncoder.Clear();
                        bitEncoder.AddRange(leftBuffer);
                        stream.WriteByte((byte)leftBuffer.Length);
                        stream.Write(leftBuffer, 0, leftBuffer.Length);
                    }
                    else
                    {
                        stream.WriteByte((byte)bitEncoder.Length);
                        stream.Write(bitEncoder.ToArray(), 0, bitEncoder.Length);
                        bitEncoder.Clear();
                    }

                    break;
                }
            }
        }
    }
}
