// ------------------------------------------------------------------------------------------------
// <copyright file="AccessDeniedReasons.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Configuration.Authentication;

/// <summary>The reasons the access-denied page explains, given by its <c>reason</c> query parameter.</summary>
public static class AccessDeniedReasons
{
  #region Fields

  /// <summary>The sign-in was refused: the account reaches no repository of an active organization.</summary>
  public const string NoRepository = "no-repository";

  #endregion
}
