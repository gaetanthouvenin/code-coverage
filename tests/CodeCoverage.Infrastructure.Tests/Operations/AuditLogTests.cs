// ------------------------------------------------------------------------------------------------
// <copyright file="AuditLogTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Operations;
using CodeCoverage.Domain.Ports;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Operations;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Operations;

[Collection(DatabaseCollection.Name)]
public sealed class AuditLogTests(DatabaseFixture database)
{
  #region Fields

  // Never opened: the interceptor suppresses the save before the connection is needed.
  private const string UnusedConnection = "Host=localhost;Database=unused";

  private static readonly DateTimeOffset Now = new(2026, 11, 8, 10, 30, 0, TimeSpan.Zero);

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Fact]
  public async Task Record_AsSystem_ShouldStoreSystemActor()
  {
    var user = new CurrentUserContext();
    user.SetSystem();
    var interceptor = new CapturingSaveChangesInterceptor();
    await using var context = CreateCapturingContext(interceptor);

    await new AuditLog(context, user, Clock()).RecordAsync("Commits.Reprocess", "42", null, Ct);

    var entry = interceptor.Added.ShouldHaveSingleItem().ShouldBeOfType<AuditEntry>();
    entry.Actor.ShouldBe(AuditEntry.SystemActor);
    entry.Action.ShouldBe("Commits.Reprocess");
    entry.Target.ShouldBe("42");
    entry.At.ShouldBe(Now);
    entry.Details.ShouldBeEmpty();
    interceptor.SaveCount.ShouldBe(1);
  }

  [Fact]
  public async Task Record_AsUser_ShouldStoreTheLogin()
  {
    var user = new CurrentUserContext();
    user.SetUser(7, "octocat", isAdmin: true);
    var interceptor = new CapturingSaveChangesInterceptor();
    await using var context = CreateCapturingContext(interceptor);

    await new AuditLog(context, user, Clock()).RecordAsync(
      "Settings.Update",
      "Processing:WorkerConcurrency",
      new Dictionary<string, string> { ["Value"] = "8" },
      Ct
    );

    var entry = interceptor.Added.ShouldHaveSingleItem().ShouldBeOfType<AuditEntry>();
    entry.Actor.ShouldBe("octocat");
    entry.Details.ShouldBe(new Dictionary<string, string> { ["Value"] = "8" });
  }

  [Fact]
  public async Task Record_Anonymously_ShouldStoreAnonymousActor()
  {
    var interceptor = new CapturingSaveChangesInterceptor();
    await using var context = CreateCapturingContext(interceptor);

    await new AuditLog(context, new CurrentUserContext(), Clock()).RecordAsync("Settings.Update", "Key", null, Ct);

    interceptor.Added.ShouldHaveSingleItem().ShouldBeOfType<AuditEntry>().Actor.ShouldBe(AuditEntry.AnonymousActor);
  }

  [Theory]
  [InlineData("WebhookSecret")]
  [InlineData("clientSecret")]
  public async Task Record_WithSecretKey_ShouldThrowArgumentExceptionAndSaveNothing(string key)
  {
    var interceptor = new CapturingSaveChangesInterceptor();
    await using var context = CreateCapturingContext(interceptor);
    var log = new AuditLog(context, new CurrentUserContext(), Clock());

    await Should.ThrowAsync<ArgumentException>(
      () => log.RecordAsync("Settings.ReplaceSecret", "GitHub:WebhookSecret", new Dictionary<string, string> { [key] = "s3cr3t" }, Ct)
    );

    interceptor.SaveCount.ShouldBe(0);
    context.ChangeTracker.Entries().ShouldBeEmpty();
  }

  [Fact]
  public async Task Record_WithNullAction_ShouldThrowArgumentNullException()
  {
    await using var context = CreateCapturingContext(new CapturingSaveChangesInterceptor());
    var log = new AuditLog(context, new CurrentUserContext(), Clock());

    (await Should.ThrowAsync<ArgumentNullException>(() => log.RecordAsync(null!, "Key", null, Ct)))
      .ParamName.ShouldBe("action");
    (await Should.ThrowAsync<ArgumentNullException>(() => log.RecordAsync("Settings.Update", null!, null, Ct)))
      .ParamName.ShouldBe("target");
  }

  [Fact]
  public async Task Record_WhenTheSaveFails_ShouldDetachTheEntryAndRethrow()
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(DatabaseProvider.PostgreSql, UnusedConnection)
           .AddInterceptors(new FailingSaveChangesInterceptor());
    await using var context = new CodeCoverageDbContext(options.Options);
    var log = new AuditLog(context, new CurrentUserContext(), Clock());

    await Should.ThrowAsync<DbUpdateException>(() => log.RecordAsync("Settings.Update", "Key", null, Ct));

    context.ChangeTracker.Entries().ShouldBeEmpty();
  }

  // The entry goes through the migrated AuditEntries table and back, details included.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Record_ForEachProvider_ShouldRoundTripTheEntry(DatabaseProvider provider)
  {
    var connectionString = await database.NewDatabaseAsync(provider, Ct);
    var user = new CurrentUserContext();
    user.SetSystem();

    await using (var context = DatabaseFixture.CreateContext(provider, connectionString))
    {
      await context.Database.MigrateAsync(Ct);
      await new AuditLog(context, user, Clock()).RecordAsync(
        "Commits.Reprocess",
        "owner/repo@0123456789abcdef0123456789abcdef01234567",
        new Dictionary<string, string> { ["Reason"] = "manual", ["CommitId"] = "42" },
        Ct
      );
    }

    await using var check = DatabaseFixture.CreateContext(provider, connectionString);
    var entry = await check.AuditEntries.SingleAsync(Ct);

    entry.Id.ShouldBeGreaterThan(0);
    entry.At.ShouldBe(Now);
    entry.Actor.ShouldBe(AuditEntry.SystemActor);
    entry.Action.ShouldBe("Commits.Reprocess");
    entry.Target.ShouldBe("owner/repo@0123456789abcdef0123456789abcdef01234567");
    entry.Details.ShouldBe(new Dictionary<string, string> { ["CommitId"] = "42", ["Reason"] = "manual" });
  }

  private static IClock Clock()
  {
    var clock = A.Fake<IClock>();
    A.CallTo(() => clock.UtcNow).Returns(Now);

    return clock;
  }

  private static CodeCoverageDbContext CreateCapturingContext(CapturingSaveChangesInterceptor interceptor)
  {
    var options = new DbContextOptionsBuilder<CodeCoverageDbContext>();
    options.UseCodeCoverageDatabase(DatabaseProvider.PostgreSql, UnusedConnection).AddInterceptors(interceptor);

    return new CodeCoverageDbContext(options.Options);
  }

  #endregion
}
