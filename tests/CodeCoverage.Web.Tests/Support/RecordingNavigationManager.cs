// ------------------------------------------------------------------------------------------------
// <copyright file="RecordingNavigationManager.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Components;

namespace CodeCoverage.Web.Tests.Support;

// A navigation outside any renderer: it records where it was asked to go.
public sealed class RecordingNavigationManager : NavigationManager
{
  #region Ctors

  public RecordingNavigationManager()
  {
    Initialize("http://localhost/", "http://localhost/");
  }

  #endregion

  #region Properties

  public bool ForcedLoad { get; private set; }

  #endregion

  #region Methods

  protected override void NavigateToCore(string uri, NavigationOptions options)
  {
    Uri = ToAbsoluteUri(uri).ToString();
    ForcedLoad = options.ForceLoad;
  }

  #endregion
}
