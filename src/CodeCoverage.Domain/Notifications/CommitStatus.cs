// ------------------------------------------------------------------------------------------------
// <copyright file="CommitStatus.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

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
}