using System;
using System.Collections;
using System.Collections.Generic;

namespace SixLabors.Telemetry
{
    internal abstract class TelemetryProviderBase
    {
        /// <summary>
        /// A times operation that encompaces a portion of code that can be used to track a group of one or more events
        /// </summary>
        /// <param name="name">The name of the operation</param>
        /// <param name="paramaters"></param>
        /// <returns></returns>
        public abstract IDisposable Operation(Func<TelemetryDetails> details);

        public abstract void Event(Func<TelemetryDetails> details);

        public abstract void Exception(Exception ex, Func<TelemetryDetails> details);
    }

    internal struct TelemetryDetails
    {
        public string Operation { get; set; }

        public IDictionary<string, string> Properties { get; set; }

        public IDictionary<string, double> Metrics { get; set; }
    }
}
