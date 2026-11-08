// ------------------------------------------------------------------------------------------------
// <copyright file="CodeCoverageApi.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Api;

/// <summary>The constants of the REST API.</summary>
public static class CodeCoverageApi
{
  #region Fields

  /// <summary>The route prefix of every endpoint, without leading slash.</summary>
  public const string Route = "api/v1";

  /// <summary>The path prefix of every endpoint.</summary>
  public const string Prefix = "/" + Route;

  /// <summary>The OpenAPI tag.</summary>
  public const string Tag = "CodeCoverage";

  #endregion
}
