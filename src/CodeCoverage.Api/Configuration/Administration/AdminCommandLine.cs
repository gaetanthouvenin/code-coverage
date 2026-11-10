// ------------------------------------------------------------------------------------------------
// <copyright file="AdminCommandLine.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Globalization;

using Mediator;

using CodeCoverage.Application.Features.Organizations.Commands.AdmitOrganization;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.CreateRepositoryUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeOrganizationUploadToken;
using CodeCoverage.Application.Features.UploadTokens.Commands.RevokeRepositoryUploadToken;
using CodeCoverage.Domain.Organizations;
using CodeCoverage.Domain.Tokens;
using CodeCoverage.Infrastructure.Administration;

using ApplicationException = CodeCoverage.Application.Exceptions.ApplicationException;
using ValidationException = CodeCoverage.Application.Exceptions.ValidationException;

namespace CodeCoverage.Api.Configuration.Administration;

/// <summary>
///   The administration verb of the Api: <c>dotnet CodeCoverage.Api.dll admin …</c> admits an
///   organization, creates, imports or revokes an upload token, then exits without starting Kestrel.
/// </summary>
/// <param name="scopeFactory">The factory of the scope of each command.</param>
/// <param name="input">Where an imported value is read: the standard input.</param>
/// <param name="output">Where a generated value is written, alone: the standard output.</param>
/// <param name="error">Where everything else is written: the standard error.</param>
/// <remarks>
///   <para>
///     The administration command line door of ADR-013: the only user of
///     <see cref="SystemCommandRunner" />, which runs a named list of commands as the system and
///     audits them under their own actor.
///   </para>
///   <para>
///     An imported value is read from the standard input only: no option takes it, so that it never
///     shows in the arguments of the process. It is never written back, and a refused argument is
///     never repeated in a message.
///   </para>
/// </remarks>
public sealed class AdminCommandLine(IServiceScopeFactory scopeFactory, TextReader input, TextWriter output, TextWriter error)
{
  #region Fields

  /// <summary>The first argument that selects the administration verb instead of the web host.</summary>
  public const string Verb = "admin";

  /// <summary>The exit code of a command that ran.</summary>
  public const int Success = 0;

  /// <summary>The exit code of a command the application refused.</summary>
  public const int Refused = 1;

  /// <summary>The exit code of arguments that name no command.</summary>
  public const int InvalidUsage = 2;

  private const string CreateUploadToken = "create-upload-token";

  private const string RevokeUploadToken = "revoke-upload-token";

  private const string AdmitOrganization = "admit-organization";

  private const string ForgeOption = "--forge";

  private const string OwnerOption = "--owner";

  private const string ScopeOption = "--scope";

  private const string RepositoryOption = "--repository";

  private const string LabelOption = "--label";

  private const string IdOption = "--id";

  private const string LoginOption = "--login";

  private const string AccountIdOption = "--account-id";

  private const string ImportSwitch = "--import-value-stdin";

  private const string ScopeOfOrganization = "organization";

  private const string ScopeOfRepository = "repository";

  private const string Usage = """
                               Usage:
                                 admin admit-organization [--forge <forge>] --login <login> --account-id <id>
                                 admin create-upload-token [--forge <forge>] --owner <login> --scope organization|repository
                                                           [--repository <name>] --label <label> [--import-value-stdin]
                                 admin revoke-upload-token [--forge <forge>] --owner <login> [--repository <name>] --id <id>

                               --forge is github (default), bitbucket, azure-devops or gitlab.
                               --import-value-stdin reads a Codecov token from the standard input: a value is never an argument.
                               A generated token is written once, alone, on the standard output.
                               """;

  private static readonly string[] CreateOptions = [ForgeOption, OwnerOption, ScopeOption, RepositoryOption, LabelOption];

  private static readonly string[] CreateSwitches = [ImportSwitch];

  private static readonly string[] RevokeOptions = [ForgeOption, OwnerOption, RepositoryOption, IdOption];

  private static readonly string[] AdmitOptions = [ForgeOption, LoginOption, AccountIdOption];

  private readonly SystemCommandRunner _runner = new(scopeFactory);

  #endregion

  #region Methods

  /// <summary>Tells whether the arguments of the process ask for the administration verb.</summary>
  /// <param name="arguments">The arguments of the process.</param>
  /// <returns><see langword="true" /> when the first argument is <see cref="Verb" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
  public static bool IsRequested(IReadOnlyList<string> arguments)
  {
    ArgumentNullException.ThrowIfNull(arguments);

    return arguments.Count > 0 && string.Equals(arguments[0], Verb, StringComparison.Ordinal);
  }

  /// <summary>Runs the command the arguments name.</summary>
  /// <param name="arguments">The arguments of the process, <see cref="Verb" /> included.</param>
  /// <param name="cancellationToken">A token to cancel the command.</param>
  /// <returns><see cref="Success" />, <see cref="Refused" /> or <see cref="InvalidUsage" />.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="arguments" /> is <see langword="null" />.</exception>
  public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(arguments);

    if (!IsRequested(arguments) || arguments.Count < 2)
    {
      return await RefuseUsageAsync("A command is expected after admin.").ConfigureAwait(false);
    }

    var rest = arguments.Skip(2);

