// ------------------------------------------------------------------------------------------------
// <copyright file="AuditEntryConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Infrastructure.Persistence.Conventions;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AuditEntry" /> to the <c>AuditEntries</c> table.</summary>
/// <remarks>
///   The key is a <c>bigint</c> identity (the model convention), which also orders the entries by
///   time. <c>Details</c> is a JSON object in a text column, never queried.
/// </remarks>
internal sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
  #region Implementation of IEntityTypeConfiguration<AuditEntry>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<AuditEntry> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("AuditEntries");
    builder.HasKey(static e => e.Id);

    builder.Property(static e => e.At).IsRequired();
    builder.Property(static e => e.Actor).IsRequired().HasMaxLength(AuditEntry.ActorMaxLength);
    builder.Property(static e => e.Action).IsRequired().HasMaxLength(AuditEntry.ActionMaxLength);
    builder.Property(static e => e.Target).IsRequired().HasMaxLength(AuditEntry.TargetMaxLength);
    builder.Property(static e => e.Details)
           .IsRequired()
           .HasConversion(new JsonDictionaryConverter(), JsonDictionaryConverter.Comparer);
  }

  #endregion
}
