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

  /// <summary>A save affected no row: the row is no longer there, or is not visible to the organization of the scope.</summary>
  public const string ConcurrentChange = "General.ConcurrentChange";

  /// <summary>One or more validation rules have failed.</summary>
  public const string ValidationFailed = "General.ValidationFailed";

  /// <summary>An unexpected error occurred; its details are never returned to the client.</summary>
  public const string Unexpected = "General.Unexpected";

  #endregion
}
