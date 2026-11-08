// ------------------------------------------------------------------------------------------------
// <copyright file="CleanController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Controllers;

// Follows the presentation rule: it only handles its own contracts.
internal sealed class CleanController
{
  #region Methods

  public static string Get() => "sample";

  #endregion
}
