// ------------------------------------------------------------------------------------------------
// <copyright file="UncoveredSampleRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

namespace CodeCoverage.Infrastructure.Tests.Support.Isolation;

// Reads the sample table inside an async method only, and no test class names it.
public sealed class UncoveredSampleRepository(SampleDbContext context)
{
  #region Methods

  public async Task<int> CountAsync(CancellationToken cancellationToken) =>
    await context.Set<SampleRecord>().CountAsync(cancellationToken).ConfigureAwait(false);

  #endregion
}
