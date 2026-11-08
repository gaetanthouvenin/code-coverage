// ------------------------------------------------------------------------------------------------
// <copyright file="TestMeterFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.Metrics;

namespace CodeCoverage.Infrastructure.Tests.Support;

// A meter factory of its own for each test: its meters carry it as their scope, so that a listener
// only sees the measurements of its test, even when the tests run in parallel.
public sealed class TestMeterFactory : IMeterFactory
{
  #region Fields

  private readonly List<Meter> _meters = [];

  #endregion

  #region Methods

  public Meter Create(MeterOptions options)
  {
    ArgumentNullException.ThrowIfNull(options);

    var meter = new Meter(new MeterOptions(options.Name) { Version = options.Version, Tags = options.Tags, Scope = this });

    lock (_meters)
    {
      _meters.Add(meter);
    }

    return meter;
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose()
  {
    lock (_meters)
    {
      foreach (var meter in _meters)
      {
        meter.Dispose();
      }

      _meters.Clear();
    }
  }

  #endregion
}
