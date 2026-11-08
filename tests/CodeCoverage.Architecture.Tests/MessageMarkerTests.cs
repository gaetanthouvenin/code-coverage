// ------------------------------------------------------------------------------------------------
// <copyright file="MessageMarkerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using static CodeCoverage.Architecture.Tests.Support.SolutionAssemblies;

namespace CodeCoverage.Architecture.Tests;

// Every message states who may send it, with exactly one marker: it serves one audience (ADR-013).
public sealed class MessageMarkerTests
{
  #region Fields

  private static readonly string[] AccessMarkerNames =
  [
    "IRepositoryScoped",
    "IOrganizationScoped",
    "IPublicOwnerAddressed",
    "IAnonymousEntry",
    "IUserScoped",
    "IAdminOnly",
    "ISystemOnly",
  ];

  // Any interface proves the rule itself: the two samples below have no link with the Application.
  private static readonly Type[] SampleMarkers = [typeof(IDisposable), typeof(IAsyncDisposable)];

  #endregion

  #region Properties

  // Loaded by name, like the assemblies: a marker renamed or removed makes the rule fail, not pass.
  private static IReadOnlyList<Type> AccessMarkers =>
    AccessMarkerNames.Select(name => ApplicationAssembly.GetType($"{ApplicationName}.Security.{name}", throwOnError: true)!)
                     .ToList();

  #endregion

  #region Methods

  [Fact]
  public void Messages_ShouldDeclareExactlyOneAccessMarker()
  {
    var markers = AccessMarkers;
    var messages = TypesOf(ApplicationAssembly)
                   .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IMessage).IsAssignableFrom(t))
                   .ToList();

    messages.ShouldNotBeEmpty("the rule must check the messages of the Application");
    messages.Select(message => MarkerViolation(message, markers))
            .OfType<string>()
            .ShouldBeEmpty("a message declares exactly one access marker of Application/Security");
  }

  [Fact]
  public void MarkerViolation_WithoutMarker_ShouldNameTheType()
  {
    var violation = MarkerViolation(typeof(object), SampleMarkers);

    violation.ShouldNotBeNull();
    violation.ShouldContain(nameof(Object));
    violation.ShouldContain("0 access markers");
  }

  [Fact]
  public void MarkerViolation_WithTwoMarkers_ShouldNameThem()
  {
    var violation = MarkerViolation(typeof(Stream), SampleMarkers);

    violation.ShouldNotBeNull();
    violation.ShouldContain(nameof(IDisposable));
    violation.ShouldContain(nameof(IAsyncDisposable));
  }

  [Fact]
  public void MarkerViolation_WithOneMarker_ShouldReturnNull()
  {
    MarkerViolation(typeof(StringComparer), [typeof(IComparer<string>)]).ShouldBeNull();
  }

  private static string? MarkerViolation(Type message, IReadOnlyList<Type> markers)
  {
    var declared = markers.Where(m => m.IsAssignableFrom(message)).Select(m => m.Name).ToList();

    return declared.Count == 1
             ? null
             : $"{message.FullName} declares {declared.Count} access markers: [{string.Join(", ", declared)}]";
  }

  #endregion
}
