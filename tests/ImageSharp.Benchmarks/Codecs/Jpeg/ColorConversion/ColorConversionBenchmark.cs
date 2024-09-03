// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

public abstract class ColorConversionBenchmark
{
    private readonly int componentCount;

    public const int Count = 128;

    protected ColorConversionBenchmark(int componentCount)
        => this.componentCount = componentCount;

    protected Buffer2D<float>[] Input { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        this.Input = CreateRandomValues(this.componentCount, Count);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (Buffer2D<float> buffer in this.Input)
        {
            buffer.Dispose();
        }
    }

    private static Buffer2D<float>[] CreateRandomValues(
        int componentCount,
        int inputBufferLength,
        float minVal = 0f,
        float maxVal = 255f)
    {
        Random rnd = new Random(42);
        Buffer2D<float>[] buffers = new Buffer2D<float>[componentCount];
        for (int i = 0; i < componentCount; i++)
        {
            float[] values = new float[inputBufferLength];

            for (int j = 0; j < inputBufferLength; j++)
            {
                values[j] = ((float)rnd.NextDouble() * (maxVal - minVal)) + minVal;
            }

            // no need to dispose when buffer is not array owner
            buffers[i] = Configuration.Default.MemoryAllocator.Allocate2D<float>(values.Length, 1);
        }

        return buffers;
    }
}
