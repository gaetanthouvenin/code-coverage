// ------------------------------------------------------------------------------------------------
// <copyright file="PortConventionTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using CodeCoverage.Architecture.Tests.Samples.Ports;

using static CodeCoverage.Architecture.Tests.Support.SolutionAssemblies;

namespace CodeCoverage.Architecture.Tests;

// The ports of the Domain return Domain objects, never IQueryable, take a mandatory
// CancellationToken and save at once: no unit of work (CONVENTIONS §4.2).
public sealed class PortConventionTests
{
  #region Fields

  private const string PortsNamespace = DomainName + ".Ports";

  private static readonly string[] UnitOfWorkMethods =
  [
    "SaveChanges",
    "SaveChangesAsync",
    "Commit",
    "CommitAsync",
    "CommitTransaction",
    "CommitTransactionAsync",
    "BeginTransaction",
    "BeginTransactionAsync",
    "Rollback",
    "RollbackAsync",
    "RollbackTransaction",
    "RollbackTransactionAsync",
  ];

  private static readonly string[] UnitOfWorkTypeNames = ["UnitOfWork", "DbContext", "Transaction"];

  #endregion

  #region Properties

  private static IEnumerable<Type> Ports =>
    TypesOf(DomainAssembly).Where(t => t.IsInterface
                               && t.Namespace is not null
                               && (t.Namespace == PortsNamespace
                                   || t.Namespace.StartsWith(PortsNamespace + ".", StringComparison.Ordinal)));

  #endregion

  #region Methods

  [Fact]
  public void Ports_WithSharedPorts_ShouldBeDiscovered()
  {
    string[] shared = ["IClock", "ICurrentUser"];

    shared.Except(Ports.Select(t => t.Name), StringComparer.Ordinal)
          .ShouldBeEmpty("the port rules must check the shared ports of the Domain");
  }

  [Fact]
  public void Ports_ShouldNotExposeQueryable()
  {
    Ports.SelectMany(QueryableViolations)
         .ShouldBeEmpty("a port returns Domain objects or IReadOnlyList<T>, never IQueryable");
  }

  [Fact]
  public void Ports_ShouldRequireCancellationToken()
  {
    Ports.SelectMany(CancellationViolations)
         .ShouldBeEmpty("an asynchronous port method takes a CancellationToken, without default value");
  }

  [Fact]
  public void Ports_ShouldOfferNoUnitOfWork()
  {
    Ports.SelectMany(UnitOfWorkViolations)
         .ShouldBeEmpty("a repository saves at once: a port never exposes a unit of work");
  }

  [Fact]
  public void QueryableViolations_WithQueryablePort_ShouldNameEachMember()
  {
    var violations = QueryableViolations(typeof(IQueryableSamplePort));

    violations.Count.ShouldBe(2);
    violations.ShouldContain(v => v.Contains("Names", StringComparison.Ordinal));
    violations.ShouldContain(v => v.Contains("SearchAsync", StringComparison.Ordinal));
  }

  [Fact]
  public void CancellationViolations_WithTokenlessPort_ShouldNameEachMethod()
  {
    var violations = CancellationViolations(typeof(ITokenlessSamplePort));

    violations.Count.ShouldBe(2);
    violations.ShouldContain(v => v.Contains("SaveAsync", StringComparison.Ordinal)
                                  && v.Contains("no CancellationToken", StringComparison.Ordinal));
    violations.ShouldContain(v => v.Contains("CountAsync", StringComparison.Ordinal)
                                  && v.Contains("default value", StringComparison.Ordinal));
  }

  [Fact]
  public void UnitOfWorkViolations_WithUnitOfWork_ShouldNameTheTypeAndTheMethod()
  {
    var violations = UnitOfWorkViolations(typeof(ISampleUnitOfWork));

    violations.ShouldContain(v => v.EndsWith(nameof(ISampleUnitOfWork) + " is a unit of work", StringComparison.Ordinal));
    violations.ShouldContain(v => v.Contains("SaveChangesAsync", StringComparison.Ordinal));
  }

