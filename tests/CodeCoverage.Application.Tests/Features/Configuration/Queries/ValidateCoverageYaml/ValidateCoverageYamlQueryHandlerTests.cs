// ------------------------------------------------------------------------------------------------
// <copyright file="ValidateCoverageYamlQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Configuration.Queries.ValidateCoverageYaml;
using CodeCoverage.Domain.Config;
using CodeCoverage.Domain.Contracts.Config;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Configuration.Queries.ValidateCoverageYaml;

public sealed class ValidateCoverageYamlQueryHandlerTests
{
  #region Fields

  private const string Yaml = "comment: false";

  private static readonly ConfigNode Root = ConfigNode.FromMap(("comment", ConfigNode.FromBoolean(false)));

  private readonly ICoverageConfigParser _parser = A.Fake<ICoverageConfigParser>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_UnknownKey_ShouldWarn()
  {
    var warning = new ConfigWarning("Config.UnknownKey", "nope");
    Parses(Root, warning);

    var result = await HandleAsync();

    result.IsValid.ShouldBeTrue();
    result.Warnings.ShouldBe([warning]);
    result.Errors.ShouldBeEmpty();
  }

  [Fact]
  public async Task Handle_ShouldNeverMergeOrganizationYaml()
  {
    Parses(Root);

    var result = await HandleAsync();

    // The organization YAML comes from the settings: the handler has no way to reach them.
    typeof(ValidateCoverageYamlQueryHandler).GetConstructors()
                                            .Single()
                                            .GetParameters()
                                            .Select(p => p.ParameterType)
                                            .ShouldBe([typeof(ICoverageConfigParser)]);
    result.Normalized.ShouldBeSameAs(Root);
    A.CallTo(() => _parser.Parse(A<string>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_KeyWarnedByTheParserAndTheTypedView_ShouldWarnOnce()
  {
    var root = ConfigNode.FromMap(("coverage", ConfigNode.FromMap(("precision", ConfigNode.FromText("two")))));
    var warning = new ConfigWarning(ConfigWarning.InvalidValue, "coverage.precision", "two");
    Parses(root, warning);

    var result = await HandleAsync();

    result.Warnings.ShouldBe([warning]);
  }

  [Fact]
  public async Task Handle_ValueTheTypedViewCannotRead_ShouldWarn()
  {
    Parses(ConfigNode.FromMap(("coverage", ConfigNode.FromMap(("round", ConfigNode.FromText("sideways"))))));

    var result = await HandleAsync();

    result.IsValid.ShouldBeTrue();
    result.Warnings.ShouldBe([new ConfigWarning(ConfigWarning.InvalidValue, "coverage.round", "sideways")]);
  }

  [Fact]
  public async Task Handle_FileThatCannotBeRead_ShouldReturnTheError()
  {
    A.CallTo(() => _parser.Parse(Yaml)).Returns(new ConfigParseResult(null, "Not YAML.", []));

    var result = await HandleAsync();

    result.IsValid.ShouldBeFalse();
    result.Normalized.ShouldBeNull();
    result.Warnings.ShouldBeEmpty();
    result.Errors.ShouldBe(["Not YAML."]);
  }

  [Fact]
  public async Task Handle_FileWithoutKeys_ShouldBeInvalid()
  {
    Parses(ConfigNode.FromMap());

    var result = await HandleAsync();

    result.IsValid.ShouldBeFalse();
    result.Normalized.ShouldBeNull();
    result.Errors.ShouldBe(["The file is empty."]);
  }

  private void Parses(ConfigNode root, params ConfigWarning[] warnings) =>
    A.CallTo(() => _parser.Parse(Yaml)).Returns(new ConfigParseResult(root, null, warnings));

  private async Task<ConfigValidationResult> HandleAsync() =>
    await new ValidateCoverageYamlQueryHandler(_parser).Handle(new ValidateCoverageYamlQuery(Yaml), Ct);

  #endregion
}
