// ------------------------------------------------------------------------------------------------
// <copyright file="PresentationIsolationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using Mediator;

using NetArchTest.Rules;

using CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Components;
using CodeCoverage.Architecture.Tests.Samples.Presentation.Host.Controllers;

using static CodeCoverage.Architecture.Tests.Support.SolutionAssemblies;

namespace CodeCoverage.Architecture.Tests;

// The Api and the Web never handle a Domain type, outside their mappers and their composition
// root (PLAN §1.4, "send and convert"). NetArchTest reads the IL: method bodies and generic
// arguments count. The Web components never hold a sender either: they send through
// UiRequestRunner (PLAN M0.13).
public sealed class PresentationIsolationTests
{
  #region Fields

  private const string SamplesNamespace = "CodeCoverage.Architecture.Tests.Samples.Presentation";

  private const string SampleHost = SamplesNamespace + ".Host";

  private const string SampleModel = SamplesNamespace + ".Model";

  private const string SampleComponents = SampleHost + ".Components";

  private const BindingFlags Declared =
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

  #endregion

  #region Methods

  [Fact]
  public void ApiAndWeb_ShouldNotReferenceDomain_ExceptMappingProgramAndConfiguration()
  {
    DomainLeaks(ApiAssembly, ApiName, DomainName, scope: null)
      .Concat(DomainLeaks(WebAssembly, WebName, DomainName, scope: null))
      .ShouldBeEmpty("outside Mapping, Program and Configuration, the Api and the Web never name a Domain type: "
                     + "go through a mapper extension on ISender (PLAN §1.4)");
  }

  [Fact]
  public void DomainLeaks_WithDomainTypeInSignatureOrBody_ShouldNameTheOffendingTypes()
  {
    var leaks = DomainLeaks(typeof(PresentationIsolationTests).Assembly, SampleHost, SampleModel, scope: SampleHost);

    leaks.ShouldBe(
      [
        typeof(GenericArgumentController).FullName!,
        typeof(MethodBodyController).FullName!,
      ],
      ignoreOrder: true);
  }

  [Fact]
  public void WebComponents_ShouldNotHoldASender()
  {
    // A dependency rule on Mediator.ISender would also refuse the lambdas the pages hand to
    // UiRequestRunner.RunAsync, which name ISender as their parameter: the rule targets what a
    // component holds (an [Inject] property, a field, a constructor parameter), not what it names.
    SenderHolders(WebAssembly, $"{WebName}.Components")
      .ShouldBeEmpty("a Web component never holds an ISender, an IMediator or the ScopedSender: "
                     + "it sends through UiRequestRunner, which reports the refusals to the user (PLAN M0.13)");
  }

  [Fact]
  public void SenderHolders_WithSenderPropertyOrConstructorParameter_ShouldNameTheOffendingTypes()
  {
    var holders = SenderHolders(typeof(PresentationIsolationTests).Assembly, SampleComponents);

    holders.ShouldBe(
      [
        typeof(ConstructedSenderComponent).FullName!,
        typeof(InjectedSenderComponent).FullName!,
      ],
      ignoreOrder: true);
    holders.ShouldNotContain(typeof(RunnerComponent).FullName!);
  }

  private static List<string> SenderHolders(Assembly assembly, string componentsNamespace) =>
    TypesOf(assembly).Where(t => t.Namespace is { } ns
                                 && (ns == componentsNamespace || InNamespace(ns, componentsNamespace)))
                     .Where(HoldsASender)
                     .Select(t => t.FullName!)
                     .Order(StringComparer.Ordinal)
                     .ToList();

  private static bool HoldsASender(Type type) =>
    type.GetFields(Declared).Any(f => IsSender(f.FieldType))
    || type.GetProperties(Declared).Any(p => IsSender(p.PropertyType))
    || type.GetConstructors(Declared).SelectMany(c => c.GetParameters()).Any(p => IsSender(p.ParameterType));

  private static bool IsSender(Type type) =>
    typeof(ISender).IsAssignableFrom(type) || typeof(IPublisher).IsAssignableFrom(type);

  private static List<string> DomainLeaks(Assembly assembly, string hostNamespace, string domainNamespace, string? scope)
  {
    var conditions = scope is null
                       ? Types.InAssembly(assembly).ShouldNot()
                       : Types.InAssembly(assembly).That().ResideInNamespaceStartingWith(scope).ShouldNot();
    var result = conditions.HaveDependencyOn(domainNamespace).GetResult();

    return (result.FailingTypeNames ?? []).Where(n => !IsAllowed(n, hostNamespace, domainNamespace))
                                         .Order(StringComparer.Ordinal)
                                         .ToList();
  }

  // Program (and the types the compiler nests in it), {Host}.Mapping and {Host}.Configuration.
  private static bool IsAllowed(string typeName, string hostNamespace, string domainNamespace)
  {
    var outer = typeName.Split('/', '+')[0];

    return outer == "Program"
           || InNamespace(outer, $"{hostNamespace}.Mapping")
           || InNamespace(outer, $"{hostNamespace}.Configuration")
           || InNamespace(outer, domainNamespace);
  }

  private static bool InNamespace(string typeName, string ns) => typeName.StartsWith(ns + ".", StringComparison.Ordinal);

  #endregion
}
