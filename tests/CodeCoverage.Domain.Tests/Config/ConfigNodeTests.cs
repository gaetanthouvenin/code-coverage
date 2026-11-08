// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigNodeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

public sealed class ConfigNodeTests
{
  #region Methods

  [Fact]
  public void Find_ThroughScalar_ShouldBeNull()
  {
    Map(("a", Bool(true))).Find("a", "b").ShouldBeNull();
  }

  [Fact]
  public void Find_AbsentKey_ShouldDifferFromNullValue()
  {
    var map = Map(("a", ConfigNode.Null));

    map.Find("a").ShouldBe(ConfigNode.Null);
    map.Find("b").ShouldBeNull();
  }

  [Fact]
  public void FromMap_RepeatedKey_ShouldKeepThePlaceAndTakeTheLastValue()
  {
    var map = Map(("a", Number(1)), ("b", Number(2)), ("a", Number(3)));

    map.Entries.Select(entry => entry.Key).ShouldBe(["a", "b"]);
    map.Find("a")!.AsInteger().ShouldBe(3);
  }

  [Theory]
  [InlineData(1.5)]
  [InlineData(3000000000)]
  public void AsInteger_NotAnInteger_ShouldBeNull(decimal value)
  {
    Number(value).AsInteger().ShouldBeNull();
  }

  [Fact]
  public void AsReaders_OnOtherKind_ShouldBeNull()
  {
    var text = Text("a");

    text.AsBoolean().ShouldBeNull();
    text.AsNumber().ShouldBeNull();
    text.AsInteger().ShouldBeNull();
    Bool(true).AsText().ShouldBeNull();
  }

  #endregion
}
