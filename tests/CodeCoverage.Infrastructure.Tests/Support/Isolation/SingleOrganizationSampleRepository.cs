// ------------------------------------------------------------------------------------------------
// <copyright file="SingleOrganizationSampleRepository.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

namespace CodeCoverage.Infrastructure.Tests.Support.Isolation;

// Its test class exists, but holds only one organization.
public sealed class SingleOrganizationSampleRepository(SampleDbContext context)
{
  #region Methods

  public async Task<int> CountAsync(CancellationToken cancellationToken) =>
    await context.Set<SampleRecord>().CountAsync(cancellationToken).ConfigureAwait(false);

  #endregion
}
