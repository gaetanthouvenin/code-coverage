// ------------------------------------------------------------------------------------------------
// <copyright file="SampleComposition.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Architecture.Tests.Samples.Presentation.Model;

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Configuration;

// Allowed by the presentation rule: the composition root may name the Domain types.
internal static class SampleComposition
{
  #region Methods

  public static Type Register() => typeof(SampleEntity);

  #endregion
}
