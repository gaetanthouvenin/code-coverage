// ------------------------------------------------------------------------------------------------
// <copyright file="MethodBodyController.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Architecture.Tests.Samples.Presentation.Model;

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Controllers;

// Breaks the presentation rule inside a method body only: the signature names no Domain type.
internal sealed class MethodBodyController
{
  #region Methods

  public static string Get()
  {
    var entity = new SampleEntity { Name = "sample" };

    return entity.Name;
  }

  #endregion
}
