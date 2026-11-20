// ------------------------------------------------------------------------------------------------
// <copyright file="ForgeHookTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Connections;

namespace CodeCoverage.Domain.Tests.Connections;

public sealed class ForgeHookTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 20, 8, 0, 0, TimeSpan.Zero);

  #endregion

  #region Methods

  [Fact]
  public void Create_Valid_ShouldBeMissingFromNow()
  {
    var hook = ForgeHook.Create("conn-1", ForgeHookScopeKind.Project, "ABC-Project", Now.ToOffset(TimeSpan.FromHours(2)));

    hook.Id.ShouldBe(0);
    hook.ConnectionId.ShouldBe("conn-1");
    hook.ScopeKind.ShouldBe(ForgeHookScopeKind.Project);
    hook.ScopeExternalId.ShouldBe("abc-project");
    hook.ExternalId.ShouldBeNull();
    hook.State.ShouldBe(ForgeHookState.Missing);
    hook.LastCheckedAt.ShouldBe(Now);
    hook.LastCheckedAt.Offset.ShouldBe(TimeSpan.Zero);
    hook.LastError.ShouldBeNull();
  }

  [Fact]
  public void Create_NullConnection_ShouldThrow() =>
    Should.Throw<ArgumentNullException>(() => ForgeHook.Create(null!, ForgeHookScopeKind.Project, "project", Now));

  [Fact]
  public void Create_MalformedConnection_ShouldThrow() =>
    Should.Throw<ArgumentException>(() => ForgeHook.Create("Conn 1", ForgeHookScopeKind.Project, "project", Now)).ParamName.ShouldBe("connectionId");

  [Fact]
  public void Create_UndefinedScopeKind_ShouldThrow() =>
    Should.Throw<ArgumentOutOfRangeException>(() => ForgeHook.Create("conn-1", (ForgeHookScopeKind)42, "project", Now));

  [Theory]
  [InlineData(null)]
  [InlineData(" ")]
  public void Create_BlankScope_ShouldThrow(string? scopeExternalId) =>
    Should.Throw<ArgumentException>(() => ForgeHook.Create("conn-1", ForgeHookScopeKind.Repository, scopeExternalId!, Now));

  [Fact]
  public void Create_TooLongScope_ShouldThrow() =>
    Should.Throw<ArgumentException>(() => ForgeHook.Create("conn-1", ForgeHookScopeKind.Repository, new string('a', ForgeHook.ScopeExternalIdMaxLength + 1), Now))
          .ParamName.ShouldBe("scopeExternalId");

  [Fact]
  public void Create_LongestScope_ShouldBeKept() =>
    ForgeHook.Create("conn-1", ForgeHookScopeKind.Connection, new string('a', ForgeHook.ScopeExternalIdMaxLength), Now).ScopeExternalId.Length.ShouldBe(ForgeHook.ScopeExternalIdMaxLength);

  [Fact]
  public void MarkActive_Forbidden_ShouldRecordTheHookAndForgetTheError()
  {
    var hook = Hook();
    hook.MarkForbidden("403 Forbidden", Now);

    hook.MarkActive("Hook-1", Now.AddHours(1));

    hook.State.ShouldBe(ForgeHookState.Active);
    hook.ExternalId.ShouldBe("Hook-1");
    hook.LastError.ShouldBeNull();
    hook.LastCheckedAt.ShouldBe(Now.AddHours(1));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  public void MarkActive_BlankExternalId_ShouldThrow(string? externalId) =>
    Should.Throw<ArgumentException>(() => Hook().MarkActive(externalId!, Now));

  [Fact]
  public void MarkActive_TooLongExternalId_ShouldThrow() =>
    Should.Throw<ArgumentException>(() => Hook().MarkActive(new string('h', ForgeHook.ExternalIdMaxLength + 1), Now)).ParamName.ShouldBe("externalId");

  [Fact]
  public void MarkDisabled_WithReason_ShouldRecordIt()
  {
    var hook = Hook();

    hook.MarkDisabled("hook-1", "Disabled after repeated failures", Now.AddHours(1));

    hook.State.ShouldBe(ForgeHookState.Disabled);
    hook.ExternalId.ShouldBe("hook-1");
    hook.LastError.ShouldBe("Disabled after repeated failures");
    hook.LastCheckedAt.ShouldBe(Now.AddHours(1));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("  ")]
  public void MarkDisabled_WithoutReason_ShouldRecordNone(string? error)
  {
    var hook = Hook();

    hook.MarkDisabled("hook-1", error, Now);

    hook.State.ShouldBe(ForgeHookState.Disabled);
    hook.LastError.ShouldBeNull();
  }

  [Fact]
  public void MarkDisabled_BlankExternalId_ShouldThrow() =>
    Should.Throw<ArgumentException>(() => Hook().MarkDisabled(" ", null, Now));

  [Fact]
  public void MarkMissing_Disabled_ShouldForgetTheHookAndTheError()
  {
    var hook = Hook();
    hook.MarkDisabled("hook-1", "turned off", Now);

    hook.MarkMissing(Now.AddHours(1));

    hook.State.ShouldBe(ForgeHookState.Missing);
    hook.ExternalId.ShouldBeNull();
    hook.LastError.ShouldBeNull();
    hook.LastCheckedAt.ShouldBe(Now.AddHours(1));
  }

  [Fact]
  public void MarkForbidden_Active_ShouldRecordTheError()
  {
    var hook = Hook();
    hook.MarkActive("hook-1", Now);

    hook.MarkForbidden("TF400813: not authorized", Now.AddHours(1).ToOffset(TimeSpan.FromHours(-5)));

    hook.State.ShouldBe(ForgeHookState.Forbidden);
    hook.LastError.ShouldBe("TF400813: not authorized");
    hook.ExternalId.ShouldBe("hook-1", "the forge no longer tells whether the hook still exists");
    hook.LastCheckedAt.ShouldBe(Now.AddHours(1));
    hook.LastCheckedAt.Offset.ShouldBe(TimeSpan.Zero);
  }

  [Fact]
  public void MarkForbidden_LongError_ShouldCutIt()
  {
    var hook = Hook();

    hook.MarkForbidden(new string('e', ForgeHook.LastErrorMaxLength + 10), Now);

    hook.LastError.ShouldNotBeNull().Length.ShouldBe(ForgeHook.LastErrorMaxLength);
  }

  [Theory]
  [InlineData(null)]
  [InlineData(" ")]
  public void MarkForbidden_BlankError_ShouldThrow(string? error) =>
    Should.Throw<ArgumentException>(() => Hook().MarkForbidden(error!, Now));

  private static ForgeHook Hook() => ForgeHook.Create("conn-1", ForgeHookScopeKind.Project, "project", Now);

  #endregion
}
