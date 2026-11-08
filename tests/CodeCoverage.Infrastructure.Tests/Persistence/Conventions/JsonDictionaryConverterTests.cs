// ------------------------------------------------------------------------------------------------
// <copyright file="JsonDictionaryConverterTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Operations;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Conventions;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Persistence.Conventions;

public sealed class JsonDictionaryConverterTests
{
  #region Methods

  [Fact]
  public void ConvertToProvider_WithEntries_ShouldWriteAJsonObjectWithTheKeysAsGiven()
  {
    var converter = new JsonDictionaryConverter();

    converter.ConvertToProvider(new SortedDictionary<string, string>(StringComparer.Ordinal) { ["CommitId"] = "42", ["Reason"] = "manual" })
             .ShouldBe("""{"CommitId":"42","Reason":"manual"}""");
  }

  [Fact]
  public void ConvertFromProvider_WithJsonObject_ShouldReadTheEntries()
  {
    var converter = new JsonDictionaryConverter();

    converter.ConvertFromProvider("""{"b":"2","a":"1"}""")
             .ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>()
             .ShouldBe(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  [InlineData("null")]
  [InlineData("{}")]
  public void ConvertFromProvider_WithNoEntry_ShouldReadAnEmptyMap(string json)
  {
    var converter = new JsonDictionaryConverter();

    converter.ConvertFromProvider(json).ShouldBeAssignableTo<IReadOnlyDictionary<string, string>>().ShouldBeEmpty();
  }

  [Fact]
  public void Comparer_WithSameEntriesInAnotherOrder_ShouldBeEqual()
  {
    var comparer = JsonDictionaryConverter.Comparer;
    var left = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };
    var right = new Dictionary<string, string> { ["b"] = "2", ["a"] = "1" };

    comparer.Equals(left, right).ShouldBeTrue();
    comparer.GetHashCode(left).ShouldBe(comparer.GetHashCode(right));
  }

  [Fact]
  public void Comparer_WithOtherEntries_ShouldDiffer()
  {
    var comparer = JsonDictionaryConverter.Comparer;
    var reference = new Dictionary<string, string> { ["a"] = "1" };

    comparer.Equals(reference, new Dictionary<string, string> { ["a"] = "2" }).ShouldBeFalse();
    comparer.Equals(reference, new Dictionary<string, string> { ["b"] = "1" }).ShouldBeFalse();
    comparer.Equals(reference, new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }).ShouldBeFalse();
  }

  [Fact]
  public void Comparer_Snapshot_ShouldCopyTheEntries()
  {
    var map = new Dictionary<string, string> { ["a"] = "1" };

    var snapshot = JsonDictionaryConverter.Comparer.Snapshot(map);
    map["b"] = "2";

    snapshot.ShouldBe(new Dictionary<string, string> { ["a"] = "1" });
  }

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Model_AuditEntryDetails_ShouldUseTheJsonDictionaryConverter(DatabaseProvider provider)
  {
    using var context = DatabaseFixture.CreateContext(provider, "Server=localhost;Database=unused");

    var property = context.Model.FindEntityType(typeof(AuditEntry)).ShouldNotBeNull()
                          .FindProperty(nameof(AuditEntry.Details)).ShouldNotBeNull();

    property.GetTypeMapping().Converter.ShouldBeOfType<JsonDictionaryConverter>();
    property.GetValueComparer().ShouldBeSameAs(JsonDictionaryConverter.Comparer);
    property.IsNullable.ShouldBeFalse();
  }

  #endregion
}
