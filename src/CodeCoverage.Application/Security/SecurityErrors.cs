// ------------------------------------------------------------------------------------------------
// <copyright file="SecurityErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Security;

/// <summary>The error codes of the access checks.</summary>
public static class SecurityErrors
{
  #region Fields

  /// <summary>The current principal lacks the permission the message requires.</summary>
  public const string Forbidden = "Access.Forbidden";

  /// <summary>The message requires a signed-in user and nobody is signed in.</summary>
  public const string Unauthenticated = "Access.Unauthenticated";

  #endregion
}
