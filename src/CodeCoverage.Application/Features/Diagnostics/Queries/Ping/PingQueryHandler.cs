// ------------------------------------------------------------------------------------------------
// <copyright file="PingQueryHandler.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Mediator;

namespace CodeCoverage.Application.Features.Diagnostics.Queries.Ping;

/// <summary>Handler for the <see cref="PingQuery" /> query.</summary>
internal sealed class PingQueryHandler : IQueryHandler<PingQuery, string>
{
  #region Fields

  private static readonly string Version = ReadVersion();

  #endregion

  #region Methods

  // The informational version (set by GitVersion), else the assembly version.
  private static string ReadVersion()
  {
    var assembly = typeof(PingQueryHandler).Assembly;

    return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
           ?? assembly.GetName().Version?.ToString()
           ?? "0.0.0";
  }

  #endregion

  #region Implementation of IQueryHandler<PingQuery,string>

  /// <inheritdoc />
  public ValueTask<string> Handle(PingQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    return ValueTask.FromResult(Version);
  }

  #endregion
}
