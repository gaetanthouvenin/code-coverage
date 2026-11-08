// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsRevisionRow.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>
///   The revision of the settings store: the single row of the <c>SettingsRevision</c> table,
///   incremented in the transaction of every save.
/// </summary>
/// <remarks>
///   A technical record of the Infrastructure. The hosts poll it to know that another host saved a
///   setting, without LISTEN/NOTIFY (ADR-006). The <c>AddSettings</c> migration inserts the row with
///   a revision of 0.
/// </remarks>
internal sealed class SettingsRevisionRow
{
  #region Fields

  /// <summary>The identifier of the single row.</summary>
  public const int SingletonId = 1;

  #endregion

  #region Properties

  /// <summary>Gets or sets the identifier: always <see cref="SingletonId" />.</summary>
  public int Id { get; set; }

  /// <summary>Gets or sets the revision, incremented by every save.</summary>
  public long Revision { get; set; }

  #endregion
}
