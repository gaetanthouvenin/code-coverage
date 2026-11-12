// ------------------------------------------------------------------------------------------------
// <copyright file="ListAuditEntriesQueryHandlerTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Application.Features.Operations;
using CodeCoverage.Application.Features.Operations.Queries.ListAuditEntries;
using CodeCoverage.Domain.Audit;
using CodeCoverage.Domain.Ports;

namespace CodeCoverage.Application.Tests.Features.Operations.Queries.ListAuditEntries;

public sealed class ListAuditEntriesQueryHandlerTests
{
  #region Fields

  private readonly IAuditReader _reader = A.Fake<IAuditReader>();

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Handle_ShouldReadTheInstanceAuditWithTheFilter()
  {
    var page = new AuditEntryPage([new AuditEntrySummary(1, DateTimeOffset.UnixEpoch, "octocat", "Jobs.Retry", "42", AuditCategory.Retries)], 2, 25, 26);
    A.CallTo(() => _reader.ListInstanceAsync(AuditCategory.Retries, "octo", 2, 25, A<CancellationToken>._)).Returns(page);

    var listed = await new ListAuditEntriesQueryHandler(_reader).Handle(new ListAuditEntriesQuery(AuditCategory.Retries, "octo", 2), Ct);

    listed.ShouldBe(page);
  }

  // Every bound answers 400 with a translated code: no unbounded page, no huge search.
  [Theory]
  [InlineData(0, 25, null, OperationsErrors.InvalidAuditPage)]
  [InlineData(ListAuditEntriesQuery.MaxPage + 1, 25, null, OperationsErrors.InvalidAuditPage)]
  [InlineData(1, 0, null, OperationsErrors.InvalidAuditPage)]
  [InlineData(1, ListAuditEntriesQuery.MaxPageSize + 1, null, OperationsErrors.InvalidAuditPage)]
  [InlineData(1, 25, 201, OperationsErrors.InvalidAuditSearch)]
  public void Validate_OutOfBounds_ShouldRefuseWithItsCode(int page, int pageSize, int? searchLength, string code)
  {
    var query = new ListAuditEntriesQuery(null, searchLength is { } length ? new string('a', length) : null, page, pageSize);

    var result = new ListAuditEntriesQueryValidator().Validate(query);

    result.Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(code);
  }

  [Fact]
  public void Validate_UnknownCategory_ShouldRefuse() =>
    new ListAuditEntriesQueryValidator().Validate(new ListAuditEntriesQuery((AuditCategory)42, null)).Errors.ShouldHaveSingleItem().ErrorCode.ShouldBe(OperationsErrors.InvalidAuditSearch);

  [Theory]
  [InlineData("Settings.ReplaceSecret", "GitHub:WebhookSecret", AuditCategory.Secrets)]
  [InlineData("Settings.Reset", "GitHub:WebhookSecret", AuditCategory.Secrets)]
  [InlineData("Settings.Reset", "Uploads:MaxFilesPerUpload", AuditCategory.Settings)]
  [InlineData("Settings.CompleteSetup", "setup", AuditCategory.Settings)]
  [InlineData("Settings.CompleteGitHubAppManifest", "184302", AuditCategory.Settings)]
  [InlineData("Organization.Admit", "GitHub/acme", AuditCategory.Organizations)]
  [InlineData("ApiTokens.Revoke", "5", AuditCategory.Tokens)]
  [InlineData("Jobs.Retry", "42", AuditCategory.Retries)]
  public void Category_ShouldFollowTheFamiliesOfTheAudit(string action, string target, AuditCategory expected) =>
    AuditCategories.Of(action, target).ShouldBe(expected);

  [Fact]
  public void Category_UnknownAction_ShouldBeNone() => AuditCategories.Of("Repositories.Activate", "7").ShouldBeNull();

  #endregion
}
