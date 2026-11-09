// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationOwnedExtensions.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Infrastructure.Persistence.Organizations;

/// <summary>Declares the tables that belong to an organization (ADR-013).</summary>
internal static class OrganizationOwnedExtensions
{
  #region Fields

  /// <summary>The name of the shadow column that holds the organization of a row.</summary>
  public const string ColumnName = "OrganizationId";

  /// <summary>The name of the query filter of the organization tables.</summary>
  public const string FilterName = "Organization";

  /// <summary>The annotation that marks an entity as owned by an organization.</summary>
  private const string OwnedAnnotation = "CodeCoverage:OrganizationOwned";

  #endregion

  #region Methods

  /// <summary>Adds the required shadow column of the organization and marks the entity for the query filter.</summary>
  /// <typeparam name="TEntity">The mapped entity.</typeparam>
  /// <param name="builder">The builder of the entity.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   <para>
  ///     The filter itself is installed by <see cref="CodeCoverageDbContext" />, once for all the marked
  ///     entities, and the table is listed in <see cref="TableClassification" />.
  ///   </para>
  ///   <para>
  ///     The column is a concurrency token so that every <c>UPDATE</c> and <c>DELETE</c> of a save filters on it
  ///     as well as on the primary key: an instance whose shadow column was written by hand can only reach a row
  ///     of the organization the save guard accepted (ADR-013).
  ///   </para>
  /// </remarks>
  public static EntityTypeBuilder<TEntity> IsOrganizationOwned<TEntity>(this EntityTypeBuilder<TEntity> builder)
    where TEntity : class
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.Property<long>(ColumnName).IsRequired().IsConcurrencyToken();
    builder.HasAnnotation(OwnedAnnotation, true);

    return builder;
  }

  /// <summary>Adds the foreign key from the shadow column to <c>Organizations</c>.</summary>
  /// <typeparam name="TEntity">The mapped entity, already declared by <see cref="IsOrganizationOwned{TEntity}" />.</typeparam>
  /// <param name="builder">The builder of the entity.</param>
  /// <returns>The same builder, for chaining.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
  /// <remarks>
  ///   Only for the tables whose unique index starts with <c>OrganizationId</c>: that index serves the
  ///   foreign key, and the convention then creates no other one. An organization that holds rows cannot be
  ///   deleted: a departure is a suspension (ADR-013).
  /// </remarks>
  public static EntityTypeBuilder<TEntity> HasOrganizationForeignKey<TEntity>(this EntityTypeBuilder<TEntity> builder)
    where TEntity : class
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.HasOne<Organization>().WithMany().HasForeignKey(ColumnName).OnDelete(DeleteBehavior.Restrict);

    return builder;
  }

  /// <summary>Tells whether an entity was declared by <see cref="IsOrganizationOwned{TEntity}" />.</summary>
  /// <param name="entityType">The entity of the model.</param>
  /// <returns><see langword="true" /> when the rows of the entity belong to an organization.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="entityType" /> is <see langword="null" />.</exception>
  public static bool IsMarkedOrganizationOwned(this IReadOnlyEntityType entityType)
  {
    ArgumentNullException.ThrowIfNull(entityType);

    return entityType.FindAnnotation(OwnedAnnotation) is not null;
  }

  #endregion
}
