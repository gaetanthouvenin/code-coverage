// ------------------------------------------------------------------------------------------------
// <copyright file="AdmitAccountForm.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Organizations;

/// <summary>What the dialog of admission asks: the server validates it.</summary>
public sealed class AdmitAccountForm
{
  #region Properties

  /// <summary>Gets or sets the forge segment of the account.</summary>
  public string Forge { get; set; } = "github";

  /// <summary>Gets or sets the login of the account.</summary>
  public string? Login { get; set; }

  /// <summary>Gets or sets the identifier of the account on its forge.</summary>
  public string? ExternalAccountId { get; set; }

  #endregion
}
