// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using Microsoft.DotNet.RemoteExecutor;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Tests.Memory.Allocators;

public class SharedArrayPoolBufferTests
{
    [Fact]
    public void AllocatesArrayPoolArray()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            using (SharedArrayPoolBuffer<byte> buffer = new(900))
            {
                Assert.Equal(900, buffer.GetSpan().Length);
                buffer.GetSpan().Fill(42);
            }

            byte[] array = ArrayPool<byte>.Shared.Rent(900);
            byte[] expected = Enumerable.Repeat((byte)42, 900).ToArray();

            Assert.True(expected.AsSpan().SequenceEqual(array.AsSpan(0, 900)));
        }
    }

    [Fact]
    public void OutstandingReferences_RetainArrays()
    {
        RemoteExecutor.Invoke(RunTest).Dispose();

        static void RunTest()
        {
            SharedArrayPoolBuffer<byte> buffer = new(900);
            Span<byte> span = buffer.GetSpan();

            buffer.AddRef();
            ((IDisposable)buffer).Dispose();
            span.Fill(42);
            byte[] array = ArrayPool<byte>.Shared.Rent(900);
            Assert.NotEqual(42, array[0]);
            ArrayPool<byte>.Shared.Return(array);

            buffer.ReleaseRef();
            array = ArrayPool<byte>.Shared.Rent(900);
            byte[] expected = Enumerable.Repeat((byte)42, 900).ToArray();
            Assert.True(expected.AsSpan().SequenceEqual(array.AsSpan(0, 900)));
            ArrayPool<byte>.Shared.Return(array);
        }
    }
}
