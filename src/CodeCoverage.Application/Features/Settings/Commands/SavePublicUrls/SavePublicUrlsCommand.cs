// ------------------------------------------------------------------------------------------------
// <copyright file="SavePublicUrlsCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Settings.Commands.SavePublicUrls;

/// <summary>Stores the public addresses of the hosts; authenticated by the setup token, in the handler.</summary>
/// <param name="SetupToken">The setup token, or <see langword="null" />.</param>
/// <param name="WebPublicUrl">The public address of the console.</param>
/// <param name="ApiPublicUrl">The public address of the Api.</param>
/// <remarks>
///   The addresses are saved before the App exists, since the manifest of the App carries them.
///   <c>CompleteSetup</c> keeps its own rule: it still refuses to finish while the App is missing.
/// </remarks>
public sealed record SavePublicUrlsCommand(string? SetupToken, string WebPublicUrl, string ApiPublicUrl) : ICommand, IAnonymousEntry
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the token: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(SavePublicUrlsCommand)} {{ WebPublicUrl = {WebPublicUrl}, ApiPublicUrl = {ApiPublicUrl} }}";

  #endregion
}
