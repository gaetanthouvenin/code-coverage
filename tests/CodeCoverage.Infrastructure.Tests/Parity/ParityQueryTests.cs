// ------------------------------------------------------------------------------------------------
// <copyright file="ParityQueryTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.EntityFrameworkCore;

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Git;
using CodeCoverage.Infrastructure.Persistence;
using CodeCoverage.Infrastructure.Persistence.Repositories;
using CodeCoverage.Infrastructure.Tests.Support;

using static CodeCoverage.Infrastructure.Tests.Support.RepositorySeeder;

namespace CodeCoverage.Infrastructure.Tests.Parity;

// The query of tools/parity/sql, the read side of Compare-TotalsWithCodecov.ps1, on both engines: the same rows in
// the same shape, and nothing written.
[Collection(DatabaseCollection.Name)]
public sealed partial class ParityQueryTests(DatabaseFixture database)
{
  #region Fields

  private static readonly GitSha Other = GitSha.Parse("c94a8fe5ccb19ba61c4c0873d391e987982fbbd3");

  #endregion

  #region Properties

  private static CancellationToken Ct => TestContext.Current.CancellationToken;

  #endregion

  #region Methods

  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Query_ShouldReturnTheTotalsOfEveryCommitAndFlag(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);
    var first = seed.Organizations.FirstId;
    var second = seed.Organizations.SecondId;

    await AddTotalsAsync(seed, first, seed.FirstRepository, Head, (CommitFlagTotals.ProjectFlag, new CoverageTotals(12, 1, 1, 1, 0), false), ("back", new CoverageTotals(9, 1, 0, 0, 0), true));
    await AddTotalsAsync(seed, first, seed.FirstRepository, Other, (CommitFlagTotals.ProjectFlag, new CoverageTotals(0, 0, 0, 0, 0), false));
    await AddTotalsAsync(seed, second, seed.SecondRepository, Head, (CommitFlagTotals.ProjectFlag, new CoverageTotals(1, 13, 0, 0, 0), false));

    var rows = await RunAsync(seed, provider, readOnly: false);

