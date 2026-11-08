// ------------------------------------------------------------------------------------------------
// <copyright file="UnmigratedRecord.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Infrastructure.Tests.Support;

// The entity of UnmigratedEntityModelCustomizer: no migration creates its table.
public sealed class UnmigratedRecord
{
  #region Properties

  public long Id { get; set; }

  public string Name { get; set; } = string.Empty;

  #endregion
}
