using System;
using System.Collections.Generic;

namespace SixLabors.Telemetry
{
    public static partial class Telemetry
    {
        private sealed class AggregateProvider : TelemetryProviderBase
        {
            private List<TelemetryProviderBase> providers = new List<TelemetryProviderBase>();

            public AggregateProvider(TelemetryProviderBase firstProvider, TelemetryProviderBase secondProvider)
            {
                this.RegisterProvider(firstProvider);
                this.RegisterProvider(secondProvider);
            }

            public override void Event(Func<TelemetryDetails> details)
            {
                // expand once other will be no-op
                var detailsExpanded = details();
                Func<TelemetryDetails> wrapped = () => detailsExpanded;
                foreach (var provider in this.providers)
                {
                    provider.Event(wrapped);
                }
            }

            public override void Exception(Exception ex, Func<TelemetryDetails> details)
            {
                // expand once other will be no-op
                var detailsExpanded = details();
                Func<TelemetryDetails> wrapped = () => detailsExpanded;
                foreach (var provider in this.providers)
                {
                    provider.Exception(ex, wrapped);
                }
            }

            public override IDisposable Operation(Func<TelemetryDetails> details)
            {
                // expand once other will be no-op
                var detailsExpanded = details();
                Func<TelemetryDetails> wrapped = () => detailsExpanded;
                var disposables = new IDisposable[this.providers.Count];
                for (var i = 0; i < disposables.Length; i++)
                {
                    disposables[i] = this.providers[i].Operation(wrapped);
                }

                return new AggregateDisposable(disposables);
            }

            public void RegisterProvider(TelemetryProviderBase provider)
            {
                if (!this.providers.Contains(provider))
                {
                    this.providers.Add(provider);
                }
            }

            private struct AggregateDisposable : IDisposable
            {
                private readonly IDisposable[] disposables;

                public AggregateDisposable(IDisposable[] disposables)
                {
                    this.disposables = disposables;
                }

                public void Dispose()
                {
                    foreach (var d in this.disposables)
                    {
                        d.Dispose();
                    }
                }
            }
        }
    }
}
