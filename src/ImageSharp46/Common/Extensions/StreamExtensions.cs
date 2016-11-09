// <copyright file="StreamExtensions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp
{
    using System.IO;

    internal static class StreamExtensions
    {
        public static void Skip(this Stream stream, int count)
        {
            if (count < 1)
            {
                return;
            }

            if (stream.CanSeek)
            {
                stream.Position += count;
            }
            else
            {
                byte[] foo = new byte[count];
                stream.Read(foo, 0, count);
            }
        }
    }
}
