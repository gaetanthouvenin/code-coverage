// ------------------------------------------------------------------------------------------------
// <copyright file="SampleRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// An entity of SampleDbContext: one property per case of the conventions.
public sealed class SampleRecord
{
  #region Properties

  public long Id { get; set; }

  public SampleState State { get; set; }

  public SampleState? PreviousState { get; set; }

  public SampleState Shortened { get; set; }

  public SampleState Numeric { get; set; }

  public IReadOnlyList<string> Flags { get; set; } = [];

  #endregion
}
