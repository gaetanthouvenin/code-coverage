// ------------------------------------------------------------------------------------------------
// <copyright file="RepositorySlug.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using CodeCoverage.Domain.Guards;
using CodeCoverage.Domain.Organizations;

namespace CodeCoverage.Domain.Repositories;

/// <summary>The name of a repository on its forge: its owner, its namespace and its own name.</summary>
/// <remarks>
///   <para>
///     A slug is written <c>owner/repo</c> or <c>group/sub/repo</c>, or in the encoded form of Codecov,
///     <c>owner::::repo</c> or <c>group:::sub::::repo</c>. The name of the repository follows the last
///     separator; the first level names the account and what lies between is the namespace, always kept as
///     <c>sub/sub2</c> (ADR-014).
///   </para>
///   <para>
///     Inside an organization, a repository is resolved by its namespace and its name:
///     <see cref="Owner" /> is only what the caller wrote.
///   </para>
/// </remarks>
public sealed record RepositorySlug
{
  #region Fields

  /// <summary>The separator of the levels of a slug, and of the levels kept in a namespace.</summary>
  private const char PathSeparator = '/';

  /// <summary>The separator of the levels of the owner, in the encoded form of Codecov.</summary>
  private const string CodecovLevelSeparator = ":::";

  /// <summary>The separator of the owner and the repository, in the encoded form of Codecov.</summary>
  private const string CodecovNameSeparator = "::::";

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="RepositorySlug" /> class.</summary>
  /// <param name="owner">The account, already checked.</param>
  /// <param name="namespace">The levels between the account and the repository, already checked.</param>
  /// <param name="name">The name of the repository, already checked.</param>
  private RepositorySlug(string owner, string @namespace, string name)
  {
    Owner = owner;
    Namespace = @namespace;
    Name = name;
  }

  #endregion

  #region Properties

  /// <summary>Gets the login of the account, as it was written.</summary>
  public string Owner { get; }

  /// <summary>
  ///   Gets the levels between the account and the repository, written <c>sub/sub2</c>: the project on Azure
  ///   DevOps, the subgroups on GitLab, and an empty text on the other forges.
  /// </summary>
  public string Namespace { get; }

  /// <summary>Gets the name of the repository, as it was written.</summary>
  public string Name { get; }

  /// <summary>Gets the form of <see cref="Namespace" /> that is compared and stored as unique.</summary>
  public string NormalizedNamespace => Namespace.ToLowerInvariant();

  /// <summary>Gets the form of <see cref="Name" /> that is compared and stored as unique.</summary>
  public string NormalizedName => Name.ToLowerInvariant();

  #endregion

  #region Methods

  /// <summary>Builds a slug from the levels that a forge reports.</summary>
  /// <param name="owner">The login of the account.</param>
  /// <param name="namespace">The levels between the account and the repository, or an empty text.</param>
  /// <param name="name">The name of the repository.</param>
  /// <returns>The slug.</returns>
  /// <exception cref="ArgumentNullException">
  ///   <paramref name="owner" />, <paramref name="namespace" /> or <paramref name="name" /> is
  ///   <see langword="null" />.
  /// </exception>
  /// <exception cref="ArgumentException">One of the levels is not valid.</exception>
  public static RepositorySlug Create(string owner, string @namespace, string name)
  {
    ArgumentNullException.ThrowIfNull(owner);
    ArgumentNullException.ThrowIfNull(@namespace);
    ArgumentNullException.ThrowIfNull(name);

    ArgumentException.ThrowIf(
      !TryCreate(owner, @namespace, name, out var slug),
      $"'{owner}', '{@namespace}' and '{name}' do not name a repository.",
      nameof(name));

    return slug;
  }

  /// <summary>Reads a slug in one of its written forms.</summary>
  /// <param name="value">The text to read.</param>
  /// <returns>The slug.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="value" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException"><paramref name="value" /> is not a slug.</exception>
  public static RepositorySlug Parse(string value)
  {
    ArgumentNullException.ThrowIfNull(value);

    ArgumentException.ThrowIf(!TryParse(value, out var slug), $"'{value}' is not a repository slug.", nameof(value));

    return slug;
  }

  /// <summary>Tries to read a slug in one of its written forms.</summary>
  /// <param name="value">The text to read, or <see langword="null" />.</param>
  /// <param name="slug">The slug, or <see langword="null" /> when the text is not one.</param>
  /// <returns><see langword="true" /> when the text is a slug; otherwise, <see langword="false" />.</returns>
  public static bool TryParse([NotNullWhen(true)] string? value, [NotNullWhen(true)] out RepositorySlug? slug)
  {
    slug = null;

    if (string.IsNullOrEmpty(value))
    {
      return false;
    }

    var codecovSeparator = value.LastIndexOf(CodecovNameSeparator, StringComparison.Ordinal);
    var pathSeparator = value.LastIndexOf(PathSeparator);
    string owner;
    string name;
    string[] levels;

    if (codecovSeparator >= 0)
    {
      owner = value[..codecovSeparator];
      name = value[(codecovSeparator + CodecovNameSeparator.Length)..];
      levels = owner.Split(CodecovLevelSeparator);
    }
    else if (pathSeparator >= 0)
    {
      owner = value[..pathSeparator];
      name = value[(pathSeparator + 1)..];
      levels = owner.Split(PathSeparator);
    }
    else
    {
      return false;
    }

    // Checked before the levels are joined: an empty level would otherwise vanish in an empty namespace.
    return levels[1..].All(IsValidLevel)
           && TryCreate(levels[0], string.Join(PathSeparator, levels[1..]), name, out slug);
  }

  /// <summary>Gives the written form of the slug, with slashes.</summary>
  /// <returns>The slug, written <c>owner/repo</c> or <c>owner/sub/repo</c>.</returns>
  public override string ToString() =>
    Namespace.Length == 0
      ? $"{Owner}{PathSeparator}{Name}"
      : $"{Owner}{PathSeparator}{Namespace}{PathSeparator}{Name}";

  /// <summary>Builds a slug when its levels are valid.</summary>
  /// <param name="owner">The login of the account.</param>
  /// <param name="namespace">The levels between the account and the repository, or an empty text.</param>
  /// <param name="name">The name of the repository.</param>
  /// <param name="slug">The slug, or <see langword="null" /> when a level is not valid.</param>
  /// <returns><see langword="true" /> when the levels name a repository.</returns>
  private static bool TryCreate(
    string owner,
    string @namespace,
    string name,
    [NotNullWhen(true)] out RepositorySlug? slug)
  {
    var valid = Organization.IsWellFormedLogin(owner)
                && name.Length <= TrackedRepository.NameMaxLength
                && IsValidLevel(name)
                && @namespace.Length <= TrackedRepository.NamespaceMaxLength
                && (@namespace.Length == 0 || @namespace.Split(PathSeparator).All(IsValidLevel));

    slug = valid ? new RepositorySlug(owner, @namespace, name) : null;

    return valid;
  }

  /// <summary>Tells whether a text can be one level of a slug.</summary>
  /// <param name="level">The text.</param>
  /// <returns>
  ///   <see langword="true" /> unless the text is empty, a relative path, framed by white space, or holds a
  ///   separator or a control character. Spaces are allowed inside: an Azure DevOps project may hold some.
  /// </returns>
  private static bool IsValidLevel(string level) =>
    level.Length > 0
    && level is not ("." or "..")
    && !char.IsWhiteSpace(level[0])
    && !char.IsWhiteSpace(level[^1])
    && !level.Any(static character => character is ':' or PathSeparator or '\\' || char.IsControl(character));

  #endregion
}