    try
    {
      return arguments[1] switch
      {
        CreateUploadToken => await CreateUploadTokenAsync(rest, cancellationToken).ConfigureAwait(false),
        RevokeUploadToken => await RevokeUploadTokenAsync(rest, cancellationToken).ConfigureAwait(false),
        AdmitOrganization => await AdmitOrganizationAsync(rest, cancellationToken).ConfigureAwait(false),
        var _ => await RefuseUsageAsync("The command is unknown.").ConfigureAwait(false),
      };
    }
    catch (ValidationException invalid)
    {
      // The messages of the rules are constant: none repeats the value it refuses.
      foreach (var message in invalid.Errors.Values.SelectMany(static messages => messages))
      {
        await error.WriteLineAsync($"{invalid.ErrorCode}: {message}").ConfigureAwait(false);
      }

      return Refused;
    }
    catch (ApplicationException refusal)
    {
      await error.WriteLineAsync($"{refusal.ErrorCode}: {refusal.Message}").ConfigureAwait(false);

      return Refused;
    }
  }

  private static bool TryReadForge(AdminArguments options, out Forge forge)
  {
    forge = Forge.GitHub;

    return options.Find(ForgeOption) is not { } segment || ForgeSegments.TryParse(segment, out forge);
  }

  private static string Target(Forge forge, string owner, string? repository)
  {
    var organization = $"{ForgeSegments.ToSegment(forge)}/{owner}";

    return repository is null ? organization : $"{organization}/{repository}";
  }

  // An owner of several levels is written org:project (ADR-014): the organization is what precedes the first colon.
  private static string Tenant(string owner)
  {
    var separator = owner.IndexOf(':', StringComparison.Ordinal);

    return separator < 0 ? owner : owner[..separator];
  }

  private async Task<int> CreateUploadTokenAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
  {
    if (!AdminArguments.TryParse(arguments, CreateOptions, CreateSwitches, out var options, out var refusal))
    {
      return await RefuseUsageAsync(refusal).ConfigureAwait(false);
    }

    var owner = options.Find(OwnerOption);
    var scope = options.Find(ScopeOption);
    var repository = options.Find(RepositoryOption);
    var label = options.Find(LabelOption);

    if (!TryReadForge(options, out var forge) || owner is null || label is null)
    {
      return await RefuseUsageAsync("--owner and --label are required, and --forge must name a forge.").ConfigureAwait(false);
    }

    if (scope is not (ScopeOfOrganization or ScopeOfRepository) || scope is ScopeOfRepository != repository is not null)
    {
      return await RefuseUsageAsync("--scope is organization, or repository with --repository.").ConfigureAwait(false);
    }

    string? imported = null;

    if (options.Has(ImportSwitch))
    {
      imported = (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false))?.Trim();

      if (string.IsNullOrEmpty(imported))
      {
        return await RefuseUsageAsync("The standard input holds no value to import.").ConfigureAwait(false);
      }
    }

    ICommand<IssuedToken> command = repository is null
                                      ? new CreateOrganizationUploadTokenCommand(forge, owner, label, imported, null)
                                      : new CreateRepositoryUploadTokenCommand(forge, owner, repository, label, imported, null);

    var issued = await _runner.RunAsync(command, forge, Tenant(owner), cancellationToken).ConfigureAwait(false);
    var token = issued.Token;
    var id = token.Id.ToString(CultureInfo.InvariantCulture);
    var target = Target(forge, owner, repository);

    if (token.Imported)
    {
      // The value came from the caller: it is never written back.
      await error.WriteLineAsync($"Upload token {id} imported for {target} (ends with {token.Hint}).").ConfigureAwait(false);
    }
    else
    {
      await error.WriteLineAsync($"Upload token {id} created for {target}. Its value follows and is never shown again.").ConfigureAwait(false);
      await output.WriteLineAsync(issued.Value).ConfigureAwait(false);
    }

    return Success;
  }

  private async Task<int> RevokeUploadTokenAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
  {
    if (!AdminArguments.TryParse(arguments, RevokeOptions, [], out var options, out var refusal))
    {
      return await RefuseUsageAsync(refusal).ConfigureAwait(false);
    }

    var owner = options.Find(OwnerOption);
    var repository = options.Find(RepositoryOption);

    if (!TryReadForge(options, out var forge)
        || owner is null
        || !long.TryParse(options.Find(IdOption), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
    {
      return await RefuseUsageAsync("--owner and a numeric --id are required, and --forge must name a forge.").ConfigureAwait(false);
    }

    ICommand<Unit> command = repository is null
                               ? new RevokeOrganizationUploadTokenCommand(forge, owner, id)
                               : new RevokeRepositoryUploadTokenCommand(forge, owner, repository, id);

    await _runner.RunAsync(command, forge, Tenant(owner), cancellationToken).ConfigureAwait(false);
    await error.WriteLineAsync($"Upload token {id.ToString(CultureInfo.InvariantCulture)} of {Target(forge, owner, repository)} is revoked.")
               .ConfigureAwait(false);

    return Success;
  }

  private async Task<int> AdmitOrganizationAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
  {
    if (!AdminArguments.TryParse(arguments, AdmitOptions, [], out var options, out var refusal))
    {
      return await RefuseUsageAsync(refusal).ConfigureAwait(false);
    }

    var login = options.Find(LoginOption);
    var accountId = options.Find(AccountIdOption);

    if (!TryReadForge(options, out var forge) || login is null || accountId is null)
    {
      return await RefuseUsageAsync("--login and --account-id are required, and --forge must name a forge.").ConfigureAwait(false);
    }

    // No organization to enter: the command creates it, or activates a pending one.
    var organization = await _runner.RunAsync(new AdmitOrganizationCommand(forge, login, accountId), forge, null, cancellationToken)
                                    .ConfigureAwait(false);

    await error.WriteLineAsync($"Organization {Target(forge, organization.Login, null)} is {organization.State}.").ConfigureAwait(false);

    return Success;
  }

  private async Task<int> RefuseUsageAsync(string reason)
  {
    await error.WriteLineAsync(reason).ConfigureAwait(false);
    await error.WriteLineAsync(Usage).ConfigureAwait(false);

    return InvalidUsage;
  }

  #endregion
}