    rows.Select(Describe).ShouldBe(
      [
        $"GitHub|first-org||repo|{Head.Value}|Complete||14|12|1|1|85.71429|False",
        $"GitHub|first-org||repo|{Head.Value}|Complete|back|10|9|1|0|90.00000|True",
        $"GitHub|first-org||repo|{Other.Value}|Complete||0|0|0|0||False",
        $"GitHub|second-org||repo|{Head.Value}|Complete||14|1|13|0|7.14286|False",
      ]
    );
  }

  // The script reads « [] » for a database without any finalized commit: one value, never an empty output.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Query_WithoutAnyTotals_ShouldReturnAnEmptyArray(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    (await RunAsync(seed, provider, readOnly: false)).ShouldBeEmpty();
  }

  // A value far longer than the 2 033 characters SQL Server cuts FOR JSON at: the query still gives one whole value.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public async Task Query_ManyRows_ShouldStayOneWholeJsonValue(DatabaseProvider provider)
  {
    var seed = await AddRepositoriesAsync(database, provider, Ct);

    (string Flag, CoverageTotals Totals, bool Carried)[] flags =
      [.. Enumerable.Range(1, 60).Select(static i => ($"flag-{i:000}-with-a-rather-long-name", new CoverageTotals(i, 1, 0, 0, 0), false))];

    await AddTotalsAsync(seed, seed.Organizations.FirstId, seed.FirstRepository, Head, flags);

    var rows = await RunAsync(seed, provider, readOnly: false);

    rows.Count.ShouldBe(60);
    rows.Select(static row => row.GetProperty("flag").GetString()).ShouldBe(flags.Select(static flag => flag.Flag));
  }

  // PostgreSQL refuses any write in a read-only transaction: the query runs in one, as the script opens its session.
  [Fact]
  public async Task Query_InAReadOnlyTransaction_ShouldRunOnPostgreSql()
  {
    var seed = await AddRepositoriesAsync(database, DatabaseProvider.PostgreSql, Ct);

    await AddTotalsAsync(seed, seed.Organizations.FirstId, seed.FirstRepository, Head, (CommitFlagTotals.ProjectFlag, new CoverageTotals(1, 1, 0, 0, 0), false));

    (await RunAsync(seed, DatabaseProvider.PostgreSql, readOnly: true)).ShouldHaveSingleItem();
  }

  // What the engines cannot both enforce is checked on the text: one statement, a SELECT, and no keyword that writes.
  [Theory]
  [ClassData(typeof(ProviderTheoryData))]
  public void Query_ShouldBeASingleSelect(DatabaseProvider provider)
  {
    var sql = CommentPattern().Replace(Read(provider), string.Empty).Trim();

    sql.ShouldStartWith("SELECT");
    sql.TrimEnd(';').ShouldNotContain(";");
    WritePattern().IsMatch(sql).ShouldBeFalse(sql);
  }

  private static string Read(DatabaseProvider provider) =>
    File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "parity", $"commit-flag-totals.{provider.ToString().ToLowerInvariant()}.sql"));

  private static string Describe(JsonElement row)
  {
    var coverage = row.GetProperty("coverage");

    return string.Join(
      '|',
      row.GetProperty("forge").GetString(),
      row.GetProperty("owner").GetString(),
      row.GetProperty("namespace").GetString(),
      row.GetProperty("name").GetString(),
      row.GetProperty("sha").GetString(),
      row.GetProperty("state").GetString(),
      row.GetProperty("flag").GetString(),
      row.GetProperty("lines").GetInt32(),
      row.GetProperty("hits").GetInt32(),
      row.GetProperty("misses").GetInt32(),
      row.GetProperty("partials").GetInt32(),
      coverage.ValueKind is JsonValueKind.Null ? string.Empty : coverage.GetDecimal().ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
      row.GetProperty("isCarriedForward").GetBoolean()
    );
  }

  private static async Task<IReadOnlyList<JsonElement>> RunAsync(BranchSeed seed, DatabaseProvider provider, bool readOnly)
  {
    // As the operator does: outside the application, so outside any organization.
    await using var context = seed.Organizations.ContextWithoutOrganization();
    var connection = context.Database.GetDbConnection();

    await connection.OpenAsync(Ct);

    if (readOnly)
    {
      await using var setting = connection.CreateCommand();
      setting.CommandText = "SET default_transaction_read_only = on";
      await setting.ExecuteNonQueryAsync(Ct);
    }

    await using var command = connection.CreateCommand();
    command.CommandText = Read(provider);

    // sqlcmd and psql print the value as text: it is read the same way here, a row at a time.
    var json = new System.Text.StringBuilder();

    await using (var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, Ct))
    {
      while (await reader.ReadAsync(Ct))
      {
        json.Append(reader.GetString(0));
      }
    }

    using var document = JsonDocument.Parse(json.ToString());

    return [.. document.RootElement.EnumerateArray().Select(static row => row.Clone())];
  }

  private static async Task AddTotalsAsync(
    BranchSeed seed,
    long organizationId,
    long repositoryId,
    GitSha sha,
    params (string Flag, CoverageTotals Totals, bool Carried)[] flags)
  {
    await using var context = seed.Organizations.ContextOf(organizationId);
    var commit = await new CommitRepository(context).GetOrCreateAsync(repositoryId, sha, Now, Ct);

    commit.RecordFinalization(CommitState.Complete, 1, flags[0].Totals, 1, 1, CarryforwardStatus.None, "default", Now);
    context.CommitFlagTotals.AddRange(flags.Select(flag => CommitFlagTotals.Create(commit, flag.Flag, flag.Totals, flag.Carried, null)));

    await context.SaveChangesAsync(Ct);
  }

  [GeneratedRegex(@"--[^\n]*")]
  private static partial Regex CommentPattern();

  [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE|GRANT|EXEC|EXECUTE|INTO|CALL|COPY)\b", RegexOptions.IgnoreCase)]
  private static partial Regex WritePattern();

  #endregion
}