  [Fact]
  public void Violations_WithCompliantPort_ShouldBeEmpty()
  {
    var port = typeof(ICompliantSamplePort);

    QueryableViolations(port).ShouldBeEmpty();
    CancellationViolations(port).ShouldBeEmpty();
    UnitOfWorkViolations(port).ShouldBeEmpty();
  }

  private static List<string> QueryableViolations(Type port)
  {
    var violations = Methods(port).Where(m => !m.IsSpecialName
                                              && (Exposes(m.ReturnType) || m.GetParameters().Any(p => Exposes(p.ParameterType))))
                                  .Select(m => $"{port.FullName}.{m.Name} exposes IQueryable")
                                  .ToList();

    violations.AddRange(Properties(port).Where(p => Exposes(p.PropertyType))
                                        .Select(p => $"{port.FullName}.{p.Name} exposes IQueryable"));

    return violations;
  }

  private static List<string> CancellationViolations(Type port)
  {
    var violations = new List<string>();

    foreach (var method in Methods(port).Where(m => !m.IsSpecialName))
    {
      var tokens = method.GetParameters().Where(p => p.ParameterType == typeof(CancellationToken)).ToList();

      if (tokens.Count == 0 && IsAsynchronous(method.ReturnType))
      {
        violations.Add($"{port.FullName}.{method.Name} takes no CancellationToken");
      }

      violations.AddRange(tokens.Where(p => p.HasDefaultValue || p.IsOptional)
                                .Select(p => $"{port.FullName}.{method.Name} gives {p.Name} a default value"));
    }

    return violations;
  }

  private static List<string> UnitOfWorkViolations(Type port)
  {
    var violations = new List<string>();

    if (NamesUnitOfWork(port))
    {
      violations.Add($"{port.FullName} is a unit of work");
    }

    foreach (var method in Methods(port).Where(m => !m.IsSpecialName))
    {
      if (UnitOfWorkMethods.Contains(method.Name, StringComparer.Ordinal))
      {
        violations.Add($"{port.FullName}.{method.Name} saves on behalf of the handler");
      }

      if (Involves(method.ReturnType, NamesUnitOfWork) || method.GetParameters().Any(p => Involves(p.ParameterType, NamesUnitOfWork)))
      {
        violations.Add($"{port.FullName}.{method.Name} hands out a unit of work or a transaction");
      }
    }

    violations.AddRange(Properties(port).Where(p => Involves(p.PropertyType, NamesUnitOfWork))
                                        .Select(p => $"{port.FullName}.{p.Name} hands out a unit of work or a transaction"));

    return violations;
  }

  // The members of the port and of the interfaces it inherits.
  private static IEnumerable<MethodInfo> Methods(Type port) => port.GetInterfaces().Prepend(port).SelectMany(i => i.GetMethods());

  private static IEnumerable<PropertyInfo> Properties(Type port) => port.GetInterfaces().Prepend(port).SelectMany(i => i.GetProperties());

  private static bool Exposes(Type type) =>
    Involves(type, t => typeof(IQueryable).IsAssignableFrom(t)
                        || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IQueryable<>)));

  private static bool NamesUnitOfWork(Type type) =>
    UnitOfWorkTypeNames.Any(n => type.Name.Contains(n, StringComparison.Ordinal));

  // The type itself, its element type and its generic arguments, recursively.
  private static bool Involves(Type type, Func<Type, bool> predicate)
  {
    if (type.HasElementType)
    {
      return Involves(type.GetElementType()!, predicate);
    }

    return predicate(type) || (type.IsGenericType && type.GetGenericArguments().Any(a => Involves(a, predicate)));
  }

  private static bool IsAsynchronous(Type type)
  {
    if (type == typeof(Task) || type == typeof(ValueTask))
    {
      return true;
    }

    if (!type.IsGenericType)
    {
      return false;
    }

    var definition = type.GetGenericTypeDefinition();

    return definition == typeof(Task<>) || definition == typeof(ValueTask<>) || definition == typeof(IAsyncEnumerable<>);
  }

  #endregion
}
