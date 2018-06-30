using System;
using System.Collections.Generic;

namespace SixLabors.Telemetry
{
    public static partial class Telemetry
    {
        private sealed class NullProvider : TelemetryProviderBase
        {
            public override void Event(Func<TelemetryDetails> details)
            {
                
            }

            public override void Exception(Exception ex, Func<TelemetryDetails> details)
            {
            }

            public override IDisposable Operation(Func<TelemetryDetails> details)
            {
                return null;
            }
        }
    }
}
