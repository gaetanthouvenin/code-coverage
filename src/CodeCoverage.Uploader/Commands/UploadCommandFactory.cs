// ------------------------------------------------------------------------------------------------
// <copyright file="UploadCommandFactory.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;

using CodeCoverage.Uploader.Output;

namespace CodeCoverage.Uploader.Commands;

/// <summary>Builds and runs the <c>coverage-upload</c> command line.</summary>
/// <remarks>
///   Each option <c>--some-name</c> falls back on the variable <c>COVERAGE_SOME_NAME</c>. The
///   environment is read after the parsing, not through the default values of the options: the
///   help prints those, and it must never print a token. A blank variable counts as unset, since
///   CI systems pass a missing secret as an empty string.
/// </remarks>
internal static class UploadCommandFactory
{
  #region Fields

  private const string UrlOption = "--url";
  private const string TokenOption = "--token";
  private const string FileOption = "--file";
  private const string FlagsOption = "--flags";
  private const string NameOption = "--name";
  private const string SlugOption = "--slug";
  private const string ShaOption = "--sha";
  private const string BranchOption = "--branch";
  private const string PullRequestOption = "--pr";
  private const string ParentShaOption = "--parent-sha";
  private const string RootOption = "--root";
  private const string DisableSearchOption = "--disable-search";
  private const string NoNetworkOption = "--no-network";
  private const string WaitOption = "--wait";
  private const string FinalizeOption = "--finalize";
  private const string DryRunOption = "--dry-run";
  private const string FailOnErrorOption = "--fail-on-error";
  private const string VerboseOption = "--verbose";
  private const string CodecovTokenVariable = "CODECOV_TOKEN";

  // A value starting with @ is a flag or a glob, never a response file.
  private static readonly ParserConfiguration ParserSettings = new()
  {
    ResponseFileTokenReplacer = null,
  };

  #endregion

  #region Methods

