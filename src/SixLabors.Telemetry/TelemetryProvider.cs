using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace SixLabors.Telemetry
{
    internal sealed class TelemetryProvider : TelemetryProviderBase
    {
        private readonly TelemetryClient client;

        public TelemetryProvider()
        {
            var config = new TelemetryConfiguration("86a8b580-17ac-4648-be54-a962bcbf74a2");

            config
                .TelemetryProcessorChainBuilder.Use(p => new MachineMetricsProcessors(p))
                .Build();

            this.client = new Microsoft.ApplicationInsights.TelemetryClient(config);
#if !NETSTANDARD1_3
            AppDomain.CurrentDomain.ProcessExit += (s, e) => {
                this.client.Flush();
            };
#endif
        }

        public override void Event(Func<TelemetryDetails> detailsFunc)
        {
            var details = detailsFunc();
            this.client.TrackEvent(details.Operation, details.Properties, details.Metrics);
        }

        public override void Exception(Exception ex, Func<TelemetryDetails> detailsFunc)
        {
            var details = detailsFunc();
            this.client.TrackException(ex, details.Properties, details.Metrics);
        }

        public override IDisposable Operation(Func<TelemetryDetails> detailsFunc)
        {
            var details = detailsFunc();

            var operation = this.client.StartOperation<RequestTelemetry>(new Activity(details.Operation));
            if (details.Properties != null)
            {
                foreach (KeyValuePair<string, string> d in details.Properties)
                {
                    operation.Telemetry.Properties.Add(d.Key, d.Value);
                }
            }
            if (details.Metrics != null)
            {
                foreach (KeyValuePair<string, double> d in details.Metrics)
                {
                    operation.Telemetry.Metrics.Add(d.Key, d.Value);
                }
            }

            return Wrap(details, operation);
        }

        private ErrorCapturingTelemetry<TOperationTelemetry> Wrap<TOperationTelemetry>(TelemetryDetails details, IOperationHolder<TOperationTelemetry> telemetry)
            where TOperationTelemetry : OperationTelemetry
        {
            return new ErrorCapturingTelemetry<TOperationTelemetry>(this.client, details, telemetry);
        }

        private struct ErrorCapturingTelemetry<TOperationTelemetry> : IDisposable
            where TOperationTelemetry : OperationTelemetry
        {
            private readonly TelemetryClient client;
            private readonly TelemetryDetails details;
            private readonly IOperationHolder<TOperationTelemetry> telemetry;

            public ErrorCapturingTelemetry(TelemetryClient client, TelemetryDetails details, IOperationHolder<TOperationTelemetry> telemetry)
            {
                this.client = client;
                this.details = details;
                this.telemetry = telemetry;
            }

            public void Dispose()
            {
                if (Marshal.GetExceptionCode() != 0)
                {
                    this.telemetry.Telemetry.Success = false;
                }

                this.telemetry?.Dispose();
            }
        }
    }

    internal class MachineMetricsProcessors : ITelemetryProcessor
    {
        private readonly double hardwardVectors;
        private readonly ITelemetryProcessor next;

        public MachineMetricsProcessors(ITelemetryProcessor next)
        {
            this.hardwardVectors = System.Numerics.Vector.IsHardwareAccelerated ? 1 : 0;
            this.next = next;
        }

        public void Process(ITelemetry item)
        {
            if (item is ISupportMetrics hasMetrics)
            {
                hasMetrics.Metrics.Add("Hardware Vectors", hardwardVectors);
            }

            next?.Process(item);
        }
    }
}
