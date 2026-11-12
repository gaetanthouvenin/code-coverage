// ------------------------------------------------------------------------------------------------
// <copyright file="ListOrganizationsQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Organizations.Queries.ListOrganizations;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Organizations.Queries.ListOrganizations;

public sealed class ListOrganizationsQueryHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly IOrganizationDirectory _directory = A.Fake<IOrganizationDirectory>();

  private readonly IJobMonitor _jobs = A.Fake<IJobMonitor>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // The acceptance of M3.41a: the read model holds the row of the organization and a count, nothing of its data.
  [Fact]
  public void Handle_ShouldExposeNoBusinessData()
  {
    typeof(OrganizationOverview).GetProperties().Select(static p => p.Name).Order(StringComparer.Ordinal).ShouldBe(["Organization", "QueueDepth"]);

    var forbidden = new[] { "Repositor", "Token", "Yaml", "Setting", "Commit", "Coverage", "Report", "Secret" };
    typeof(Organization).GetProperties()
                        .Select(static p => p.Name)
                        .Where(name => forbidden.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)))
                        .ShouldBeEmpty();
  }

  // Decision 20: the rows a renaming tombstoned are hidden, a suspension by uninstallation is shown; depth 0 by default.
  [Fact]
  public async Task Handle_ShouldListEveryStateWithTheDepthOfItsQueue()
  {
    var active = Organization(1, "1001", "zeta-org");
    active.Admit("admin", Now);
    var pending = Organization(2, "1002", "acme");
    var uninstalled = Organization(3, "1003", "blue-harbor");
    uninstalled.Admit("admin", Now);
    uninstalled.Suspend(SuspensionReason.AppUninstalled, Now);
    var tombstoned = Organization(4, "1004", "renamed-away");
    tombstoned.Tombstone(Now);
    A.CallTo(() => _directory.ListAllAsync(A<CancellationToken>._)).Returns([active, pending, uninstalled, tombstoned]);
    A.CallTo(() => _jobs.CountQueuedByOrganizationAsync(A<CancellationToken>._)).Returns(new Dictionary<long, int> { [1] = 6 });

    var listed = await new ListOrganizationsQueryHandler(_directory, _jobs).Handle(new ListOrganizationsQuery(), Ct);

    listed.Select(static o => o.Organization.Login).ShouldBe(["acme", "blue-harbor", "zeta-org"]);
    listed.Single(static o => o.Organization.Login == "zeta-org").QueueDepth.ShouldBe(6);
    listed.Single(static o => o.Organization.Login == "acme").QueueDepth.ShouldBe(0);
    listed.Single(static o => o.Organization.Login == "blue-harbor").Organization.SuspensionReason.ShouldBe(SuspensionReason.AppUninstalled);
  }

  private static Organization Organization(long id, string account, string login)
  {
    var organization = Domain.Organizations.Organization.CreatePending(Forge.GitHub, account, login, OrganizationAccountType.Organization, Now);
    typeof(Organization).GetProperty(nameof(Domain.Organizations.Organization.Id))!.SetValue(organization, id);

    return organization;
  }

  #endregion
}
