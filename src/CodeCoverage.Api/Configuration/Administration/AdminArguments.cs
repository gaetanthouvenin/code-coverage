// ------------------------------------------------------------------------------------------------
// <copyright file="AdminArguments.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace CodeCoverage.Api.Configuration.Administration;

/// <summary>The options of one verb of the administration command line.</summary>
/// <remarks>
///   An option is <c>--name value</c>, a switch is <c>--name</c> alone. Anything else is refused,
///   so that a value never slips in under a name the verb does not know.
/// </remarks>
internal sealed class AdminArguments
{
  #region Fields

  private const string Prefix = "--";

  private readonly Dictionary<string, string> _values;

  private readonly HashSet<string> _switches;

  #endregion

  #region Ctors

  private AdminArguments(Dictionary<string, string> values, HashSet<string> switches)
  {
    _values = values;
    _switches = switches;
  }

  #endregion

  #region Methods

  /// <summary>Reads the options of a verb.</summary>
  /// <param name="arguments">The arguments after the verb.</param>
  /// <param name="options">The names of the options that take a value, with their <c>--</c>.</param>
  /// <param name="switches">The names of the switches, with their <c>--</c>.</param>
  /// <param name="parsed">The options read, or <see langword="null" /> when one is refused.</param>
  /// <param name="error">Why the arguments are refused, or <see langword="null" />.</param>
  /// <returns><see langword="true" /> when every argument is known and complete.</returns>
  /// <remarks>The message of a refusal names an option the verb knows, never what was typed.</remarks>
  public static bool TryParse(
    IEnumerable<string> arguments,
    IReadOnlyCollection<string> options,
    IReadOnlyCollection<string> switches,
    [NotNullWhen(true)] out AdminArguments? parsed,
    [NotNullWhen(false)] out string? error)
  {
    ArgumentNullException.ThrowIfNull(arguments);
    ArgumentNullException.ThrowIfNull(options);
    ArgumentNullException.ThrowIfNull(switches);

    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    var found = new HashSet<string>(StringComparer.Ordinal);
    string? pending = null;

    parsed = null;

    foreach (var argument in arguments)
    {
      if (pending is not null)
      {
        if (!values.TryAdd(pending, argument))
        {
          error = $"The option {pending} is given twice.";
          return false;
        }

        pending = null;
      }
      else if (options.Contains(argument, StringComparer.Ordinal))
      {
        pending = argument;
      }
      else if (switches.Contains(argument, StringComparer.Ordinal))
      {
        found.Add(argument);
      }
      else
      {
        // The argument itself is not repeated: it could be a secret typed by mistake.
        error = argument.StartsWith(Prefix, StringComparison.Ordinal)
                  ? "An option is unknown."
                  : "An argument is not expected: every value follows its option.";
        return false;
      }
    }

    if (pending is not null)
    {
      error = $"The option {pending} needs a value.";
      return false;
    }

    parsed = new AdminArguments(values, found);
    error = null;
    return true;
  }

  /// <summary>Gets the value of an option.</summary>
  /// <param name="option">The name of the option, with its <c>--</c>.</param>
  /// <returns>The value, or <see langword="null" /> when the option is absent or blank.</returns>
  public string? Find(string option)
  {
    ArgumentNullException.ThrowIfNull(option);

    return _values.TryGetValue(option, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
  }

  /// <summary>Tells whether a switch is present.</summary>
  /// <param name="name">The name of the switch, with its <c>--</c>.</param>
  /// <returns><see langword="true" /> when the switch is present.</returns>
  public bool Has(string name)
  {
    ArgumentNullException.ThrowIfNull(name);

    return _switches.Contains(name);
  }

  #endregion
}
