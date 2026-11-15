// ------------------------------------------------------------------------------------------------
// <copyright file="ResolveSetupAdministratorsQuery.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Features.Settings.Queries.ResolveSetupAdministrators;

/// <summary>
///   Turns the administrators typed in the first-start wizard, logins or numeric identifiers, into identifiers;
///   authenticated by the setup token, in the handler.
/// </summary>
/// <param name="SetupToken">The setup token, or <see langword="null" />.</param>
/// <param name="AdminUserIds">The logins or numeric identifiers of the administrators, separated by commas.</param>
/// <remarks>The list has the name it has in <c>CompleteSetupCommand</c>, so a refusal shows under the same field.</remarks>
public sealed record ResolveSetupAdministratorsQuery(string? SetupToken, string? AdminUserIds) : IQuery<IReadOnlyList<SetupAdministrator>>, IAnonymousEntry
{
  #region Methods

  /// <summary>Returns the name only: the token never reaches a log or a trace.</summary>
  /// <returns>The name of the query.</returns>
  public override string ToString() => nameof(ResolveSetupAdministratorsQuery);

  #endregion
}
