using System;
using System.Collections.Generic;

namespace SixLabors.Telemetry
{
    public static partial class Telemetry
    {
        private struct SafeDisposable : IDisposable
        {
            private readonly IDisposable disposable;

            public SafeDisposable(IDisposable disposable)
            {
                this.disposable = disposable;
            }

            public void Dispose()
            {
                try
                {
                    this.disposable?.Dispose();
                }
                catch
                {
                    // this will explicitly supose to swollow to prevent telemetry code fro breaking the application
                }
            }
        }
    }
}
