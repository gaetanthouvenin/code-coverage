// ------------------------------------------------------------------------------------------------
// <copyright file="MemoryReportViewCacheTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using CodeCoverage.Domain.Coverage;
using CodeCoverage.Domain.Paths;
using CodeCoverage.Infrastructure.Caching;
using CodeCoverage.Infrastructure.Identity;
using CodeCoverage.Infrastructure.Tests.Support;

namespace CodeCoverage.Infrastructure.Tests.Caching;

public sealed class MemoryReportViewCacheTests : IDisposable
{
  #region Fields

  private static readonly PathPatternSet AllPaths = new([]);

  private static readonly SessionReport[] Sessions = [];

  private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero));

  private readonly MemoryCache _memory;

  #endregion

  #region Ctors

  public MemoryReportViewCacheTests()
  {
    _memory = new MemoryCache(Options.Create(new MemoryCacheOptions { Clock = new FakeSystemClock(_time) }));
  }

  #endregion

  #region Methods

  [Fact]
  public void GetOrCreate_SameSelection_ShouldBuildOnce()
  {
    var cache = CacheOf(1);

    var first = cache.GetOrCreate(7, 1, Sessions, ["unit", "api"], Paths("src/"));
    var second = cache.GetOrCreate(7, 1, Sessions, ["api", "unit", "api"], Paths("src/"));

    second.ShouldBeSameAs(first);
  }

  [Fact]
  public void GetOrCreate_OtherCommitRevisionFlagsOrPaths_ShouldBuildAgain()
  {
    var cache = CacheOf(1);

    ReportView[] views =
    [
      cache.GetOrCreate(7, 1, Sessions, ["unit"], Paths("src/")),
      cache.GetOrCreate(8, 1, Sessions, ["unit"], Paths("src/")),
      cache.GetOrCreate(7, 2, Sessions, ["unit"], Paths("src/")),
      cache.GetOrCreate(7, 1, Sessions, ["Unit"], Paths("src/")),
      cache.GetOrCreate(7, 1, Sessions, [], Paths("src/")),
      cache.GetOrCreate(7, 1, Sessions, ["unit"], Paths("tests/")),
      cache.GetOrCreate(7, 1, Sessions, ["unit"], Paths("!src/")),
      cache.GetOrCreate(7, 1, Sessions, ["unit"], AllPaths)
    ];

    Distinct(views).ShouldBe(8);
  }

  // The order of the patterns changes what a set keeps: two orders are two views.
  [Fact]
  public void GetOrCreate_PatternsInAnotherOrder_ShouldBuildAgain()
  {
    var cache = CacheOf(1);

    var first = cache.GetOrCreate(7, 1, Sessions, [], Paths("src/", "!src/gen/"));
    var second = cache.GetOrCreate(7, 1, Sessions, [], Paths("!src/gen/", "src/"));

    second.ShouldNotBeSameAs(first);
  }

  // A flag that looks like two flags joined is still one flag.
  [Fact]
  public void GetOrCreate_FlagsThatConcatenateAlike_ShouldNotShareAView()
  {
    var cache = CacheOf(1);

    ReportView[] views =
    [
      cache.GetOrCreate(7, 1, Sessions, ["a", "b"], AllPaths),
      cache.GetOrCreate(7, 1, Sessions, ["ab"], AllPaths),
      cache.GetOrCreate(7, 1, Sessions, ["a:b"], AllPaths)
    ];

    Distinct(views).ShouldBe(3);
  }

  // The configuration of the commit that reads a report decides the sessions `joined: false` leaves out: a base read
  // through two heads is two views.
  [Fact]
  public void GetOrCreate_SessionsLeftOutByJoined_ShouldNotShareAView()
  {
    var cache = CacheOf(1);

    var whole = cache.GetOrCreate(7, 1, [Session("unit", 1), Session("e2e", 2)], [], AllPaths);
    var part = cache.GetOrCreate(7, 1, [Session("unit", 1), Session("e2e", 2, false)], [], AllPaths);
    var other = cache.GetOrCreate(7, 1, [Session("unit", 1, false), Session("e2e", 2)], [], AllPaths);
    var none = cache.GetOrCreate(7, 1, [Session("unit", 1, false), Session("e2e", 2, false)], [], AllPaths);

    whole.Totals.Lines.ShouldBe(2);
    part.Report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem().Line.ShouldBe(1);
    other.Report.Files.ShouldHaveSingleItem().Lines.ShouldHaveSingleItem().Line.ShouldBe(2);
    none.Totals.Lines.ShouldBe(0);
  }

  // The same sessions left out, read again: the view is kept.
  [Fact]
  public void GetOrCreate_SameSessionsLeftOutByJoined_ShouldBuildOnce()
  {
    var cache = CacheOf(1);

    var first = cache.GetOrCreate(7, 1, [Session("unit", 1), Session("e2e", 2, false)], [], AllPaths);
    var second = cache.GetOrCreate(7, 1, [Session("unit", 1), Session("e2e", 2, false)], [], AllPaths);

    second.ShouldBeSameAs(first);
  }

  // A view with flags takes every session that carries one of them, joined or not: `joined` does not split it.
  [Fact]
  public void GetOrCreate_FlagsAndSessionsLeftOutByJoined_ShouldShareTheView()
  {
    var cache = CacheOf(1);

    var first = cache.GetOrCreate(7, 1, [Session("unit", 1), Session("e2e", 2)], ["e2e"], AllPaths);
    var second = cache.GetOrCreate(7, 1, [Session("unit", 1), Session("e2e", 2, false)], ["e2e"], AllPaths);

    second.ShouldBeSameAs(first);
    second.Totals.Lines.ShouldBe(1);
  }

  // The same commit identifier read from two organizations never shares a view (ADR-013).
  [Fact]
  public void GetOrCreate_OtherOrganization_ShouldNotSeeTheView()
  {
    var first = CacheOf(1).GetOrCreate(7, 1, Sessions, ["unit"], AllPaths);
    var second = CacheOf(2).GetOrCreate(7, 1, Sessions, ["unit"], AllPaths);

    second.ShouldNotBeSameAs(first);
  }

  [Fact]
  public void GetOrCreate_WithoutOrganization_ShouldThrow()
  {
    var cache = new MemoryReportViewCache(_memory, new OrganizationContext());

    Should.Throw<InvalidOperationException>(() => cache.GetOrCreate(7, 1, Sessions, [], AllPaths));
    _memory.Count.ShouldBe(0);
  }

  [Fact]
  public void GetOrCreate_AfterTenMinutes_ShouldBuildAgain()
  {
    var cache = CacheOf(1);

    var first = cache.GetOrCreate(7, 1, Sessions, [], AllPaths);
    _time.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
    cache.GetOrCreate(7, 1, Sessions, [], AllPaths).ShouldBeSameAs(first);

    _time.Advance(TimeSpan.FromSeconds(1));
    cache.GetOrCreate(7, 1, Sessions, [], AllPaths).ShouldNotBeSameAs(first);
  }

  [Fact]
  public void GetOrCreate_NullArgument_ShouldThrow()
  {
    var cache = CacheOf(1);

    Should.Throw<ArgumentNullException>(() => cache.GetOrCreate(7, 1, null!, [], AllPaths));
    Should.Throw<ArgumentNullException>(() => cache.GetOrCreate(7, 1, Sessions, null!, AllPaths));
    Should.Throw<ArgumentNullException>(() => cache.GetOrCreate(7, 1, Sessions, [], null!));
    _memory.Count.ShouldBe(0);
  }

  private static PathPatternSet Paths(params string[] patterns) => new(patterns.Select(PathPattern.Parse));

  private static SessionReport Session(string flag, int line, bool joined = true)
  {
    return new SessionReport([flag], CoverageReport.Create([FileCoverage.Create("src/a.cs", [new LineCoverage(line, 1, 0, 0, false, null)])]), joined);
  }

  private static int Distinct(ReportView[] views) => views.Distinct(ReferenceEqualityComparer.Instance).Count();

  private MemoryReportViewCache CacheOf(long organizationId)
  {
    var organization = new OrganizationContext();
    organization.Enter(organizationId);

    return new MemoryReportViewCache(_memory, organization);
  }

  #endregion

  #region Implementation of IDisposable

  public void Dispose() => _memory.Dispose();

  #endregion
}