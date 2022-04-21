// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using SixLabors.ImageSharp.Diagnostics;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public static class MemoryAllocatorValidator
    {
        private static bool _trackstacktraces = false;

        private static readonly AsyncLocal<TestMemoryDiagnostics> LocalInstance = new();

        public static bool MonitoringAllocations => LocalInstance.Value != null;

        static MemoryAllocatorValidator()
        {
            if (bool.TryParse(Environment.GetEnvironmentVariable("TESTING_TRACK_STACK_TRACES"), out var trackStackTraces))
            {
                _trackstacktraces = trackStackTraces;
            }

            MemoryDiagnostics.MemoryAllocated += MemoryDiagnostics_MemoryAllocated;
            MemoryDiagnostics.MemoryReleased += MemoryDiagnostics_MemoryReleased;
        }

        private static void MemoryDiagnostics_MemoryReleased()
        {
            TestMemoryDiagnostics backing = LocalInstance.Value;
            if (backing != null)
            {
                backing.TotalRemainingAllocated--;
            }
        }

        private static void MemoryDiagnostics_MemoryAllocated()
        {
            TestMemoryDiagnostics backing = LocalInstance.Value;
            if (backing != null)
            {
                backing.TotalAllocated++;
                backing.TotalRemainingAllocated++;
            }
        }

        public static TestMemoryDiagnostics MonitorAllocations()
        {
            var diag = new TestMemoryDiagnostics();
            LocalInstance.Value = diag;

            // if we have the debugger attached or we have the environment variable set then track stacktraces of allocations
            if ((Debugger.IsAttached || _trackstacktraces) && !MemoryDiagnostics.UndisposedAllocationSubscribed)
            {
                // noop - just needed to force the allocations to track stack traces
                MemoryDiagnostics.UndisposedAllocation += (t) => { };
            }

            return diag;
        }

        public static void StopMonitoringAllocations() => LocalInstance.Value = null;

        public static void ValidateAllocations(int expectedAllocationCount = 0)
            => LocalInstance.Value?.Validate(expectedAllocationCount);

        public class TestMemoryDiagnostics : IDisposable
        {
            public int TotalAllocated { get; set; }

            public int TotalRemainingAllocated { get; set; }

            public void Validate(int expectedAllocationCount)
            {
                var count = this.TotalRemainingAllocated;
                var pass = expectedAllocationCount == count;

                if (!pass)
                {
                    if (MemoryDiagnostics.UndisposedAllocationSubscribed)
                    {
                        var stackTraces = new List<string>();
                        void Callback(string trace)
                        {
                            while (trace.StartsWith("   at System.Environment.get_StackTrace") ||
                            trace.StartsWith("   at SixLabors.ImageSharp.Memory"))
                            {
                                trace = trace.Substring(trace.IndexOf(Environment.NewLine) + Environment.NewLine.Length);
                            }

                            // strip prefix to get 'real' trace
                            stackTraces.Add(trace);
                        }

                        MemoryDiagnostics.UndisposedAllocation += Callback;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        MemoryDiagnostics.UndisposedAllocation -= Callback;

                        if (stackTraces.Any())
                        {
                            throw new AllocationException(stackTraces);
                        }

                        Assert.Empty(stackTraces);
                        Assert.True(false, $"Expected {expectedAllocationCount} undisposed buffers but found {count}");
                    }
                    else
                    {
                        Assert.True(false, $"Expected {expectedAllocationCount} undisposed buffers but found {count}: ");
                    }
                }
            }

            public void Dispose()
            {
                this.Validate(0);
                if (LocalInstance.Value == this)
                {
                    StopMonitoringAllocations();
                }
            }
        }

        public class AllocationException : Exception
        {
            public AllocationException(List<string> stackTraces)
                : base($"Unexpected allocations")
            {
                this.StackTraces = stackTraces;
            }

            public List<string> StackTraces { get; }

            public override string StackTrace => this.StackTraces.FirstOrDefault();
        }
    }
}
