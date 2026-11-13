// ------------------------------------------------------------------------------------------------
// <copyright file="BadgeSnippetsViewModel.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Web.ViewModels.Repositories;

/// <summary>What a README needs to show the badge of a repository.</summary>
/// <param name="ImageUrl">The address of the badge image, with the token of a private repository.</param>
/// <param name="PageUrl">The address the badge links to: the overview of the repository.</param>
/// <param name="Markdown">The Markdown snippet.</param>
/// <param name="Html">The HTML snippet.</param>
/// <remarks>The addresses carry the badge token of a private repository: an instance never writes them down.</remarks>
public sealed record BadgeSnippetsViewModel(string ImageUrl, string PageUrl, string Markdown, string Html)
{
  #region Methods

  /// <inheritdoc />
  /// <remarks>Never the addresses: they carry the badge token.</remarks>
  public override string ToString() => nameof(BadgeSnippetsViewModel);

  #endregion
}
