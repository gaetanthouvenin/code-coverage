// ------------------------------------------------------------------------------------------------
// <copyright file="TestOptionsMonitor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Options;

namespace CodeCoverage.Web.Tests.Support;

// A monitor over one value a test can change, as the settings store does at runtime.
public sealed class TestOptionsMonitor<TOptions>(TOptions value) : IOptionsMonitor<TOptions>
{
  #region Properties

  public TOptions CurrentValue { get; } = value;

  #endregion

  #region Methods

  public TOptions Get(string? name) => CurrentValue;

  public IDisposable? OnChange(Action<TOptions, string?> listener) => null;

  #endregion
}
