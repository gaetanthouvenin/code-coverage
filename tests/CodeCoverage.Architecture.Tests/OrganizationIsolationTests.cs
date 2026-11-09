// ------------------------------------------------------------------------------------------------
// <copyright file="OrganizationIsolationTests.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using CodeCoverage.Architecture.Tests.Support;

namespace CodeCoverage.Architecture.Tests;

// What could bypass the organization filter, the write guard or the single entry (ADR-013). The
// rules read the Roslyn syntax of src/: a comment, an XML doc or a string never counts. A .razor is
// not C#: only its @inject directives are read (the C# of a page lives in its .razor.cs).
// MigrationBuilder.Sql is not guarded: a migration runs outside any request. An exception is added
// with its reason, and one that no longer matches a use fails, so that the lists only tighten.
public sealed partial class OrganizationIsolationTests
{
  #region Fields

  private const string Infrastructure = "src/CodeCoverage.Infrastructure/";

  private const string Gate = "src/CodeCoverage.Application/Behaviors/AuthorizationBehavior.cs";

  private const string JobQueue = Infrastructure + "Jobs/SqlJobQueue.cs";

  private const string HealthCheck = Infrastructure + "HealthChecks/ReadCommittedSnapshotHealthCheck.cs";

  private const string SettingsProvider = Infrastructure + "Settings/DatabaseSettingsConfigurationProvider.cs";

  private const string Registration = Infrastructure + "DependencyInjection.cs";

  private const string ScopeImplementation = Infrastructure + "Identity/OrganizationScope.cs";

  private const string TokenProvider = Infrastructure + "GitHub/GitHubAppTokenProvider.cs";

  private const string UploadCredentialCache = Infrastructure + "Uploads/MemoryUploadCredentialCache.cs";

  private const string AdminCommandLine = "src/CodeCoverage.Api/Configuration/Administration/AdminCommandLine.cs";

  private const string WebhookReception = "src/CodeCoverage.Application/Features/GitHubEvents/Commands/ReceiveWebhook/ReceiveWebhookCommandHandler.cs";

  private const string SamplePath = Infrastructure + "Sample.cs";

