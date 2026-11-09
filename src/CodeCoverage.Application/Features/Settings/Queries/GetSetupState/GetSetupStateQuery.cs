// ------------------------------------------------------------------------------------------------
// <copyright file="GetSetupStateQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Queries.GetSetupState;

/// <summary>Reads what the first-start wizard has done; authenticated by the setup token, in the handler.</summary>
/// <param name="SetupToken">The setup token, or <see langword="null" />.</param>
public sealed record GetSetupStateQuery(string? SetupToken) : IQuery<SetupState>, IAnonymousEntry
{
  #region Methods

  /// <summary>Returns the name only: the token never reaches a log or a trace.</summary>
  /// <returns>The name of the query.</returns>
  public override string ToString() => nameof(GetSetupStateQuery);

  #endregion
}
