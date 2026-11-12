// ------------------------------------------------------------------------------------------------
// <copyright file="CreateApiTokenCommandHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Exceptions;
using CodeCoverage.Application.Features.Access;
using CodeCoverage.Application.Features.Access.Commands.CreateApiToken;
using CodeCoverage.Application.Features.Organizations;
using CodeCoverage.Application.Tests.Support;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Domain.Tokens;

namespace CodeCoverage.Application.Tests.Features.Access.Commands.CreateApiToken;

public sealed class CreateApiTokenCommandHandlerTests
{
  #region Fields

  private const string Value = "cvp_abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG";

  private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

  private readonly ICurrentUser _currentUser = A.Fake<ICurrentUser>();

  private readonly IApiTokenRepository _tokens = A.Fake<IApiTokenRepository>();

  private readonly InMemoryOrganizationDirectory _directory = new(Now);

  private readonly IOrganizationRoleResolver _roles = A.Fake<IOrganizationRoleResolver>();

  private readonly IAuditLog _auditLog = A.Fake<IAuditLog>();

  private readonly CreateApiTokenCommandHandler _handler;

  private readonly Organization _organization;

  private ApiToken? _added;

  #endregion

  #region Ctors

  public CreateApiTokenCommandHandlerTests()
  {
    var generator = A.Fake<ITokenGenerator>();
    var hasher = A.Fake<ITokenHasher>();
    var clock = A.Fake<IClock>();

    A.CallTo(() => _currentUser.UserId).Returns(42L);
    A.CallTo(() => _currentUser.IsAuthenticated).Returns(true);
    A.CallTo(() => generator.NewApiToken()).Returns(Value);
    A.CallTo(() => hasher.Hash(Value)).Returns(new byte[ApiToken.HashLength]);
    A.CallTo(() => clock.UtcNow).Returns(Now);
    A.CallTo(() => _tokens.AddAsync(A<ApiToken>._, A<CancellationToken>._)).Invokes((ApiToken token, CancellationToken _) => _added = token);
    _organization = _directory.Add("1001", "octo-org", admitted: true);

    _handler = new CreateApiTokenCommandHandler(_currentUser, _tokens, _directory, _roles, generator, hasher, _auditLog, clock);
  }

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [InlineData(OrganizationRole.Participant)]
  [InlineData(OrganizationRole.Admin)]
  public async Task Handle_MemberOfTheOrganization_ShouldIssueATokenOfThatOrganization(OrganizationRole role)
  {
    A.CallTo(() => _roles.ResolveAsync(42, _organization.Id, A<CancellationToken>._)).Returns(role);

    var issued = await _handler.Handle(new CreateApiTokenCommand(" ci ", Forge.GitHub, "octo-org", Now.AddDays(30)), Ct);

    issued.Value.ShouldBe(Value);
    issued.ToString().ShouldNotContain(Value);
    var token = _added.ShouldNotBeNull();
    token.UserId.ShouldBe(42);
    token.OrganizationId.ShouldBe(_organization.Id);
    token.Name.ShouldBe("ci");
    token.Hint.ShouldBe("cvp_abcd");
    token.ExpiresAt.ShouldBe(Now.AddDays(30));

    // Fail-closed: the event of a token bound to an organization belongs to that organization, never to the instance.
    A.CallTo(() => _auditLog.RecordAsync(_organization.Id, CreateApiTokenCommandHandler.AuditAction, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._))
     .MustHaveHappenedOnceExactly();
    A.CallTo(() => _auditLog.RecordAsync(A<string>._, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._)).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_NotAMember_ShouldThrowOrganizationNotFound()
  {
    A.CallTo(() => _roles.ResolveAsync(42, _organization.Id, A<CancellationToken>._)).Returns((OrganizationRole?)null);

    var exception = await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new CreateApiTokenCommand("ci", Forge.GitHub, "octo-org", null), Ct));

