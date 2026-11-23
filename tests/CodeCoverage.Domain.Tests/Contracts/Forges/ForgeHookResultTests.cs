// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookResultTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;
using CodeCoverage.Domain.Contracts.Forges;

namespace CodeCoverage.Domain.Tests.Contracts.Forges;

public sealed class ForgeHookResultTests
{
  #region Properties

  private static ForgeHookScope Scope => new(ForgeHookScopeKind.Repository, "42", null);

  #endregion

  #region Methods

  [Fact]
  public void Active_ShouldKeepTheHook()
  {
    var result = ForgeHookResult.Active(Scope, "hook-1");

    result.Scope.ShouldBe(Scope);
    result.State.ShouldBe(ForgeHookState.Active);
    result.HookExternalId.ShouldBe("hook-1");
    result.Error.ShouldBeNull();
  }

  [Fact]
  public void Disabled_ShouldKeepTheHookAndTheReason()
  {
    var result = ForgeHookResult.Disabled(Scope, "hook-1", "too many failures");

    result.State.ShouldBe(ForgeHookState.Disabled);
    result.HookExternalId.ShouldBe("hook-1");
    result.Error.ShouldBe("too many failures");
  }

  [Fact]
  public void Missing_ShouldHaveNoHook()
  {
    var result = ForgeHookResult.Missing(Scope);

    result.State.ShouldBe(ForgeHookState.Missing);
    result.HookExternalId.ShouldBeNull();
    result.Error.ShouldBeNull();
  }

  [Fact]
  public void Forbidden_ShouldKeepTheReason()
  {
    var result = ForgeHookResult.Forbidden(Scope, "403 Forbidden");

    result.State.ShouldBe(ForgeHookState.Forbidden);
    result.HookExternalId.ShouldBeNull();
    result.Error.ShouldBe("403 Forbidden");
  }

  [Fact]
  public void Factories_NullScope_ShouldThrowArgumentNullException()
  {
    Should.Throw<ArgumentNullException>(() => ForgeHookResult.Active(null!, "hook-1"));
    Should.Throw<ArgumentNullException>(() => ForgeHookResult.Disabled(null!, "hook-1", null));
    Should.Throw<ArgumentNullException>(() => ForgeHookResult.Missing(null!));
    Should.Throw<ArgumentNullException>(() => ForgeHookResult.Forbidden(null!, "refused"));
  }

  [Fact]
  public void Factories_BlankHookOrReason_ShouldThrowArgumentException()
  {
    Should.Throw<ArgumentException>(() => ForgeHookResult.Active(Scope, " "));
    Should.Throw<ArgumentException>(() => ForgeHookResult.Disabled(Scope, "", null));
    Should.Throw<ArgumentException>(() => ForgeHookResult.Forbidden(Scope, " "));
  }

  #endregion
}
