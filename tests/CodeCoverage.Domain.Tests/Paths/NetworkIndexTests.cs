// ------------------------------------------------------------------------------------------------
// <copyright file="NetworkIndexTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Paths;

namespace CodeCoverage.Domain.Tests.Paths;

public sealed class NetworkIndexTests
{
  #region Methods

  [Fact]
  public void Contains_ShouldBeCaseSensitive()
  {
    var index = new NetworkIndex(["src/A.cs"]);

    index.Contains("src/A.cs").ShouldBeTrue();
    index.Contains("src/a.cs").ShouldBeFalse();
  }

  [Fact]
  public void FindByFileName_ShouldIgnoreCase()
  {
    var index = new NetworkIndex(["src/A.cs", "lib/a.cs", "b.cs"]);

    index.FindByFileName("a.CS").ShouldBe(["src/A.cs", "lib/a.cs"], ignoreOrder: true);
  }

  [Fact]
  public void FindByFileName_RootFile_ShouldBeFound()
  {
    new NetworkIndex(["b.cs"]).FindByFileName("b.cs").ShouldBe(["b.cs"]);
  }

  [Fact]
  public void FindByFileName_UnknownName_ShouldBeEmpty()
  {
    new NetworkIndex(["b.cs"]).FindByFileName("c.cs").ShouldBeEmpty();
  }

  [Fact]
  public void IsEmpty_ShouldTellWhetherAFileIsListed()
  {
    new NetworkIndex([]).IsEmpty.ShouldBeTrue();
    new NetworkIndex(["b.cs"]).IsEmpty.ShouldBeFalse();
  }

  [Fact]
  public void Ctor_Null_ShouldThrow()
  {
    Should.Throw<ArgumentNullException>(() => new NetworkIndex(null!));
  }

  #endregion
}
