// ------------------------------------------------------------------------------------------------
// <copyright file="IssuedTokenViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Tokens;

/// <summary>A token that was just created, with the value shown this one time.</summary>
/// <param name="Label">The name it was given.</param>
/// <param name="Value">Its value in clear, kept only while the dialog that shows it is open.</param>
public sealed record IssuedTokenViewModel(string Label, string Value)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the value: an instance can reach a log.</remarks>
  public override string ToString() => $"{nameof(IssuedTokenViewModel)} {{ Label = {Label} }}";

  #endregion
}
