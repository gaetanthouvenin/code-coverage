// ------------------------------------------------------------------------------------------------
// <copyright file="TokenForm.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.Components.Tokens;

/// <summary>What the create and import dialogs hold: the names of the properties are those of the commands.</summary>
/// <remarks>The refusals of the server land on these properties, so a message shows under the right field.</remarks>
public sealed class TokenForm
{
  #region Properties

  /// <summary>Gets or sets the name given to the token.</summary>
  public string Label { get; set; } = string.Empty;

  /// <summary>Gets or sets the value of the token to import, empty for a token that is generated.</summary>
  public string ImportedValue { get; set; } = string.Empty;

  #endregion

  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the imported value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(TokenForm)} {{ Label = {Label} }}";

  #endregion
}
