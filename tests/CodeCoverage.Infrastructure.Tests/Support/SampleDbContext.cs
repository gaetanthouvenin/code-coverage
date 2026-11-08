// ------------------------------------------------------------------------------------------------
// <copyright file="SampleDbContext.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Infrastructure.Persistence.Conventions;

namespace CodeCoverage.Infrastructure.Tests.Support;

// A context with the conventions of CodeCoverageDbContext, and an entity to apply them to: the real
// model has no entity yet. Its model is built without a server.
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
  #region Properties

  public DbSet<SampleRecord> Records => Set<SampleRecord>();

  #endregion

  #region Methods

  public static SampleDbContext Create()
  {
    var options = new DbContextOptionsBuilder<SampleDbContext>().UseSqlServer("Server=localhost;Database=unused");

    return new SampleDbContext(options.Options);
  }

  protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
  {
    configurationBuilder.Conventions.Add(static _ => new EnumAsStringConvention());
  }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    var record = modelBuilder.Entity<SampleRecord>();

    record.Property(r => r.Shortened).HasMaxLength(16);
    record.Property(r => r.Numeric).HasConversion<int>();
    record.Property(r => r.Flags).HasConversion(new JsonListConverter<string>(), JsonListConverter<string>.Comparer);
  }

  #endregion
}
