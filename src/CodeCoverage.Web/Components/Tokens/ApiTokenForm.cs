// ------------------------------------------------------------------------------------------------
// <copyright file="ApiTokenForm.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>What the dialog of a token of the API holds: the name of the properties is the one of the command.</summary>
/// <remarks>The refusals of the server land on these properties, so a message shows under the right field.</remarks>
public sealed class ApiTokenForm
{
  #region Properties

  /// <summary>Gets or sets the name given to the token.</summary>
  public string Name { get; set; } = string.Empty;

  /// <summary>Gets or sets the login of the organization the token is scoped to.</summary>
  public string Organization { get; set; } = string.Empty;

  #endregion
}
