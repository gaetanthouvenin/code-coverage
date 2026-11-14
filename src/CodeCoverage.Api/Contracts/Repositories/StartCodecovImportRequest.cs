// ------------------------------------------------------------------------------------------------
// <copyright file="StartCodecovImportRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api.Contracts.Repositories;

/// <summary>The API token of Codecov that reads the history of the repository on Codecov.</summary>
/// <param name="Token">The token, kept protected until the import ends, seven days at most.</param>
public sealed record StartCodecovImportRequest(string Token)
{
  #region Methods

  /// <summary>Describes the request without its token, for the logs.</summary>
  /// <returns>The name of the request.</returns>
  public override string ToString() => $"{nameof(StartCodecovImportRequest)} {{ }}";

  #endregion
}
