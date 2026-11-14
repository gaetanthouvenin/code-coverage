// ------------------------------------------------------------------------------------------------
// <copyright file="RepositoryInaccessible.razor.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

using CodeCoverage.Web.Configuration.Authentication;
using CodeCoverage.Web.Resources;

namespace CodeCoverage.Web.Components.Repositories;

/// <summary>
///   The one state of an address that is unknown, unreadable, of another organization or of a forge the console does not
///   serve: the page never tells which (ADR-013).
/// </summary>
/// <remarks>Nobody signed in, it offers to sign in first, and to come back to the same address.</remarks>
public partial class RepositoryInaccessible
{
  #region Injected services

  [Inject]
  private IStringLocalizer<Common> L { get; set; } = null!;

  [Inject]
  private NavigationManager Navigation { get; set; } = null!;

  [Inject]
  private SessionState Session { get; set; } = null!;

  #endregion

  #region Properties

  /// <summary>Gets the address of the sign-in page, which brings the visitor back to this address.</summary>
  private string SignInHref => AuthenticationEndpoints.SignInUri(new Uri(Navigation.Uri).PathAndQuery);

  #endregion
}
