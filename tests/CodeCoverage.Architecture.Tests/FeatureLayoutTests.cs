// ------------------------------------------------------------------------------------------------
// <copyright file="FeatureLayoutTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using static CodeCoverage.Architecture.Tests.Support.SolutionAssemblies;

namespace CodeCoverage.Architecture.Tests;

// The use cases live in Features/{Feature}/{Commands|Queries}/{UseCase}/, named after their folder
// (CONVENTIONS §5.1), and no layer carries a DTO.
public sealed partial class FeatureLayoutTests
{
  #region Fields

  private const string FeaturesNamespace = ApplicationName + ".Features";

  #endregion

  #region Methods

  [Fact]
  public void UseCases_ShouldLiveInFeatureCommandsOrQueriesFolder()
  {
    TypesOf(ApplicationAssembly)
      .Select(t => MisplacedUseCase(t.Namespace, t.Name))
      .OfType<string>()
      .ShouldBeEmpty("a use case lives in Features/{Feature}/{Commands|Queries}/{UseCase}/ and is named after its folder");
  }

  [Fact]
  public void Types_ShouldNotBeSuffixedDto()
  {
    Production.SelectMany(TypesOf)
              .Select(t => DtoViolation(t.Namespace, t.Name))
              .OfType<string>()
              .ShouldBeEmpty("requests return Domain objects and hosts use Response or ViewModel types: never a DTO");
  }

  [Theory]
  [InlineData(FeaturesNamespace + ".Repositories.Commands.CreateRepository", "CreateRepositoryCommand")]
  [InlineData(FeaturesNamespace + ".Repositories.Commands.CreateRepository", "CreateRepositoryCommandHandler")]
  [InlineData(FeaturesNamespace + ".Repositories.Commands.CreateRepository", "CreateRepositoryCommandValidator")]
  [InlineData(FeaturesNamespace + ".Repositories.Queries.GetRepository", "GetRepositoryQuery")]
  [InlineData(FeaturesNamespace + ".Repositories.Queries.GetRepository", "GetRepositoryQueryHandler")]
  [InlineData(FeaturesNamespace + ".Repositories", "RepositoryErrors")]
  [InlineData(ApplicationName + ".Behaviors", "LoggingBehavior")]
  public void MisplacedUseCase_WithWellPlacedType_ShouldReturnNull(string ns, string name)
  {
    MisplacedUseCase(ns, name).ShouldBeNull();
  }

  [Theory]
  [InlineData(ApplicationName + ".Commands.Repositories.CreateRepository", "CreateRepositoryCommand")]
  [InlineData(FeaturesNamespace + ".Repositories.Commands", "CreateRepositoryCommand")]
  [InlineData(FeaturesNamespace + ".Repositories.Queries.CreateRepository", "CreateRepositoryCommand")]
  [InlineData(FeaturesNamespace + ".Repositories.Commands.AddRepository", "CreateRepositoryCommandHandler")]
  [InlineData(FeaturesNamespace + ".Repositories.Commands.CreateRepository.Rules", "SlugRule")]
  [InlineData(ApplicationName, "GetRepositoryQuery")]
  public void MisplacedUseCase_WithMisplacedType_ShouldExplainTheExpectedFolder(string ns, string name)
  {
    MisplacedUseCase(ns, name).ShouldNotBeNull().ShouldContain(name);
  }

  [Theory]
  [InlineData(ApplicationName + ".Features.Repositories", "RepositoryDto")]
  [InlineData(ApplicationName + ".Features.Repositories", "PageDTO`1")]
  [InlineData(ApiName + ".Dtos", "RepositoryResponse")]
  public void DtoViolation_WithDto_ShouldNameTheType(string ns, string name)
  {
    DtoViolation(ns, name).ShouldNotBeNull().ShouldContain(ns);
  }

  [Theory]
  [InlineData(ApiName + ".Contracts", "RepositoryResponse")]
  [InlineData(WebName + ".ViewModels.Repositories", "RepositoryViewModel")]
  public void DtoViolation_WithResponseOrViewModel_ShouldReturnNull(string ns, string name)
  {
    DtoViolation(ns, name).ShouldBeNull();
  }

  private static string? MisplacedUseCase(string? ns, string name)
  {
    var simpleName = name.Split('`')[0];
    var segments = (ns ?? string.Empty).Split('.');
    var inUseCaseFolder = segments.Contains("Commands") || segments.Contains("Queries");
    var useCase = UseCaseName().Match(simpleName);

    if (!useCase.Success && !inUseCaseFolder)
    {
      return null;
    }

    var folder = UseCaseFolder().Match(ns ?? string.Empty);

    if (!folder.Success)
    {
      return $"{ns}.{name} is not in {FeaturesNamespace}.{{Feature}}.{{Commands|Queries}}.{{UseCase}}";
    }

    if (!useCase.Success)
    {
      return null;
    }

    var expectedKind = useCase.Groups["kind"].Value == "Command" ? "Commands" : "Queries";
    var expectedFolder = useCase.Groups["case"].Value;

    if (folder.Groups["kind"].Value != expectedKind || folder.Groups["case"].Value != expectedFolder)
    {
      return $"{ns}.{name} belongs to {FeaturesNamespace}.{folder.Groups["feature"].Value}.{expectedKind}.{expectedFolder}";
    }

    return null;
  }

  private static string? DtoViolation(string? ns, string name)
  {
    var simpleName = name.Split('`')[0];
    var dtoFolder = (ns ?? string.Empty).Split('.')
                                         .Any(s => s.Equals("Dto", StringComparison.OrdinalIgnoreCase)
                                                   || s.Equals("Dtos", StringComparison.OrdinalIgnoreCase));

    return simpleName.EndsWith("Dto", StringComparison.OrdinalIgnoreCase) || dtoFolder ? $"{ns}.{name}" : null;
  }

  [GeneratedRegex(@"^(?<case>[A-Z]\w*?)(?<kind>Command|Query)(?:Handler|Validator)?$", RegexOptions.CultureInvariant)]
  private static partial Regex UseCaseName();

  [GeneratedRegex(
    @"^CodeCoverage\.Application\.Features\.(?<feature>[A-Z]\w*)\.(?<kind>Commands|Queries)\.(?<case>[A-Z]\w*)$",
    RegexOptions.CultureInvariant)]
  private static partial Regex UseCaseFolder();

  #endregion
}
