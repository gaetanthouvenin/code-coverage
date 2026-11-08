// ------------------------------------------------------------------------------------------------
// <copyright file="HandlerConventionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Architecture.Tests.Samples.Handlers;

using static CodeCoverage.Architecture.Tests.Support.SolutionAssemblies;

namespace CodeCoverage.Architecture.Tests;

// A handler is an implementation detail of the Application: internal sealed (CONVENTIONS §3.5).
public sealed class HandlerConventionTests
{
  #region Methods

  [Fact]
  public void Handlers_ShouldBeInternalSealed()
  {
    TypesOf(ApplicationAssembly)
      .Select(HandlerViolation)
      .OfType<string>()
      .ShouldBeEmpty("a handler is declared internal sealed");
  }

  [Fact]
  public void HandlerViolation_WithPublicUnsealedHandler_ShouldNameTheType()
  {
    var violation = HandlerViolation(typeof(PublicSampleCommandHandler));

    violation.ShouldNotBeNull();
    violation.ShouldContain(nameof(PublicSampleCommandHandler));
    violation.ShouldContain("public");
    violation.ShouldContain("not sealed");
  }

  [Fact]
  public void HandlerViolation_WithInternalSealedHandler_ShouldReturnNull()
  {
    HandlerViolation(typeof(InternalSampleQueryHandler)).ShouldBeNull();
  }

  // A handler is named {UseCase}{Command|Query}Handler or implements a handler of the mediator.
  private static string? HandlerViolation(Type type)
  {
    var isHandler = type.Name.Split('`')[0].EndsWith("Handler", StringComparison.Ordinal)
                    || type.GetInterfaces()
                           .Any(i => i.Namespace == "Mediator"
                                     && i.Name.Split('`')[0].EndsWith("Handler", StringComparison.Ordinal));

    if (!isHandler || type.IsInterface)
    {
      return null;
    }

    var problems = new List<string>();

    if (type.IsPublic || type.IsNestedPublic)
    {
      problems.Add("public");
    }

    if (!type.IsSealed)
    {
      problems.Add("not sealed");
    }

    return problems.Count == 0 ? null : $"{type.FullName} is {string.Join(" and ", problems)}";
  }

  #endregion
}
