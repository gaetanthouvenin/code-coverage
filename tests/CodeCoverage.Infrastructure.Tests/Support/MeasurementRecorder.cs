// ------------------------------------------------------------------------------------------------
// <copyright file="MeasurementRecorder.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Records, through a MeterListener, the measurements of the meters created by one TestMeterFactory:
// each measurement is kept as its value and its tags, under the name of its instrument.
public sealed class MeasurementRecorder : IDisposable
{
  #region Fields

  private readonly MeterListener _listener = new();

  private readonly ConcurrentQueue<(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags)> _measurements = new();

  #endregion

  #region Ctors

  public MeasurementRecorder(TestMeterFactory scope)
  {
    ArgumentNullException.ThrowIfNull(scope);

    _listener.InstrumentPublished = (instrument, listener) =>
    {
      if (ReferenceEquals(instrument.Meter.Scope, scope))
      {
        listener.EnableMeasurementEvents(instrument);
      }
    };
    _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument, value, tags));
    _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument, value, tags));
    _listener.Start();
  }

  #endregion

  #region Methods

  public void Collect() => _listener.RecordObservableInstruments();

  public IReadOnlyList<(double Value, IReadOnlyDictionary<string, object?> Tags)> Of(string instrument) =>
    _measurements.Where(m => m.Instrument == instrument).Select(static m => (m.Value, m.Tags)).ToList();

  public double Sum(string instrument) => Of(instrument).Sum(static m => m.Value);

  private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
  {
    var copy = new Dictionary<string, object?>(StringComparer.Ordinal);

    foreach (var tag in tags)
    {
      copy[tag.Key] = tag.Value;
    }

    _measurements.Enqueue((instrument.Name, value, copy));
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _listener.Dispose();

  #endregion
}
