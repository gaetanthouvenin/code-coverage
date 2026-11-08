// ------------------------------------------------------------------------------------------------
// <copyright file="GenericArgumentController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Architecture.Tests.Samples.Presentation.Model;

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Controllers;

// Breaks the presentation rule through a generic argument only, inside a method body.
internal sealed class GenericArgumentController
{
  #region Methods

  public static string Get() => SampleSender.Send<SampleEntity>();

  #endregion
}
