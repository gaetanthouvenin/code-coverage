// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookScopeTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Tests.Contracts.Forges;

public sealed class ForgeHookScopeTests
{
  #region Methods

  [Fact]
  public void Ctor_KnownHook_ShouldKeepEveryValue()
  {
    var scope = new ForgeHookScope(ForgeHookScopeKind.Project, "Platform", "sub-1");

    scope.Kind.ShouldBe(ForgeHookScopeKind.Project);
    scope.ExternalId.ShouldBe("Platform");
    scope.HookExternalId.ShouldBe("sub-1");
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("  ")]
  public void Ctor_NoHook_ShouldHaveNoHookIdentifier(string? hookExternalId)
  {
    new ForgeHookScope(ForgeHookScopeKind.Repository, "42", hookExternalId).HookExternalId.ShouldBeNull();
  }

  [Fact]
  public void Ctor_UndefinedKind_ShouldThrowArgumentOutOfRangeException()
  {
    Should.Throw<ArgumentOutOfRangeException>(() => new ForgeHookScope((ForgeHookScopeKind)99, "42", null));
  }

  [Theory]
  [InlineData("")]
  [InlineData(" ")]
  public void Ctor_BlankExternalId_ShouldThrowArgumentException(string externalId)
  {
    Should.Throw<ArgumentException>(() => new ForgeHookScope(ForgeHookScopeKind.Repository, externalId, null));
  }

  [Fact]
  public void IsSameScope_SameKindAndIdentifierInAnotherCase_ShouldBeTrue()
  {
    new ForgeHookScope(ForgeHookScopeKind.Project, "Platform", "sub-1")
      .IsSameScope(new ForgeHookScope(ForgeHookScopeKind.Project, "platform", null))
      .ShouldBeTrue();
  }

  [Fact]
  public void IsSameScope_OtherKindOrIdentifier_ShouldBeFalse()
  {
    var scope = new ForgeHookScope(ForgeHookScopeKind.Project, "platform", null);

    scope.IsSameScope(new ForgeHookScope(ForgeHookScopeKind.Repository, "platform", null)).ShouldBeFalse();
    scope.IsSameScope(new ForgeHookScope(ForgeHookScopeKind.Project, "tools", null)).ShouldBeFalse();
  }

  [Fact]
  public void IsSameScope_Null_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => new ForgeHookScope(ForgeHookScopeKind.Project, "platform", null).IsSameScope(null!));
  }

  #endregion
}
