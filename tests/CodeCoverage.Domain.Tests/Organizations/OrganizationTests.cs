// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Tests.Organizations;

public sealed class OrganizationTests
{
  #region Fields

  private const string AccountId = "583231";

  private static readonly DateTimeOffset At = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  private static readonly DateTimeOffset Later = At.AddHours(1);

  #endregion

  #region Methods

  [Fact]
  public void CreatePending_ShouldKeepTheAccountAndNormalizeTheLogin()
  {
    var organization = Pending("Octo-Org");

    organization.Forge.ShouldBe(Forge.GitHub);
    organization.ExternalAccountId.ShouldBe(AccountId);
    organization.Login.ShouldBe("Octo-Org");
    organization.NormalizedLogin.ShouldBe("octo-org");
    organization.AccountType.ShouldBe(OrganizationAccountType.Organization);
    organization.State.ShouldBe(OrganizationState.Pending);
    organization.SuspensionReason.ShouldBeNull();
    organization.AdmittedBy.ShouldBeNull();
    organization.CreatedAt.ShouldBe(At);
    organization.UpdatedAt.ShouldBe(At);
    organization.Version.ShouldBe(0);
  }

  [Fact]
  public void CreatePending_MalformedLogin_ShouldThrow()
  {
    Should.Throw<ArgumentException>(
      () => Organization.CreatePending(Forge.GitHub, AccountId, "~stale-1", OrganizationAccountType.User, At)
    );
  }

  [Fact]
  public void CreatePending_MixedCaseAccountId_ShouldStoreLowerCase()
  {
    var organization = Organization.CreatePending(
      Forge.Bitbucket,
      "{A1B2C3D4-0000-4000-8000-ABCDEFABCDEF}",
      "octo-workspace",
      OrganizationAccountType.Organization,
      At
    );

    organization.Forge.ShouldBe(Forge.Bitbucket);
    organization.ExternalAccountId.ShouldBe("{a1b2c3d4-0000-4000-8000-abcdefabcdef}");
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void CreatePending_BlankAccountId_ShouldThrow(string accountId)
  {
    Should.Throw<ArgumentException>(
      () => Organization.CreatePending(Forge.GitHub, accountId, "octo-org", OrganizationAccountType.User, At)
    );
  }

  [Fact]
  public void CreatePending_TooLongAccountId_ShouldThrow()
  {
    var accountId = new string('a', Organization.ExternalAccountIdMaxLength + 1);

    Should.Throw<ArgumentException>(
      () => Organization.CreatePending(Forge.GitHub, accountId, "octo-org", OrganizationAccountType.User, At)
    );
  }

  [Fact]
  public void Admit_FromPending_ShouldBecomeActive()
  {
    var organization = Pending("octo-org");

    organization.Admit("admin-user", Later);

    organization.State.ShouldBe(OrganizationState.Active);
    organization.AdmittedBy.ShouldBe("admin-user");
    organization.UpdatedAt.ShouldBe(Later);
    organization.Version.ShouldBe(1);
  }

  [Theory]
  [InlineData("admin")]
  [InlineData("API")]
  public void Admit_ReservedLogin_ShouldThrow(string login)
  {
    var organization = Pending(login);

    Should.Throw<InvalidOperationException>(() => organization.Admit("admin-user", Later));

    organization.State.ShouldBe(OrganizationState.Pending);
    organization.Version.ShouldBe(0);
  }

  [Fact]
  public void Admit_NotPending_ShouldThrow()
  {
    var organization = Active("octo-org");

    Should.Throw<InvalidOperationException>(() => organization.Admit("admin-user", Later));
  }

  [Fact]
  public void Admit_TooLongAdmittedBy_ShouldThrow()
  {
    var organization = Pending("octo-org");

    Should.Throw<ArgumentException>(() => organization.Admit(new string('a', 65), Later));
  }

  [Fact]
  public void Suspend_ShouldRecordTheReason()
  {
    var organization = Active("octo-org");

    organization.Suspend(SuspensionReason.Administrator, Later);

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.Administrator);
  }

