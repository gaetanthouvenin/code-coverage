// ------------------------------------------------------------------------------------------------
// <copyright file="GeneralErrors.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Application.Exceptions.ErrorCodes;

/// <summary>The error codes shared by every feature.</summary>
public static class GeneralErrors
{
  #region Fields

  /// <summary>One or more validation rules have failed.</summary>
  public const string ValidationFailed = "General.ValidationFailed";

  /// <summary>An unexpected error occurred; its details are never returned to the client.</summary>
  public const string Unexpected = "General.Unexpected";

  #endregion
}
