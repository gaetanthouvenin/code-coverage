// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationContextTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Reflection;

using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;

namespace CodeCoverage.Infrastructure.Tests.Identity;

public sealed class OrganizationContextTests
{
  #region Methods

  [Fact]
  public void Id_WhenUnset_ShouldThrow()
  {
    var context = new OrganizationContext();

    context.IsKnown.ShouldBeFalse();
    Should.Throw<InvalidOperationException>(() => context.Id);
  }

  [Fact]
  public void Enter_ShouldSetTheOrganization()
  {
    var context = new OrganizationContext();

    context.Enter(42);

    context.IsKnown.ShouldBeTrue();
    context.Id.ShouldBe(42);
  }

  [Fact]
  public void Enter_SameValueTwice_ShouldKeepIt()
  {
    var context = new OrganizationContext();
    context.Enter(42);

    context.Enter(42);

    context.Id.ShouldBe(42);
  }

  [Fact]
  public void Enter_OtherValue_ShouldThrow()
  {
    var context = new OrganizationContext();
    context.Enter(42);

    Should.Throw<InvalidOperationException>(() => context.Enter(43));

    context.Id.ShouldBe(42);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(-1)]
  public void Enter_NotPositive_ShouldThrow(long organizationId)
  {
    var context = new OrganizationContext();

    Should.Throw<ArgumentOutOfRangeException>(() => context.Enter(organizationId));

    context.IsKnown.ShouldBeFalse();
  }

  // The acceptance of the micro-feature: the Application only sees the ports, and no port enters by identifier.
  [Fact]
  public void Enter_ShouldNotBeReachableThroughThePorts()
  {
    var enter = typeof(OrganizationContext).GetMethod(
      nameof(OrganizationContext.Enter),
      BindingFlags.Instance | BindingFlags.NonPublic
    );

    typeof(OrganizationContext).IsPublic.ShouldBeFalse();
    enter.ShouldNotBeNull().IsAssembly.ShouldBeTrue();
    typeof(ICurrentOrganization).GetProperties().ShouldAllBe(static p => !p.CanWrite);
    typeof(IOrganizationScope).GetMethods()
                              .SelectMany(static m => m.GetParameters())
                              .ShouldNotContain(static p => p.ParameterType == typeof(long));
  }

  #endregion
}
