// ------------------------------------------------------------------------------------------------
// <copyright file="UnmigratedEntityModelCustomizer.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CodeCoverage.Infrastructure.Tests.Support;

// Adds an entity that no migration knows to the model of CodeCoverageDbContext, as a configuration
// added without "dotnet ef migrations add" would.
public sealed class UnmigratedEntityModelCustomizer(ModelCustomizerDependencies dependencies)
  : ModelCustomizer(dependencies)
{
  #region Methods

  public override void Customize(ModelBuilder modelBuilder, DbContext context)
  {
    base.Customize(modelBuilder, context);

    modelBuilder.Entity<UnmigratedRecord>().Property(r => r.Name).HasMaxLength(32);
  }

  #endregion
}