  private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Preview);

  private static readonly string[] QueryFilterBypass = ["IgnoreQueryFilters"];

  // The statements EF does not compose with the filter, and a model built outside OnModelCreating.
  private static readonly string[] RawSql =
  [
    "FromSql", "FromSqlRaw", "FromSqlInterpolated",
    "SqlQuery", "SqlQueryRaw",
    "ExecuteSql", "ExecuteSqlAsync", "ExecuteSqlRaw", "ExecuteSqlRawAsync", "ExecuteSqlInterpolated", "ExecuteSqlInterpolatedAsync",
    "ToSqlQuery", "UseModel",
    "GetDbConnection", "CreateCommand",
    "DbConnection", "DbCommand", "NpgsqlConnection", "NpgsqlCommand", "SqlConnection", "SqlCommand",
  ];

  private static readonly string[] OrganizationEntry = ["IOrganizationScope", "OrganizationScope"];

  private static readonly string[] CrossOrganizationEnqueue = ["ForOrganization"];

  private static readonly string[] SystemRunner = ["SystemCommandRunner"];

  private static readonly string[] ProcessWideCache = ["IMemoryCache", "MemoryCache"];

  private static readonly (string File, string Name)[] RawSqlAllowList =
  [
    // Claims a job through the provider dialects; Jobs is operational, never filtered.
    (JobQueue, "FromSql"),
    // Reads sys.databases, which is no table of the model.
    (HealthCheck, "SqlQuery"),
    // Reads Settings, a global table, in plain ADO.NET before the DI container exists.
    (SettingsProvider, "CreateCommand"),
    (SettingsProvider, "DbConnection"),
    (SettingsProvider, "NpgsqlConnection"),
    (SettingsProvider, "SqlConnection"),
  ];

  private static readonly (string File, string Name)[] OrganizationEntryAllowList =
  [
    // The only consumer: the gate of every message.
    (Gate, "IOrganizationScope"),
    // Not consumers: the implementation of the port and its registration.
    (ScopeImplementation, "IOrganizationScope"),
    (Registration, "IOrganizationScope"),
    (Registration, "OrganizationScope"),
  ];

  private static readonly (string File, string Name)[] CrossOrganizationEnqueueAllowList =
  [
    // Runs before any scope enters an organization: the installation of the signed body names it (ADR-013).
    (WebhookReception, "ForOrganization"),
  ];

  // A key that omits the organization would serve one organization's value to another: each user is reviewed.
  private static readonly (string File, string Name)[] MemoryCacheAllowList =
  [
    // Caches installation tokens by installation id, which belongs to a single account.
    (TokenProvider, "IMemoryCache"),
    // Remembers the identity of an upload token, under a key that starts with the organization of the scope.
    (UploadCredentialCache, "IMemoryCache"),
  ];

  // Not in AllowLists: AdminCommandLine (M1.23), which holds the only use, does not exist yet.
  private static readonly (string File, string Name)[] SystemRunnerAllowList = [(AdminCommandLine, "SystemCommandRunner")];

  private static readonly (string[] Names, (string File, string Name)[] Allowed)[] AllowLists =
  [
    (RawSql, RawSqlAllowList),
    (OrganizationEntry, OrganizationEntryAllowList),
    (CrossOrganizationEnqueue, CrossOrganizationEnqueueAllowList),
    (ProcessWideCache, MemoryCacheAllowList),
  ];

  private static readonly Lazy<List<(string Path, string Text)>> Sources = new(
    static () => SourceFiles.All
                            .Select(static f => (Path: SourceFiles.Relative(f), Text: SourceFiles.Read(f)))
                            .Where(static s => s.Path.StartsWith("src/", StringComparison.Ordinal))
                            .ToList()
  );

  #endregion

  #region Methods

  [Fact]
  public void Source_ShouldNeverIgnoreQueryFilters()
  {
    ViolationsInSource(QueryFilterBypass, [])
      .ShouldBeEmpty("IgnoreQueryFilters has no exception: cross-organization work is a coordinator job (ADR-013)");
  }

  [Fact]
  public void Source_RawSql_ShouldStayOnTheAllowList()
  {
    ViolationsInSource(RawSql, RawSqlAllowList)
      .ShouldBeEmpty("raw SQL and UseModel escape the organization filter: use LINQ, or add a named exception (ADR-013)");
  }

  [Fact]
  public void Source_SetProperty_ShouldNeverTargetOrganizationId()
  {
    Sources.Value
           .Where(static s => s.Path.EndsWith(".cs", StringComparison.Ordinal))
           .SelectMany(static s => SetPropertyViolations(s.Path, s.Text))
           .ShouldBeEmpty("ExecuteUpdate never moves a row to another organization (ADR-013)");
  }

  [Fact]
  public void Source_OrganizationScopeConsumers_ShouldBeAuthorizationBehaviorOnly()
  {
    ViolationsInSource(OrganizationEntry, OrganizationEntryAllowList)
      .ShouldBeEmpty("a DI scope enters an organization through one gate: AuthorizationBehavior (ADR-013)");
  }

  [Fact]
  public void Source_ForOrganization_ShouldStayOnTheAllowList()
  {
    ViolationsInSource(CrossOrganizationEnqueue, CrossOrganizationEnqueueAllowList)
      .ShouldBeEmpty("queuing for another organization by identifier is limited to named sources (ADR-013)");
  }

  [Fact]
  public void Source_SystemCommandRunner_ShouldStayInAdminCommandLine()
  {
    ViolationsInSource(SystemRunner, SystemRunnerAllowList)
      .ShouldBeEmpty("only the administration command line runs a command as the system (ADR-013)");
  }

  [Fact]
  public void Source_MemoryCache_ShouldStayOnTheAllowList()
  {
    ViolationsInSource(ProcessWideCache, MemoryCacheAllowList)
      .ShouldBeEmpty("a process-wide cache is shared by every organization: name the user and its organization-safe key (ADR-013)");
  }

  [Fact]
  public void AllowLists_EveryEntry_ShouldMatchACurrentUse()
  {
    var stale = AllowLists.SelectMany(static list => StaleEntries(list.Names, list.Allowed)).ToList();

    stale.ShouldBeEmpty("an exception that no longer matches a use must be removed: the lists only tighten");
  }

  [Theory]
  [InlineData("db.Set<T>().IgnoreQueryFilters()")]
  [InlineData("db.Set<T>().IgnoreQueryFilters([\"Organization\"])")]
  [InlineData("Query(EntityFrameworkQueryableExtensions.IgnoreQueryFilters)")]
  public void Source_IgnoreQueryFilters_ShouldFail(string statement)
  {
    Violations(SamplePath, Method(statement), QueryFilterBypass, []).ShouldHaveSingleItem();
  }

  [Theory]
  [InlineData("db.Set<T>().FromSql($\"SELECT 1\")")]
  [InlineData("db.Set<T>().FromSqlRaw(\"SELECT 1\")")]
  [InlineData("db.Database.SqlQuery<int>($\"SELECT 1\")")]
  [InlineData("db.Database.SqlQueryRaw<int>(\"SELECT 1\")")]
  [InlineData("db.Database.ExecuteSqlAsync($\"DELETE FROM Repositories\")")]
  [InlineData("db.Database.ExecuteSqlRaw(\"DELETE FROM Repositories\")")]
  [InlineData("db.Database.GetDbConnection()")]
  [InlineData("connection.CreateCommand()")]
  [InlineData("new NpgsqlCommand(\"SELECT 1\")")]
  [InlineData("options.UseModel(compiled)")]
  public void Source_RawSqlOutsideAllowList_ShouldFail(string statement)
  {
    Violations(SamplePath, Method(statement), RawSql, RawSqlAllowList).ShouldHaveSingleItem();
  }

  [Theory]
  [InlineData(JobQueue, "db.Jobs.FromSql($\"SELECT 1\")")]
  [InlineData(HealthCheck, "db.Database.SqlQuery<bool>($\"SELECT 1\")")]
  [InlineData(SettingsProvider, "connection.CreateCommand()")]
  public void Source_RawSqlOnTheAllowList_ShouldPass(string file, string statement)
  {
    Violations(file, Method(statement), RawSql, RawSqlAllowList).ShouldBeEmpty();
  }

  [Fact]
  public void Source_OtherRawSqlInAnAllowedFile_ShouldFail()
  {
    Violations(JobQueue, Method("db.Database.SqlQuery<bool>($\"SELECT 1\")"), RawSql, RawSqlAllowList)
      .ShouldHaveSingleItem()
      .ShouldContain("SqlQuery");
  }

  [Theory]
  [InlineData("class C { // db.Database.SqlQuery<int>($\"SELECT 1\");\n}")]
  [InlineData("class C { /* options.UseModel(compiled); */ }")]
  [InlineData("/// <summary>See <see cref=\"RelationalQueryableExtensions.FromSql\" />.</summary>\nclass C;")]
  [InlineData("class C { const string Text = \"SqlQuery FromSql UseModel\"; }")]
  public void Source_RawSqlInComment_ShouldBeIgnored(string text)
  {
    Violations(SamplePath, text, RawSql, []).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("s.SetProperty(r => r.OrganizationId, 2)")]
  [InlineData("s.SetProperty(r => EF.Property<long>(r, \"OrganizationId\"), 2)")]
  [InlineData("s.SetProperty(r => EF.Property<long>(r, OrganizationOwnedExtensions.ColumnName), 2)")]
  [InlineData("s.SetProperty(r => EF.Property<long>(r, nameof(Stored.OrganizationId)), 2)")]
  [InlineData("s.SetProperty(r => EF.Property<long>(r, column), 2)")]
  [InlineData("s.SetProperty(selector, 2)")]
  public void Source_SetPropertyOnOrganizationId_ShouldFail(string statement)
  {
    SetPropertyViolations(SamplePath, Method(statement)).ShouldHaveSingleItem();
  }

  [Theory]
  [InlineData("s.SetProperty(j => j.RunAfter, now)")]
  [InlineData("s.SetProperty(j => j.Owner, j => j.OrganizationId)")]
  [InlineData("s.SetProperty(j => EF.Property<int>(j, \"Attempts\"), 2)")]
  [InlineData("s.SetProperty(j => EF.Property<int>(j, nameof(Stored.Attempts)), 2)")]
  [InlineData("// s.SetProperty(r => r.OrganizationId, 2)\nother()")]
  public void SetPropertyViolations_OnAnotherColumn_ShouldBeEmpty(string statement)
  {
    SetPropertyViolations(SamplePath, Method(statement)).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("class C(IOrganizationScope scope);")]
  [InlineData("class C { [Inject] private IOrganizationScope Scope { get; set; } = null!; }")]
  [InlineData("class C { void M(IServiceProvider s) => s.GetRequiredService<IOrganizationScope>(); }")]
  [InlineData("class C { object M() => new OrganizationScope(directory, context); }")]
  public void Source_OrganizationScopeOutsideTheGate_ShouldFail(string text)
  {
    Violations(SamplePath, text, OrganizationEntry, OrganizationEntryAllowList).ShouldHaveSingleItem();
  }

  [Theory]
  [InlineData("@inject IOrganizationScope Scope\n<p></p>\n", 1)]
  [InlineData("<p></p>\n@inject CodeCoverage.Domain.Ports.IOrganizationScope Scope\n", 2)]
  public void Source_OrganizationScopeInjectedInRazor_ShouldFail(string text, int line)
  {
    Violations("src/CodeCoverage.Web/Components/Pages/Sample.razor", text, OrganizationEntry, OrganizationEntryAllowList)
      .ShouldHaveSingleItem()
      .ShouldStartWith($"src/CodeCoverage.Web/Components/Pages/Sample.razor:{line}: ");
  }

  [Theory]
  [InlineData("@* @inject IOrganizationScope Scope *@\n<p>@inject IOrganizationScope Scope</p>\n")]
  [InlineData("@inject ICurrentOrganization Organization\n")]
  public void Source_NoOrganizationScopeInjectedInRazor_ShouldBeEmpty(string text)
  {
    Violations("src/CodeCoverage.Web/Components/Pages/Sample.razor", text, OrganizationEntry, []).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("JobRequest.ForOrganization(7, \"type\", \"payload\")")]
  [InlineData("ForOrganization(7, \"type\", \"payload\")")]
  [InlineData("var enqueue = JobRequest.ForOrganization")]
  public void Source_ForOrganizationOutsideAllowList_ShouldFail(string statement)
  {
    Violations(SamplePath, Method(statement), CrossOrganizationEnqueue, []).ShouldHaveSingleItem();
  }

  [Theory]
  [InlineData("new SystemCommandRunner(services)")]
  [InlineData("services.GetRequiredService<SystemCommandRunner>()")]
  public void Source_SystemCommandRunnerOutsideAdminCommandLine_ShouldFail(string statement)
  {
    Violations(SamplePath, Method(statement), SystemRunner, SystemRunnerAllowList).ShouldHaveSingleItem();
    Violations(AdminCommandLine, Method(statement), SystemRunner, SystemRunnerAllowList).ShouldBeEmpty();
  }

  [Theory]
  [InlineData("class C(IMemoryCache cache);")]
  [InlineData("class C { object M() => new MemoryCache(options); }")]
  public void Source_MemoryCacheOutsideAllowList_ShouldFail(string text)
  {
    Violations(SamplePath, text, ProcessWideCache, MemoryCacheAllowList).ShouldHaveSingleItem();
  }

  [Fact]
  public void Source_MemoryCacheRegistration_ShouldBeIgnored()
  {
    Violations(SamplePath, Method("services.AddMemoryCache()"), ProcessWideCache, []).ShouldBeEmpty();
  }

  [Fact]
  public void StaleEntries_WithoutAUse_ShouldNameTheEntry()
  {
    StaleEntries(RawSql, [(SamplePath, "SqlQuery")]).ShouldBe([$"{SamplePath}: SqlQuery"]);
  }

  private static string Method(string statement) => "class C { void M() { " + statement + "; } }";

  private static List<string> ViolationsInSource(string[] names, (string File, string Name)[] allowed) =>
    Sources.Value.SelectMany(s => Violations(s.Path, s.Text, names, allowed)).ToList();

  private static List<string> Violations(string path, string text, string[] names, (string File, string Name)[] allowed) =>
    Uses(path, text, names).Where(u => !allowed.Contains((u.File, u.Name)))
                           .Select(static u => $"{u.File}:{u.Line}: {u.Name}")
                           .ToList();

  private static List<string> StaleEntries(string[] names, (string File, string Name)[] allowed)
  {
    var uses = Sources.Value.SelectMany(s => Uses(s.Path, s.Text, names)).Select(static u => (u.File, u.Name)).ToHashSet();

    return allowed.Where(e => !uses.Contains(e)).Select(static e => $"{e.File}: {e.Name}").ToList();
  }

  private static IEnumerable<(string File, int Line, string Name)> Uses(string path, string text, string[] names) =>
    path.EndsWith(".razor", StringComparison.Ordinal) ? RazorUses(path, text, names) : CSharpUses(path, text, names);

  // SimpleName covers a call, a method group, a type and a generic argument; DescendantNodes stays out of the trivia.
  private static IEnumerable<(string File, int Line, string Name)> CSharpUses(string path, string text, string[] names) =>
    CSharpSyntaxTree.ParseText(text, ParseOptions)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Where(n => names.Contains(n.Identifier.ValueText, StringComparer.Ordinal))
                    .Select(n => (path, n.GetLocation().GetLineSpan().StartLinePosition.Line + 1, n.Identifier.ValueText));

  private static IEnumerable<(string File, int Line, string Name)> RazorUses(string path, string text, string[] names)
  {
    var withoutComments = RazorComment().Replace(text, static m => new string('\n', m.Value.Count(static c => c == '\n')));

    foreach (Match directive in InjectDirective().Matches(withoutComments))
    {
      var line = withoutComments.AsSpan(0, directive.Index).Count('\n') + 1;

      foreach (var name in SyntaxFactory.ParseTypeName(directive.Groups["type"].Value)
                                        .DescendantNodesAndSelf()
                                        .OfType<SimpleNameSyntax>()
                                        .Select(static n => n.Identifier.ValueText)
                                        .Where(n => names.Contains(n, StringComparer.Ordinal)))
      {
        yield return (path, line, name);
      }
    }
  }

  private static List<string> SetPropertyViolations(string path, string text) =>
    CSharpSyntaxTree.ParseText(text, ParseOptions)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(static i => CalledName(i) == "SetProperty" && i.ArgumentList.Arguments.Count > 0)
                    .Where(static i => MayTargetOrganizationId(i.ArgumentList.Arguments[0].Expression))
                    .Select(i => $"{path}:{i.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {i}")
                    .ToList();

  // Only a lambda can be read, and EF.Property only with a name known from the syntax alone.
  private static bool MayTargetOrganizationId(ExpressionSyntax selector)
  {
    if (selector is not LambdaExpressionSyntax)
    {
      return true;
    }

    var names = selector.DescendantNodes().OfType<SimpleNameSyntax>().Select(static n => n.Identifier.ValueText);
    var literals = selector.DescendantNodes().OfType<LiteralExpressionSyntax>().Select(static l => l.Token.ValueText);
    var unreadable = selector.DescendantNodes()
                             .OfType<InvocationExpressionSyntax>()
                             .Where(static i => CalledName(i) == "Property" && i.ArgumentList.Arguments.Count == 2)
                             .Select(static i => i.ArgumentList.Arguments[1].Expression)
                             .Any(static name => name is not LiteralExpressionSyntax
                                                 && name is not InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } });

    return unreadable || names.Concat(literals).Any(static n => n is "OrganizationId" or "ColumnName");
  }

  private static string? CalledName(InvocationExpressionSyntax invocation) =>
    invocation.Expression switch
    {
      MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
      SimpleNameSyntax name => name.Identifier.ValueText,
      var _ => null,
    };

  [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
  private static partial Regex RazorComment();

  [GeneratedRegex(@"^[ \t]*@inject[ \t]+(?<type>.+?)[ \t]+\w+[ \t]*\r?$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
  private static partial Regex InjectDirective();

  #endregion
}