  [Fact]
  public void Suspend_AlreadySuspended_ShouldKeepFirstReason()
  {
    var organization = Active("octo-org");
    organization.Suspend(SuspensionReason.Administrator, Later);
    var version = organization.Version;

    organization.Suspend(SuspensionReason.AppUninstalled, Later.AddHours(1));

    organization.SuspensionReason.ShouldBe(SuspensionReason.Administrator);
    organization.Version.ShouldBe(version);
  }

  [Fact]
  public void Suspend_AdministratorAfterAppUninstalled_ShouldReplaceTheReason()
  {
    var organization = Active("octo-org");
    organization.Suspend(SuspensionReason.AppUninstalled, Later);
    var version = organization.Version;

    organization.Suspend(SuspensionReason.Administrator, Later.AddHours(1));

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.Administrator);
    organization.Version.ShouldBe(version + 1);
  }

  [Fact]
  public void Suspend_ReservedLoginAfterAppUninstalled_ShouldKeepFirstReason()
  {
    var organization = Active("octo-org");
    organization.Suspend(SuspensionReason.AppUninstalled, Later);

    organization.Suspend(SuspensionReason.ReservedLogin, Later.AddHours(1));

    organization.SuspensionReason.ShouldBe(SuspensionReason.AppUninstalled);
  }

  [Fact]
  public void Reactivate_AdmittedAndAppUninstalled_ShouldBeActiveAgain()
  {
    var organization = Active("octo-org");
    organization.Suspend(SuspensionReason.AppUninstalled, Later);
    var version = organization.Version;

    organization.Reactivate(Later.AddHours(1)).ShouldBeTrue();

    organization.State.ShouldBe(OrganizationState.Active);
    organization.SuspensionReason.ShouldBeNull();
    organization.UpdatedAt.ShouldBe(Later.AddHours(1));
    organization.Version.ShouldBe(version + 1);
  }

  [Fact]
  public void Reactivate_NeverAdmitted_ShouldBePendingNotActive()
  {
    var organization = Pending("octo-org");
    organization.Suspend(SuspensionReason.AppUninstalled, Later);

    organization.Reactivate(Later.AddHours(1)).ShouldBeTrue();

    organization.State.ShouldBe(OrganizationState.Pending);
    organization.SuspensionReason.ShouldBeNull();
    organization.AdmittedBy.ShouldBeNull();
  }

  [Theory]
  [InlineData(SuspensionReason.Administrator)]
  [InlineData(SuspensionReason.ReservedLogin)]
  public void Reactivate_OtherReason_ShouldStaySuspended(SuspensionReason reason)
  {
    var organization = Active("octo-org");
    organization.Suspend(reason, Later);
    var version = organization.Version;

    organization.Reactivate(Later.AddHours(1)).ShouldBeFalse();

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(reason);
    organization.Version.ShouldBe(version);
  }

  [Fact]
  public void Reactivate_AdministratorAfterAppUninstalled_ShouldStaySuspended()
  {
    var organization = Active("octo-org");
    organization.Suspend(SuspensionReason.AppUninstalled, Later);
    organization.Suspend(SuspensionReason.Administrator, Later);

    organization.Reactivate(Later.AddHours(1)).ShouldBeFalse();

    organization.State.ShouldBe(OrganizationState.Suspended);
  }

  [Fact]
  public void Reactivate_NotSuspended_ShouldChangeNothing()
  {
    var organization = Active("octo-org");
    var version = organization.Version;

    organization.Reactivate(Later).ShouldBeFalse();

    organization.State.ShouldBe(OrganizationState.Active);
    organization.Version.ShouldBe(version);
  }

  [Fact]
  public void Reactivate_LoginBecameReserved_ShouldStaySuspendedForThatReason()
  {
    var organization = Active("octo-org");
    organization.Suspend(SuspensionReason.AppUninstalled, Later);
    organization.Rename("Setup", Later);

    organization.Reactivate(Later.AddHours(1)).ShouldBeFalse();

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.ReservedLogin);
  }

  [Fact]
  public void Rename_ShouldKeepExternalAccountId()
  {
    var organization = Active("octo-org");

    organization.Rename("New-Name", Later);

    organization.ExternalAccountId.ShouldBe(AccountId);
    organization.Login.ShouldBe("New-Name");
    organization.NormalizedLogin.ShouldBe("new-name");
    organization.State.ShouldBe(OrganizationState.Active);
    organization.UpdatedAt.ShouldBe(Later);
  }

  [Fact]
  public void Rename_ReservedLogin_ShouldSuspend()
  {
    var organization = Active("octo-org");

    organization.Rename("Setup", Later);

    organization.State.ShouldBe(OrganizationState.Suspended);
    organization.SuspensionReason.ShouldBe(SuspensionReason.ReservedLogin);
    organization.Login.ShouldBe("Setup");
  }

  [Fact]
  public void Rename_PendingToReservedLogin_ShouldStayPending()
  {
    var organization = Pending("octo-org");

    organization.Rename("setup", Later);

    organization.State.ShouldBe(OrganizationState.Pending);
    organization.SuspensionReason.ShouldBeNull();
  }

  [Fact]
  public void Rename_SameLogin_ShouldNotChangeVersion()
  {
    var organization = Active("octo-org");
    var version = organization.Version;

    organization.Rename("octo-org", Later);

    organization.Version.ShouldBe(version);
  }

  [Fact]
  public void Rename_MalformedLogin_ShouldThrow()
  {
    var organization = Active("octo-org");

    Should.Throw<ArgumentException>(() => organization.Rename("~stale-1", Later));

    organization.Login.ShouldBe("octo-org");
  }

  [Fact]
  public void ChangeAccountType_ShouldApplyIt()
  {
    var organization = Pending("octo-org");

    organization.ChangeAccountType(OrganizationAccountType.User, Later);

    organization.AccountType.ShouldBe(OrganizationAccountType.User);
    organization.Version.ShouldBe(1);
  }

  [Fact]
  public void Tombstone_ShouldKeepStateAndUseAccountId()
  {
    var organization = Active("octo-org");

    organization.Tombstone(Later);

    organization.Login.ShouldBe($"~stale-{AccountId}");
    organization.NormalizedLogin.ShouldBe($"~stale-{AccountId}");
    organization.State.ShouldBe(OrganizationState.Active);
    Organization.IsWellFormedLogin(organization.Login).ShouldBeFalse();
  }

  [Fact]
  public void Tombstone_LongestAccountId_ShouldFitTheColumn()
  {
    var accountId = new string('a', Organization.ExternalAccountIdMaxLength);
    var organization = Organization.CreatePending(Forge.GitHub, accountId, "octo-org", OrganizationAccountType.User, At);

    organization.Tombstone(Later);

    organization.Login.Length.ShouldBeLessThanOrEqualTo(Organization.LoginMaxLength);
  }

  [Theory]
  [InlineData("octo", true)]
  [InlineData("Octo-Org-42", true)]
  [InlineData("octo_corp", true)]
  [InlineData("octo.group", true)]
  [InlineData("a", true)]
  [InlineData("trailing-", true)]
  [InlineData(null, false)]
  [InlineData("", false)]
  [InlineData("-leading", false)]
  [InlineData("_ui", false)]
  [InlineData("~stale-1", false)]
  [InlineData("with space", false)]
  [InlineData("octo/org", false)]
  [InlineData("group:subgroup", false)]
  [InlineData(".hidden", false)]
  [InlineData("octé", false)]
  public void IsWellFormedLogin_ShouldFollowTheCommonAlphabet(string? login, bool expected)
  {
    Organization.IsWellFormedLogin(login).ShouldBe(expected);
  }

  [Fact]
  public void IsWellFormedLogin_ShouldBoundTheLength()
  {
    Organization.IsWellFormedLogin(new string('a', Organization.LoginMaxLength)).ShouldBeTrue();
    Organization.IsWellFormedLogin(new string('a', Organization.LoginMaxLength + 1)).ShouldBeFalse();
  }

  private static Organization Pending(string login) =>
    Organization.CreatePending(Forge.GitHub, AccountId, login, OrganizationAccountType.Organization, At);

  private static Organization Active(string login)
  {
    var organization = Pending(login);
    organization.Admit("admin-user", At);

    return organization;
  }

  #endregion
}
