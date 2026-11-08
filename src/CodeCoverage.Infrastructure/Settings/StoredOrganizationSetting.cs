// ------------------------------------------------------------------------------------------------
// <copyright file="StoredOrganizationSetting.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Settings;

/// <summary>A value an organization has saved for one setting: one row of the <c>OrganizationSettings</c> table.</summary>
/// <remarks>
///   A technical record of the Infrastructure. The organization of the row is the <c>OrganizationId</c> shadow
///   column, and the absence of a row means the default of the catalog.
/// </remarks>
internal sealed class StoredOrganizationSetting
{
  #region Properties

  /// <summary>Gets or sets the identifier of the row.</summary>
  public long Id { get; set; }

  /// <summary>Gets or sets the configuration key, as <c>Section:Name</c>.</summary>
  public required string Key { get; set; }

  /// <summary>Gets or sets the value, written with the invariant culture.</summary>
  public required string Value { get; set; }

  /// <summary>Gets or sets when the value was last changed, in UTC.</summary>
  public DateTimeOffset UpdatedAt { get; set; }

  /// <summary>Gets or sets who last changed the value (a login, or the system).</summary>
  public required string UpdatedBy { get; set; }

  #endregion
}