    exception.ErrorCode.ShouldBe(OrganizationErrors.NotFound);
    _added.ShouldBeNull();
  }

  [Fact]
  public async Task Handle_PendingOrUnknownOrganization_ShouldThrowOrganizationNotFound()
  {
    _directory.Add("2002", "pending-org");

    await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new CreateApiTokenCommand("ci", Forge.GitHub, "pending-org", null), Ct));
    await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new CreateApiTokenCommand("ci", Forge.GitHub, "nobody", null), Ct));

    A.CallTo(_roles).MustNotHaveHappened();
  }

  [Fact]
  public async Task Handle_InstanceTokenByAnAdministrator_ShouldIssueATokenOfTheInstance()
  {
    A.CallTo(() => _currentUser.IsAdmin).Returns(true);

    await _handler.Handle(new CreateApiTokenCommand("ops", null, null, null), Ct);

    _added.ShouldNotBeNull().IsInstanceToken.ShouldBeTrue();
    A.CallTo(() => _auditLog.RecordAsync(CreateApiTokenCommandHandler.AuditAction, A<string>._, A<IReadOnlyDictionary<string, string>?>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
  }

  [Fact]
  public async Task Handle_InstanceTokenByAnotherUser_ShouldThrowForbidden()
  {
    await Should.ThrowAsync<ForbiddenException>(async () => await _handler.Handle(new CreateApiTokenCommand("ops", null, null, null), Ct));

    _added.ShouldBeNull();
  }

  // A request authenticated by a token stays within it.
  [Fact]
  public async Task Handle_FromATokenOfAnotherOrganization_ShouldThrowOrganizationNotFound()
  {
    A.CallTo(() => _currentUser.TokenOrganizationId).Returns(_organization.Id + 1);
    A.CallTo(() => _roles.ResolveAsync(42, _organization.Id, A<CancellationToken>._)).Returns(OrganizationRole.Admin);

    await Should.ThrowAsync<NotFoundException>(async () => await _handler.Handle(new CreateApiTokenCommand("ci", Forge.GitHub, "octo-org", null), Ct));
  }

  [Fact]
  public async Task Handle_InstanceTokenFromATokenOfAnOrganization_ShouldThrowForbidden()
  {
    A.CallTo(() => _currentUser.IsAdmin).Returns(true);
    A.CallTo(() => _currentUser.TokenOrganizationId).Returns(_organization.Id);

    await Should.ThrowAsync<ForbiddenException>(async () => await _handler.Handle(new CreateApiTokenCommand("ops", null, null, null), Ct));
  }

  [Fact]
  public async Task Handle_OrganizationTokenFromATokenOfTheInstance_ShouldThrowForbidden()
  {
    A.CallTo(() => _currentUser.IsInstanceToken).Returns(true);
    A.CallTo(() => _roles.ResolveAsync(42, _organization.Id, A<CancellationToken>._)).Returns(OrganizationRole.Admin);

    await Should.ThrowAsync<ForbiddenException>(async () => await _handler.Handle(new CreateApiTokenCommand("ci", Forge.GitHub, "octo-org", null), Ct));
  }

  [Theory]
  [InlineData("", "GitHub", "octo-org", 1, AccessErrors.InvalidApiTokenName)]
  [InlineData("ci", "GitHub", null, 1, AccessErrors.InvalidApiTokenOrganization)]
  [InlineData("ci", null, "octo-org", 1, AccessErrors.InvalidApiTokenOrganization)]
  [InlineData("ci", null, null, -1, AccessErrors.InvalidApiTokenExpiry)]
  public void Validator_InvalidCommand_ShouldReportTheCode(string name, string? forge, string? organization, int days, string code)
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);
    var command = new CreateApiTokenCommand(name, forge is null ? null : Enum.Parse<Forge>(forge), organization, Now.AddDays(days));

    var result = new CreateApiTokenCommandValidator(clock).Validate(command);

    result.Errors.Select(static e => e.ErrorCode).ShouldBe([code]);
  }

  #endregion
}
