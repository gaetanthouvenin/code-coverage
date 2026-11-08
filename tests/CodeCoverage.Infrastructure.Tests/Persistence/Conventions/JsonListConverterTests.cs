// ------------------------------------------------------------------------------------------------
// <copyright file="JsonListConverterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Conventions;

public sealed class JsonListConverterTests
{
  #region Methods

  [Fact]
  public void ConvertToProvider_WithItems_ShouldWriteAJsonArray()
  {
    var converter = new JsonListConverter<string>();

    converter.ConvertToProvider(new[] { "unit", "integration" }).ShouldBe("""["unit","integration"]""");
  }

  [Fact]
  public void ConvertFromProvider_WithJsonArray_ShouldReadTheItemsInOrder()
  {
    var converter = new JsonListConverter<string>();

    converter.ConvertFromProvider("""["b","a"]""").ShouldBeAssignableTo<IReadOnlyList<string>>().ShouldBe(["b", "a"]);
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("null")]
  [InlineData("[]")]
  public void ConvertFromProvider_WithNoItem_ShouldReadAnEmptyList(string json)
  {
    var converter = new JsonListConverter<string>();

    converter.ConvertFromProvider(json).ShouldBeAssignableTo<IReadOnlyList<string>>().ShouldBeEmpty();
  }

  [Fact]
  public void Comparer_WithSameItemsInAnotherList_ShouldBeEqual()
  {
    var comparer = JsonListConverter<string>.Comparer;

    comparer.Equals(["a", "b"], new List<string> { "a", "b" }).ShouldBeTrue();
    comparer.GetHashCode(["a", "b"]).ShouldBe(comparer.GetHashCode(new List<string> { "a", "b" }));
  }

  [Fact]
  public void Comparer_WithOtherItemsOrOrder_ShouldDiffer()
  {
    var comparer = JsonListConverter<string>.Comparer;

    comparer.Equals(["a", "b"], ["b", "a"]).ShouldBeFalse();
    comparer.Equals(["a"], ["a", "b"]).ShouldBeFalse();
  }

  [Fact]
  public void Comparer_Snapshot_ShouldCopyTheItems()
  {
    var items = new List<string> { "a" };

    var snapshot = JsonListConverter<string>.Comparer.Snapshot(items);
    items.Add("b");

    snapshot.ShouldBe(["a"]);
  }

  [Fact]
  public void HasConversion_WithJsonListConverter_ShouldStoreText()
  {
    using var context = SampleDbContext.Create();

    var property = context.Model.FindEntityType(typeof(SampleRecord)).ShouldNotBeNull()
                          .FindProperty(nameof(SampleRecord.Flags)).ShouldNotBeNull();

    property.GetTypeMapping().Converter.ShouldBeOfType<JsonListConverter<string>>();
    property.GetValueComparer().ShouldBeSameAs(JsonListConverter<string>.Comparer);
  }

  #endregion
}
