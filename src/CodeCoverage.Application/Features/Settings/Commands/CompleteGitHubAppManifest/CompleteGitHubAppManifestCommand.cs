// ------------------------------------------------------------------------------------------------
// <copyright file="CompleteGitHubAppManifestCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Settings.Commands.CompleteGitHubAppManifest;

/// <summary>Stores the GitHub App created from a manifest; authenticated by the setup token, in the handler.</summary>
/// <param name="SetupToken">The setup token, or <see langword="null" />.</param>
/// <param name="Code">The temporary code GitHub gave after the manifest was accepted.</param>
public sealed record CompleteGitHubAppManifestCommand(string? SetupToken, string Code) : ICommand, IAnonymousEntry
{
  #region Methods

  /// <summary>Returns the name only: neither the token nor the code reaches a log or a trace.</summary>
  /// <returns>The name of the command.</returns>
  public override string ToString() => nameof(CompleteGitHubAppManifestCommand);

  #endregion
}
