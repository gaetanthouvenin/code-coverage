// ------------------------------------------------------------------------------------------------
// <copyright file="UploadConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Persistence.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Upload" /> to the <c>Uploads</c> table.</summary>
/// <remarks>
///   A table owned by an organization, with no foreign key to <c>Organizations</c> (ADR-013). The unique indexes
///   do not hold the organization: a commit has one, so the commit of a new row is checked by
///   <see cref="Repositories.UploadRepository" />, and a public identifier cannot be guessed.
/// </remarks>
internal sealed class UploadConfiguration : IEntityTypeConfiguration<Upload>
{
  #region Fields

  /// <summary>The unique index of the uploads of a commit by idempotency key.</summary>
  public const string IdempotencyIndexName = "UX_Uploads_CommitId_IdempotencyKey";

  /// <summary>The name of the <c>Uploads</c> table.</summary>
  private const string TableName = "Uploads";

  /// <summary>The unique index of the uploads by public identifier.</summary>
  private const string PublicIdIndexName = "UX_Uploads_PublicId";

  /// <summary>The index that counts the uploads of a commit by state.</summary>
  private const string StateIndexName = "IX_Uploads_CommitId_State";

  #endregion

  #region Implementation of IEntityTypeConfiguration<Upload>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Upload> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(TableName);
    builder.HasKey(static u => u.Id);
    builder.IsOrganizationOwned();

    builder.Property(static u => u.PublicId).IsRequired();
    builder.Property(static u => u.CommitId).IsRequired();
    builder.Property(static u => u.Sequence).IsRequired();
    builder.Property(static u => u.IdempotencyKey).IsRequired().HasMaxLength(Upload.IdempotencyKeyMaxLength).IsUnicode(false);
    builder.Property(static u => u.State).IsRequired();

    builder.Property(static u => u.Flags)
           .IsRequired()
           .HasConversion(new JsonListConverter<string>(), JsonListConverter<string>.Comparer);

    builder.Property(static u => u.Name).HasMaxLength(Upload.NameMaxLength);
    builder.Property(static u => u.CiService).HasMaxLength(Upload.CiServiceMaxLength);
    builder.Property(static u => u.BuildId).HasMaxLength(Upload.BuildIdMaxLength);
    builder.Property(static u => u.JobId).HasMaxLength(Upload.BuildIdMaxLength);
    builder.Property(static u => u.BuildUrl).HasMaxLength(Upload.BuildUrlMaxLength);
    builder.Property(static u => u.RepoRoot).HasMaxLength(Upload.RepoRootMaxLength);
    builder.Property(static u => u.UploaderVersion).HasMaxLength(Upload.UploaderVersionMaxLength);
    builder.Property(static u => u.TokenId);
    builder.Property(static u => u.PrincipalKind).IsRequired();
    builder.Property(static u => u.ReportFileCount).IsRequired();
    builder.Property(static u => u.ResolvedFileCount).IsRequired();
    builder.Property(static u => u.UnresolvedFileCount).IsRequired();
    builder.Property(static u => u.Lines).IsRequired();
    builder.Property(static u => u.Hits).IsRequired();
    builder.Property(static u => u.Misses).IsRequired();
    builder.Property(static u => u.Partials).IsRequired();
    builder.Property(static u => u.ErrorCode).HasMaxLength(Upload.ErrorCodeMaxLength);
    builder.Property(static u => u.Error);
    builder.Property(static u => u.ProcessingLog).IsRequired().HasConversion<ProcessingLogConverter>();
    builder.Property(static u => u.ReceivedAt).IsRequired();
    builder.Property(static u => u.ProcessedAt);
    builder.Property(static u => u.LockedBy).HasMaxLength(Upload.LockedByMaxLength);

    builder.HasIndex(static u => u.PublicId).IsUnique().HasDatabaseName(PublicIdIndexName);
    builder.HasIndex(static u => new { u.CommitId, u.IdempotencyKey }).IsUnique().HasDatabaseName(IdempotencyIndexName);
    builder.HasIndex(static u => new { u.CommitId, u.State }).HasDatabaseName(StateIndexName);

    builder.HasOne<Commit>().WithMany().HasForeignKey(static u => u.CommitId).OnDelete(DeleteBehavior.Restrict);
  }

  #endregion
}
