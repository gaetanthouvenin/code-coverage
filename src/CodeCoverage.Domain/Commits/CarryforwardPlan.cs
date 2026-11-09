// ------------------------------------------------------------------------------------------------
// <copyright file="CarryforwardPlan.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

namespace CodeCoverage.Domain.Commits;

/// <summary>What a finalization has to carry forward: the carried flags that no upload of the commit brought.</summary>
public sealed class CarryforwardPlan
{
  #region Fields

  private readonly HashSet<string> _missing;

  private readonly HashSet<string> _uploaded;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="CarryforwardPlan" /> class.</summary>
  /// <param name="missing">The flags to carry forward.</param>
  /// <param name="uploaded">The flags of the sessions uploaded for the commit.</param>
  internal CarryforwardPlan(IEnumerable<string> missing, IEnumerable<string> uploaded)
  {
    _missing = new HashSet<string>(missing, StringComparer.Ordinal);
    _uploaded = new HashSet<string>(uploaded, StringComparer.Ordinal);
    Missing = [.. _missing.Order(StringComparer.Ordinal)];
  }

  #endregion

  #region Properties

  /// <summary>Gets the flags to carry forward, in ordinal order.</summary>
  public IReadOnlyList<string> Missing { get; }

  /// <summary>Gets a value indicating whether there is nothing to carry forward.</summary>
  public bool IsEmpty => Missing.Count == 0;

  #endregion

  #region Methods

  /// <summary>Tells whether a session of an ancestor is copied.</summary>
  /// <param name="flags">The flags of the session.</param>
  /// <returns>
  ///   <see langword="true" /> when one of them is missing and none was uploaded: a session that also holds
  ///   an uploaded flag would count that flag twice.
  /// </returns>
  /// <exception cref="ArgumentNullException"><paramref name="flags" /> is <see langword="null" />.</exception>
  public bool Selects(IEnumerable<string> flags)
  {
    ArgumentNullException.ThrowIfNull(flags);

    string[] all = [.. flags];

    return all.Any(_missing.Contains) && !all.Any(_uploaded.Contains);
  }

  #endregion
}
