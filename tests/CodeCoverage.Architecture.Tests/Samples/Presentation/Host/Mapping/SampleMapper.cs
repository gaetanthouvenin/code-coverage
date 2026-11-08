// ------------------------------------------------------------------------------------------------
// <copyright file="SampleMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Architecture.Tests.Samples.Presentation.Model;

namespace CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Mapping;

// Allowed by the presentation rule: the mappers convert the Domain types.
internal static class SampleMapper
{
  #region Methods

  public static string ToResponse(SampleEntity entity) => entity.Name;

  #endregion
}
