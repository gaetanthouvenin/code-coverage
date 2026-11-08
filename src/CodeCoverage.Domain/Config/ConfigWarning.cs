// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigWarning.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Config;

/// <summary>Something in a configuration that was not applied as written.</summary>
/// <param name="Code">The reason, in the <c>Feature.Reason</c> form.</param>
/// <param name="Key">Where it is, such as <c>coverage.range</c> or <c>ignore[2]</c>.</param>
/// <param name="Detail">The offending text, if there is one.</param>
public sealed record ConfigWarning(string Code, string Key, string? Detail = null)
{
  #region Fields

  /// <summary>The value does not have the expected form: the default applies instead.</summary>
  public const string InvalidValue = "Config.InvalidValue";

  /// <summary>The pattern cannot run without backtracking: it stays in its set and matches nothing.</summary>
  public const string UnsupportedPattern = "Config.UnsupportedPattern";

  /// <summary>The value is valid but exceeds what is supported: the closest supported value applies.</summary>
  public const string ClampedValue = "Config.ClampedValue";

  #endregion
}
