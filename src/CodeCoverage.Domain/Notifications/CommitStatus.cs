// ------------------------------------------------------------------------------------------------
// <copyright file="CommitStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Guards;

namespace CodeCoverage.Domain.Notifications;

/// <summary>A commit status to post on the forge.</summary>
public sealed record CommitStatus
{
  #region Fields

  /// <summary>The maximum length of a description: the forge refuses a longer one.</summary>
  public const int DescriptionMaxLength = 140;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CommitStatus" /> class.</summary>
  /// <param name="context">The context of the status.</param>
  /// <param name="state">The state of the status.</param>
  /// <param name="description">The description, cut at <see cref="DescriptionMaxLength" /> characters.</param>
  /// <exception cref="ArgumentException"><paramref name="context" /> is empty.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="description" /> is <see langword="null" />.</exception>
  public CommitStatus(string context, StatusState state, string description)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(context);
    ArgumentNullException.ThrowIfNull(description);

    Context = context;
    State = state;
    Description = description.Length > DescriptionMaxLength ? description[..DescriptionMaxLength] : description;
  }

  #endregion

  #region Properties

  /// <summary>Gets the context of the status.</summary>
  public string Context { get; }

  /// <summary>Gets the state of the status.</summary>
  public StatusState State { get; }

  /// <summary>Gets the description of the status, at most <see cref="DescriptionMaxLength" /> characters long.</summary>
  public string Description { get; }

  #endregion

  #region Methods

  /// <summary>Gives the same status with a text added at the end of its description.</summary>
  /// <param name="suffix">The text to add, with the space that separates it.</param>
  /// <returns>The status; the description is cut so that the suffix always fits whole.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="suffix" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="suffix" /> is longer than a description can be.</exception>
  public CommitStatus WithSuffix(string suffix)
  {
    ArgumentNullException.ThrowIfNull(suffix);
    ArgumentException.ThrowIf(suffix.Length > DescriptionMaxLength, $"A suffix cannot be longer than {DescriptionMaxLength} characters.", nameof(suffix));

    if (suffix.Length == 0 || Description.EndsWith(suffix, StringComparison.Ordinal))
    {
      return this;
    }

    var room = DescriptionMaxLength - suffix.Length;

    return new CommitStatus(Context, State, (Description.Length > room ? Description[..room] : Description) + suffix);
  }

  #endregion
}