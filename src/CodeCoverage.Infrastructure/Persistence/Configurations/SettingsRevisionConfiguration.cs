// ------------------------------------------------------------------------------------------------
// <copyright file="SettingsRevisionConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Infrastructure.Settings;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="SettingsRevisionRow" /> to the <c>SettingsRevision</c> table.</summary>
/// <remarks>
///   A single row, seeded by the migration (<c>Id = 1</c>, <c>Revision = 0</c>): the identifier is
///   never generated, and the revision only changes through
///   <c>UPDATE SettingsRevision SET Revision = Revision + 1</c>.
/// </remarks>
internal sealed class SettingsRevisionConfiguration : IEntityTypeConfiguration<SettingsRevisionRow>
{
  #region Implementation of IEntityTypeConfiguration<SettingsRevisionRow>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<SettingsRevisionRow> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("SettingsRevision");
    builder.HasKey(static r => r.Id);

    builder.Property(static r => r.Id).ValueGeneratedNever();
    builder.Property(static r => r.Revision).IsRequired();

    builder.HasData(new SettingsRevisionRow { Id = SettingsRevisionRow.SingletonId, Revision = 0 });
  }

  #endregion
}
