// ------------------------------------------------------------------------------------------------
// <copyright file="AppUserConfiguration.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Users;

namespace CodeCoverage.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="AppUser" /> to the <c>Users</c> table.</summary>
/// <remarks>The table is global and holds no business data. The token columns only ever hold protected values.</remarks>
internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
  #region Fields

  /// <summary>The unique index of the users by forge account.</summary>
  public const string AccountIndexName = "UX_Users_Forge_ExternalId";

  #endregion

  #region Implementation of IEntityTypeConfiguration<AppUser>

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<AppUser> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("Users");
    builder.HasKey(static u => u.Id);

    builder.Property(static u => u.Forge).IsRequired();
    builder.Property(static u => u.ExternalId).IsRequired().HasMaxLength(AppUser.ExternalIdMaxLength);
    builder.Property(static u => u.Login).IsRequired().HasMaxLength(AppUser.LoginMaxLength);
    builder.Property(static u => u.AvatarUrl).HasMaxLength(AppUser.AvatarUrlMaxLength);
    builder.Property(static u => u.AccessTokenProtected);
    builder.Property(static u => u.RefreshTokenProtected);
    builder.Property(static u => u.AccessTokenExpiresAt);
    builder.Property(static u => u.RefreshTokenExpiresAt);
    builder.Property(static u => u.IsAdmin).IsRequired();
    builder.Property(static u => u.LastLoginAt);
    builder.Property(static u => u.Version).IsRequired().IsConcurrencyToken();

    builder.HasIndex(static u => new { u.Forge, u.ExternalId }).IsUnique().HasDatabaseName(AccountIndexName);
  }

  #endregion
}
