// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteSetupCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Settings.Commands.CompleteSetup;

/// <summary>Ends the first start and forgets the setup token; authenticated by that token, in the handler.</summary>
/// <param name="SetupToken">The setup token, or <see langword="null" />.</param>
/// <param name="WebPublicUrl">The public address of the Web host, or <see langword="null" /> to keep the current one.</param>
/// <param name="ApiPublicUrl">The public address of the Api host, or <see langword="null" /> to keep the current one.</param>
/// <param name="AdminUserIds">The numeric identifiers of the administrators, separated by commas, or <see langword="null" /> to keep the current ones.</param>
public sealed record CompleteSetupCommand(string? SetupToken, string? WebPublicUrl, string? ApiPublicUrl, string? AdminUserIds) : ICommand, IAnonymousEntry
{
  #region Methods

  /// <summary>Returns the name only: the token never reaches a log or a trace.</summary>
  /// <returns>The name of the command.</returns>
  public override string ToString() => nameof(CompleteSetupCommand);

  #endregion
}
