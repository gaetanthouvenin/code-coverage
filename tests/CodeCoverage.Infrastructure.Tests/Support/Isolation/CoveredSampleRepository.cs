// ------------------------------------------------------------------------------------------------
// <copyright file="CoveredSampleRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

namespace CodeCoverage.Infrastructure.Tests.Support.Isolation;

// Reads the sample table, and a test class names it with two organizations.
public sealed class CoveredSampleRepository(SampleDbContext context)
{
  #region Methods

  public async Task<int> CountAsync(CancellationToken cancellationToken) =>
    await context.Set<SampleRecord>().CountAsync(cancellationToken).ConfigureAwait(false);

  #endregion
}
