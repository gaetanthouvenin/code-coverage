// ------------------------------------------------------------------------------------------------
// <copyright file="EnumAsStringConvention.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace CodeCoverage.Infrastructure.Persistence.Conventions;

/// <summary>Stores every enumeration as text, unless a configuration says otherwise.</summary>
/// <remarks>
///   The name of the member is stored (<c>HasConversion&lt;string&gt;()</c>), which keeps the rows
///   readable and survives a reordering of the members. A column without an explicit length gets
///   <see cref="MaxLength" /> characters, so that it can be indexed on SQL Server (<c>Jobs.State</c>).
///   An explicit <c>HasConversion</c> or <c>HasMaxLength</c> in a configuration wins.
/// </remarks>
internal sealed class EnumAsStringConvention : IModelFinalizingConvention
{
  #region Fields

  /// <summary>The length of an enumeration column without an explicit length.</summary>
  public const int MaxLength = 64;

  #endregion

  #region Implementation of IModelFinalizingConvention

  /// <inheritdoc />
  public void ProcessModelFinalizing(
    IConventionModelBuilder modelBuilder,
    IConventionContext<IConventionModelBuilder> context)
  {
    ArgumentNullException.ThrowIfNull(modelBuilder);

    var enumProperties = modelBuilder.Metadata
                                     .GetEntityTypes()
                                     .SelectMany(static e => e.GetDeclaredProperties())
                                     .Where(static p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType).IsEnum)
                                     .ToList();

    foreach (var property in enumProperties)
    {
      if (property.GetValueConverter() is null && property.GetProviderClrType() is null)
      {
        property.Builder.HasConversion(typeof(string));
      }

      if (property.GetMaxLength() is null)
      {
        property.Builder.HasMaxLength(MaxLength);
      }
    }
  }

  #endregion
}
