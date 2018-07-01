using System;
using System.Collections.Generic;

namespace SixLabors.Telemetry
{
    public static partial class Telemetry
    {
        private sealed class SafeWrappingProvider : TelemetryProviderBase
        {
            public TelemetryProviderBase InnerProvider { get; set; }

            public override void Event(Func<TelemetryDetails> details)
            {
                try
                {
                    InnerProvider.Event(details);
                }
                catch
                {
                    // swallow
                }
            }

            public override void Exception(Exception ex, Func<TelemetryDetails> details)
            {
                try
                {
                    InnerProvider.Exception(ex, details);
                }
                catch
                {
                    // swallow
                }
            }

            public override IDisposable Operation(Func<TelemetryDetails> details)
            {
                try
                {
                    return InnerProvider.Operation(details);
                }
                catch
                {
                    // swallow
                }

                return null;
            }
        }
    }
}