  /// <summary>Builds the command.</summary>
  /// <param name="environment">Reads an environment variable.</param>
  /// <param name="upload">Runs the upload; returns whether it succeeded.</param>
  /// <returns>The command.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static RootCommand Create(
    Func<string, string?> environment,
    Func<UploaderSettings, CancellationToken, Task<bool>> upload)
  {
    ArgumentNullException.ThrowIfNull(environment);
    ArgumentNullException.ThrowIfNull(upload);

    var command = new RootCommand("Uploads coverage reports to a CodeCoverage server.")
    {
      Text(UrlOption, "The address of the server (required)."),
      new Option<string>(TokenOption)
      {
        Description = $"The upload credential. Environment: {VariableOf(TokenOption)}, then "
                      + $"{CodecovTokenVariable}.",
      },
      new Option<string[]>(FileOption, "-f")
      {
        Description = "A glob or path of a report, repeatable. Environment: "
                      + $"{VariableOf(FileOption)}, separated by semicolons.",
        AllowMultipleArgumentsPerToken = false,
      },
      Text(FlagsOption, "The flags of the upload, separated by commas."),
      Text(NameOption, "The session name."),
      Text(SlugOption, "The owner/repo slug."),
      Text(ShaOption, "The commit."),
      Text(BranchOption, "The head branch."),
      new Option<int?>(PullRequestOption)
      {
        Description = $"The pull request number. Environment: {VariableOf(PullRequestOption)}.",
      },
      Text(ParentShaOption, "The parent commit."),
      Text(RootOption, "The root directory of the repository."),
      Switch(DisableSearchOption, "Uses only the reports given by --file."),
      Switch(NoNetworkOption, "Leaves out the file list of the repository."),
      Switch(WaitOption, "Waits for the processing of the upload."),
      Switch(FinalizeOption, "Declares the uploads of the commit complete."),
      Switch(DryRunOption, "Prints what would be sent, without sending it."),
      Switch(FailOnErrorOption, "Exits with 1 on a failure, instead of 0."),
      Switch(VerboseOption, "Prints the details of the run."),
    };

    command.SetAction(
      (parseResult, cancellationToken) =>
        UploadAsync(parseResult, environment, upload, cancellationToken));

    return command;
  }

  /// <summary>Runs the uploader; whatever goes wrong, the exit code follows the policy.</summary>
  /// <param name="args">The command-line arguments.</param>
  /// <param name="environment">Reads an environment variable.</param>
  /// <param name="reporter">Prints the messages.</param>
  /// <param name="upload">Runs the upload; returns whether it succeeded.</param>
  /// <param name="cancellationToken">A token to cancel the run.</param>
  /// <returns>The exit code given by <see cref="ExitCodePolicy" />.</returns>
  /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
  public static async Task<int> RunAsync(
    string[] args,
    Func<string, string?> environment,
    ConsoleReporter reporter,
    Func<UploaderSettings, CancellationToken, Task<bool>> upload,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(args);
    ArgumentNullException.ThrowIfNull(environment);
    ArgumentNullException.ThrowIfNull(reporter);
    ArgumentNullException.ThrowIfNull(upload);

    var failOnError = false;

    try
    {
      failOnError = IsEnabled(environment(VariableOf(FailOnErrorOption)));

      var parseResult = Create(environment, upload).Parse(args, ParserSettings);

      failOnError = ReadSwitch(parseResult, FailOnErrorOption, environment);
      reporter.IsVerbose = ReadSwitch(parseResult, VerboseOption, environment);

      if (parseResult.Errors.Count > 0)
      {
        foreach (var error in parseResult.Errors)
        {
          reporter.Error(error.Message);
        }

        return ExitCodePolicy.Resolve(succeeded: false, failOnError);
      }

      var invocation = new InvocationConfiguration
      {
        Output = reporter.Output,
        Error = reporter.Output,
        EnableDefaultExceptionHandler = false,
      };

      return await parseResult.InvokeAsync(invocation, cancellationToken).ConfigureAwait(false);
    }
    catch (Exception exception)
    {
      reporter.Error(exception.Message);
      reporter.Detail(exception.ToString());

      return ExitCodePolicy.Resolve(succeeded: false, failOnError);
    }
  }

  private static async Task<int> UploadAsync(
    ParseResult parseResult,
    Func<string, string?> environment,
    Func<UploaderSettings, CancellationToken, Task<bool>> upload,
    CancellationToken cancellationToken)
  {
    var settings = ToSettings(parseResult, environment);
    var succeeded = await upload(settings, cancellationToken).ConfigureAwait(false);

    return ExitCodePolicy.Resolve(succeeded, settings.FailOnError);
  }

  private static UploaderSettings ToSettings(
    ParseResult parseResult,
    Func<string, string?> environment)
  {
    return new UploaderSettings
    {
      Url = ReadUrl(parseResult, environment),
      Token = ReadText(parseResult, TokenOption, environment)
              ?? NullIfBlank(environment(CodecovTokenVariable)),
      Files = ReadFiles(parseResult, environment),
      Flags = Split(ReadText(parseResult, FlagsOption, environment), ','),
      Name = ReadText(parseResult, NameOption, environment),
      Slug = ReadText(parseResult, SlugOption, environment),
      Sha = ReadText(parseResult, ShaOption, environment),
      Branch = ReadText(parseResult, BranchOption, environment),
      PullRequest = ReadPullRequest(parseResult, environment),
      ParentSha = ReadText(parseResult, ParentShaOption, environment),
      Root = ReadText(parseResult, RootOption, environment),
      DisableSearch = ReadSwitch(parseResult, DisableSearchOption, environment),
      NoNetwork = ReadSwitch(parseResult, NoNetworkOption, environment),
      Wait = ReadSwitch(parseResult, WaitOption, environment),
      Finalize = ReadSwitch(parseResult, FinalizeOption, environment),
      DryRun = ReadSwitch(parseResult, DryRunOption, environment),
      FailOnError = ReadSwitch(parseResult, FailOnErrorOption, environment),
      Verbose = ReadSwitch(parseResult, VerboseOption, environment),
    };
  }

  private static Uri ReadUrl(ParseResult parseResult, Func<string, string?> environment)
  {
    var value = ReadText(parseResult, UrlOption, environment);

    if (Uri.TryCreate(value, UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
    {
      return url;
    }

    throw new InvalidOperationException(
      $"An absolute http(s) address is required: pass {UrlOption} or set {VariableOf(UrlOption)}.");
  }

  private static string? ReadText(
    ParseResult parseResult,
    string option,
    Func<string, string?> environment)
  {
    return NullIfBlank(parseResult.GetValue<string>(option))
           ?? NullIfBlank(environment(VariableOf(option)));
  }

  private static List<string> ReadFiles(
    ParseResult parseResult,
    Func<string, string?> environment)
  {
    var files = parseResult.GetValue<string[]>(FileOption);

    return files is { Length: > 0 }
             ? [.. files]
             : Split(environment(VariableOf(FileOption)), ';');
  }

  // The number is only a hint, the server knows the pull requests: a malformed variable is ignored
  // rather than losing the upload.
  private static int? ReadPullRequest(ParseResult parseResult, Func<string, string?> environment)
  {
    if (parseResult.GetValue<int?>(PullRequestOption) is { } number)
    {
      return number;
    }

    return int.TryParse(
             environment(VariableOf(PullRequestOption)),
             NumberStyles.None,
             CultureInfo.InvariantCulture,
             out var parsed)
             ? parsed
             : null;
  }

  // A switch given on the command line wins over the environment, even when it turns it off.
  private static bool ReadSwitch(
    ParseResult parseResult,
    string option,
    Func<string, string?> environment)
  {
    return parseResult.GetResult(option) is OptionResult { Implicit: false }
             ? parseResult.GetValue<bool>(option)
             : IsEnabled(environment(VariableOf(option)));
  }

  private static bool IsEnabled(string? value)
  {
    return string.Equals(value?.Trim(), "true", StringComparison.OrdinalIgnoreCase)
           || string.Equals(value?.Trim(), "1", StringComparison.Ordinal);
  }

  private static List<string> Split(string? value, char separator)
  {
    return value is null
             ? []
             :
             [
               .. value.Split(
                 separator,
                 StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
             ];
  }

  private static string? NullIfBlank(string? value)
  {
    return string.IsNullOrWhiteSpace(value) ? null : value;
  }

  private static string VariableOf(string option)
  {
    return "COVERAGE_" + option[2..].Replace('-', '_').ToUpperInvariant();
  }

  private static Option<string> Text(string name, string description)
  {
    return new Option<string>(name)
    {
      Description = $"{description} Environment: {VariableOf(name)}.",
    };
  }

  private static Option<bool> Switch(string name, string description)
  {
    return new Option<bool>(name)
    {
      Description = $"{description} Environment: {VariableOf(name)} (true or 1).",
    };
  }

  #endregion
}
