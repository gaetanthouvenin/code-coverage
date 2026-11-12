// ------------------------------------------------------------------------------------------------
// <copyright file="UpdateOrganizationSettingCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Organizations.Commands.ResetOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Commands.UpdateOrganizationSetting;
using CodeCoverage.Application.Features.Organizations.Queries.GetOrganizationSettings;
using CodeCoverage.Application.Features.Settings;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Settings;

namespace CodeCoverage.Application.Tests.Features.Organizations.Commands.UpdateOrganizationSetting;

public sealed class UpdateOrganizationSettingCommandHandlerTests
{
  #region Fields

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly IOrganizationSettingRepository _settings = A.Fake<IOrganizationSettingRepository>();

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly UpdateOrganizationSettingCommandHandler _handler;

  #endregion

  #region Ctors

  public UpdateOrganizationSettingCommandHandlerTests()
  {
    var clock = A.Fake<IClock>();

    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _currentUser.Login).Returns("octocat");

    _handler = new UpdateOrganizationSettingCommandHandler(_settings, _currentUser, _auditLog, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  // A setting of the instance is changed by an administrator of the instance only, never through an organization.
  [Theory]
  [InlineData(SettingDefinitions.AccessAdminUserIds)]
  [InlineData("Unknown:Key")]
  public async Task Handle_InstanceKey_ShouldThrow(string key)
  {
    var exception = await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new UpdateOrganizationSettingCommand(Forge.GitHub, "octo-org", key, "1"), Ct));

    exception.ErrorCode.ShouldBe(SettingErrors.Unknown);
    A.CallTo(_settings).MustNotHaveHappened();
  }

  // The repository writes the organization of the scope, entered by the gate from the address: no organization is
  // passed, so none other can be written.
  [Fact]
  public async Task Handle_ShouldWriteOnlyCurrentOrganization()
  {
    await _handler.Handle(new UpdateOrganizationSettingCommand(Forge.GitHub, "octo-org", "notifications:statuscontextprefix", "codecov"), Ct);

    A.CallTo(() => _settings.SaveAsync(A<SettingDefinition>.That.Matches(d => d.Key == SettingDefinitions.NotificationsStatusContextPrefix), "codecov", "octocat", Now, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(UpdateOrganizationSettingCommandHandler.AuditAction, SettingDefinitions.NotificationsStatusContextPrefix, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
  }

  [Theory]
  [InlineData(SettingDefinitions.NotificationsStatusesEnabled, "maybe", false)]
  [InlineData(SettingDefinitions.NotificationsStatusesEnabled, "false", true)]
  [InlineData(SettingDefinitions.AccessCacheMinutes, "not-a-number", true)]
  public void Validator_ShouldCheckOnlyTheValuesOfAnOrganization(string key, string value, bool valid)
  {
    new UpdateOrganizationSettingCommandValidator().Validate(new UpdateOrganizationSettingCommand(Forge.GitHub, "octo-org", key, value)).IsValid.ShouldBe(valid);
  }

  [Fact]
  public async Task Reset_StoredValue_ShouldRemoveItAndRecordIt()
  {
    A.CallTo(() => _settings.RemoveAsync(A<SettingDefinition>._, A<CancellationToken>._)).Returns(true);

    await new ResetOrganizationSettingCommandHandler(_settings, _auditLog).Handle(new ResetOrganizationSettingCommand(Forge.GitHub, "octo-org", SettingDefinitions.CoverageOrganizationYaml), Ct);

    A.CallTo(() => _auditLog.RecordAsync(ResetOrganizationSettingCommandHandler.AuditAction, SettingDefinitions.CoverageOrganizationYaml, null, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Reset_AlreadyDefault_ShouldRecordNothing()
  {
    A.CallTo(() => _settings.RemoveAsync(A<SettingDefinition>._, A<CancellationToken>._)).Returns(false);

    await new ResetOrganizationSettingCommandHandler(_settings, _auditLog).Handle(new ResetOrganizationSettingCommand(Forge.GitHub, "octo-org", SettingDefinitions.CoverageOrganizationYaml), Ct);

    A.CallTo(_auditLog).MustNotHaveHappened();
  }

  [Fact]
  public async Task Get_ShouldListTheFourKeysWithTheirSource()
  {
    var prefix = SettingDefinitions.Find(SettingDefinitions.NotificationsStatusContextPrefix)!;
    A.CallTo(() => _settings.ListAsync(A<CancellationToken>._)).Returns([SettingValue.Create(prefix, "codecov", SettingSource.Store, Now, "octocat")]);

    var values = await new GetOrganizationSettingsQueryHandler(_settings).Handle(new GetOrganizationSettingsQuery(Forge.GitHub, "octo-org"), Ct);

    values.Count.ShouldBe(4);
    values.Single(v => v.Key == prefix.Key).Source.ShouldBe(SettingSource.Store);
    values.Where(v => v.Key != prefix.Key).ShouldAllBe(v => v.Source == SettingSource.Default);
  }

  #endregion
}
