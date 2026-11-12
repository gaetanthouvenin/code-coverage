// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryInaccessible.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The one state of an address that is unknown, unreadable, of another organization or of a forge the console does not
///   serve: the page never tells which (ADR-013).
/// </summary>
public partial class RepositoryInaccessible
{
  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  #endregion
}
