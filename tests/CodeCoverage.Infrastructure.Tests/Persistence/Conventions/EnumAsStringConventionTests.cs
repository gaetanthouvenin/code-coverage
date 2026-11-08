// ------------------------------------------------------------------------------------------------
// <copyright file="EnumAsStringConventionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Conventions;

public sealed class EnumAsStringConventionTests
{
  #region Methods

  [Theory]
  [InlineData(nameof(SampleRecord.State))]
  [InlineData(nameof(SampleRecord.PreviousState))]
  public void ProcessModelFinalizing_WithEnumProperty_ShouldStoreTheNameAsText(string propertyName)
  {
    var property = Property(propertyName);

    property.GetTypeMapping().Converter.ShouldNotBeNull().ProviderClrType.ShouldBe(typeof(string));
    property.GetMaxLength().ShouldBe(EnumAsStringConvention.MaxLength);
    property.GetRelationalTypeMapping().StoreType.ShouldBe($"nvarchar({EnumAsStringConvention.MaxLength})");
  }

  [Fact]
  public void ProcessModelFinalizing_WithExplicitLength_ShouldKeepTheLength()
  {
    var property = Property(nameof(SampleRecord.Shortened));

    property.GetTypeMapping().Converter.ShouldNotBeNull().ProviderClrType.ShouldBe(typeof(string));
    property.GetMaxLength().ShouldBe(16);
  }

  [Fact]
  public void ProcessModelFinalizing_WithExplicitConversion_ShouldKeepTheConversion()
  {
    var property = Property(nameof(SampleRecord.Numeric));

    property.GetTypeMapping().Converter.ShouldNotBeNull().ProviderClrType.ShouldBe(typeof(int));
  }

  [Fact]
  public void ProcessModelFinalizing_WithStoredValue_ShouldWriteTheMemberName()
  {
    var converter = Property(nameof(SampleRecord.State)).GetTypeMapping().Converter.ShouldNotBeNull();

    converter.ConvertToProvider(SampleState.Running).ShouldBe("Running");
    converter.ConvertFromProvider("Queued").ShouldBe(SampleState.Queued);
  }

  private static IProperty Property(string name)
  {
    using var context = SampleDbContext.Create();

    return context.Model.FindEntityType(typeof(SampleRecord)).ShouldNotBeNull().FindProperty(name).ShouldNotBeNull();
  }

  #endregion
}
