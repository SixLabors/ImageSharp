// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using SixLabors.ImageSharp.Formats;

namespace SixLabors.ImageSharp.Tests.Formats.Heif;

/// <summary>
/// Temporary mutation fuzz of the AV1 payloads in the AVIF corpus.
/// </summary>
[Trait("Format", "HeifFuzz")]
public class HeifFuzzScratch
{
    /// <summary>
    /// Decodes seeded mutants until the time budget ends and writes every unexpected failure to the output directory.
    /// </summary>
    [Fact]
    public void FuzzAv1Payloads()
    {
        string? outDir = Environment.GetEnvironmentVariable("FUZZ_OUT");
        if (outDir is null)
        {
            return;
        }

        int baseSeed = int.Parse(Environment.GetEnvironmentVariable("FUZZ_SEED") ?? "0");
        int seconds = int.Parse(Environment.GetEnvironmentVariable("FUZZ_SECONDS") ?? "300");
        string? only = Environment.GetEnvironmentVariable("FUZZ_ONLY");
        Directory.CreateDirectory(outDir);

        string[] files = Directory.GetFiles(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, "Heif"), "*.avif");
        Array.Sort(files, StringComparer.Ordinal);
        byte[][] data = files.Select(File.ReadAllBytes).ToArray();
        (int Start, int End)[][] regions = data.Select(FindRegions).ToArray();

        DecoderOptions options = new() { SegmentIntegrityHandling = SegmentIntegrityHandling.Strict };

        int workers = Math.Max(1, Environment.ProcessorCount - 2);
        long next = 0;
        long total = 0;
        long failures = 0;
        long slow = 0;
        ConcurrentDictionary<string, int> exceptionCounts = new();
        Stopwatch budget = Stopwatch.StartNew();
        string[] current = new string[workers];
        long[] startTicks = new long[workers];
        object logLock = new();
        string logPath = Path.Combine(outDir, "findings.txt");

        void Log(string text)
        {
            lock (logLock)
            {
                File.AppendAllText(logPath, text + Environment.NewLine);
            }
        }

        Thread[] threads = new Thread[workers];
        for (int w = 0; w < workers; w++)
        {
            int worker = w;
            threads[w] = new Thread(() =>
            {
                while (budget.Elapsed.TotalSeconds < seconds)
                {
                    int seed;
                    if (only is not null)
                    {
                        if (Interlocked.Increment(ref next) > 1)
                        {
                            return;
                        }

                        seed = int.Parse(only);
                    }
                    else
                    {
                        seed = unchecked(baseSeed + (int)Interlocked.Increment(ref next));
                    }

                    byte[] mutant = Mutate(seed, data, regions, out string description);
                    current[worker] = description;
                    File.WriteAllText(Path.Combine(outDir, $"current{worker}.txt"), description);
                    Volatile.Write(ref startTicks[worker], Stopwatch.GetTimestamp());
                    Stopwatch watch = Stopwatch.StartNew();
                    try
                    {
                        using Image image = Image.Load(options, mutant);
                    }
                    catch (Exception ex) when (ex is ImageFormatException or NotSupportedException)
                    {
                        exceptionCounts.AddOrUpdate(ex.GetType().Name, 1, (_, c) => c + 1);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failures);
                        exceptionCounts.AddOrUpdate("UNEXPECTED " + ex.GetType().Name, 1, (_, c) => c + 1);
                        Log($"FAIL {description}{Environment.NewLine}{ex}{Environment.NewLine}");
                    }

                    watch.Stop();
                    Volatile.Write(ref startTicks[worker], 0);
                    if (watch.Elapsed.TotalSeconds > 5)
                    {
                        Interlocked.Increment(ref slow);
                        Log($"SLOW {watch.Elapsed.TotalSeconds:F1}s {description}");
                    }

                    Interlocked.Increment(ref total);
                }
            });
            threads[w].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }

        StringBuilder summary = new();
        summary.AppendLine($"seed {baseSeed} total {total} failures {failures} slow {slow} elapsed {budget.Elapsed}");
        foreach (KeyValuePair<string, int> pair in exceptionCounts.OrderBy(p => p.Key))
        {
            summary.AppendLine($"  {pair.Key}: {pair.Value}");
        }

        File.AppendAllText(Path.Combine(outDir, "summary.txt"), summary.ToString());
    }

    private static byte[] Mutate(int seed, byte[][] data, (int Start, int End)[][] regions, out string description)
    {
        Random random = new(seed);
        int fileIndex = random.Next(data.Length);
        byte[] source = data[fileIndex];
        (int Start, int End)[] fileRegions = regions[fileIndex];
        (int start, int end) = fileRegions[random.Next(fileRegions.Length)];
        int length = end - start;
        byte[] mutant = (byte[])source.Clone();
        StringBuilder text = new($"seed={seed} file={fileIndex} region={start}-{end}");
        int kind = random.Next(10);

        int Position()
        {
            // Headers and the first tile bytes live at the start of each payload, so half the edits target its first bytes.
            return random.Next(2) == 0 ? start + random.Next(Math.Min(length, 64)) : start + random.Next(length);
        }

        if (kind < 4)
        {
            int flips = 1 + random.Next(6);
            for (int i = 0; i < flips; i++)
            {
                int p = Position();
                int bit = random.Next(8);
                mutant[p] ^= (byte)(1 << bit);
                text.Append($" flip {p}:{bit}");
            }
        }
        else if (kind < 8)
        {
            int writes = 1 + random.Next(4);
            byte[] special = [0x00, 0xFF, 0x7F, 0x80, 0x01, 0xFE];
            for (int i = 0; i < writes; i++)
            {
                int p = Position();
                byte value = random.Next(2) == 0 ? special[random.Next(special.Length)] : (byte)random.Next(256);
                mutant[p] = value;
                text.Append($" set {p}={value}");
            }
        }
        else if (kind == 8)
        {
            int p = start + random.Next(length);
            Array.Clear(mutant, p, end - p);
            text.Append($" zero-tail {p}");
        }
        else
        {
            int p = start + random.Next(length);
            Array.Resize(ref mutant, p);
            text.Append($" truncate {p}");
        }

        description = text.ToString();
        return mutant;
    }

    private static (int Start, int End)[] FindRegions(byte[] data)
    {
        List<(int Start, int End)> result = [];
        int offset = 0;
        while (offset + 8 <= data.Length)
        {
            long size = (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
            string type = Encoding.ASCII.GetString(data, offset + 4, 4);
            int header = 8;
            if (size == 1)
            {
                size = (long)BitConverter.ToUInt64(data.AsSpan(offset + 8, 8).ToArray().Reverse().ToArray());
                header = 16;
            }
            else if (size == 0)
            {
                size = data.Length - offset;
            }

            if (size < header || offset + size > data.Length)
            {
                break;
            }

            if (type == "mdat" && size > header)
            {
                result.Add((offset + header, (int)(offset + size)));
            }

            offset += (int)size;
        }

        // The av1C property holds a copy of the sequence header OBU.
        for (int i = 0; i + 8 <= data.Length; i++)
        {
            if (data[i] == (byte)'a' && data[i + 1] == (byte)'v' && data[i + 2] == (byte)'1' && data[i + 3] == (byte)'C')
            {
                int size = (data[i - 4] << 24) | (data[i - 3] << 16) | (data[i - 2] << 8) | data[i - 1];
                if (size > 12)
                {
                    result.Add((i + 8, i - 4 + size));
                }
            }
        }

        return result.ToArray();
    }
}
