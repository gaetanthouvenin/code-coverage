// ------------------------------------------------------------------------------------------------
// <copyright file="ConfigMergerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Config;

using static CodeCoverage.Domain.Tests.Support.ConfigNodes;

namespace CodeCoverage.Domain.Tests.Config;

// The expected results follow merge_yamls of Codecov's libs/shared/shared/yaml/user_yaml.py.
public sealed class ConfigMergerTests
{
  #region Methods

  [Fact]
  public void Merge_ListShouldReplace()
  {
    var merged = ConfigMerger.Merge(Map(("ignore", Texts("a", "b"))), Map(("ignore", Texts("c"))));

    merged.Find("ignore")!.Items.Select(item => item.AsText()).ShouldBe(["c"]);
  }

  [Fact]
  public void Merge_MapShouldMerge()
  {
    var lower = Map(("coverage", Map(("precision", Number(2)), ("round", Text("down")))));
    var upper = Map(("coverage", Map(("round", Text("up")), ("range", Text("70..90")))));

    var merged = ConfigMerger.Merge(lower, upper);

    merged.Find("coverage", "precision")!.AsInteger().ShouldBe(2);
    merged.Find("coverage", "round")!.AsText().ShouldBe("up");
    merged.Find("coverage", "range")!.AsText().ShouldBe("70..90");
  }

  [Fact]
  public void Merge_ScalarOverMap_ShouldReplace()
  {
    var merged = ConfigMerger.Merge(Map(("comment", Map(("behavior", Text("default"))))), Map(("comment", Bool(false))));

    merged.Find("comment")!.AsBoolean().ShouldBe(false);
  }

  [Fact]
  public void Merge_MapOverScalar_ShouldReplace()
  {
    var merged = ConfigMerger.Merge(Map(("project", Bool(true))), Map(("project", Map(("backend", Bool(true))))));

    merged.Find("project", "backend")!.AsBoolean().ShouldBe(true);
  }

  [Fact]
  public void Merge_NullShouldReplace()
  {
    var merged = ConfigMerger.Merge(Map(("coverage", Map(("precision", Number(2))))), Map(("coverage", ConfigNode.Null)));

    merged.Find("coverage")!.Kind.ShouldBe(ConfigNodeKind.Null);
  }

  [Fact]
  public void Merge_ShouldKeepTheOrderOfTheLowerLayer()
  {
    var merged = ConfigMerger.Merge(
      Map(("a", Number(1)), ("b", Number(2))),
      Map(("c", Number(3)), ("a", Number(4))));

    merged.Entries.Select(entry => entry.Key).ShouldBe(["a", "b", "c"]);
    merged.Find("a")!.AsInteger().ShouldBe(4);
  }

  [Fact]
  public void Merge_NamedStatusOverDefaults_ShouldNotAddDefaultStatus()
  {
    var project = Map(("backend", Map(("target", Text("80%")))));
    var repository = Map(("coverage", Map(("status", Map(("project", project))))));

    var merged = ConfigMerger.Merge(CoverageConfigDefaults.Root, repository);

    merged.Find("coverage", "status", "project")!.Entries.Select(entry => entry.Key).ShouldBe(["backend"]);
    merged.Find("coverage", "status", "patch")!.AsBoolean().ShouldBe(true);
  }

  #endregion
}
