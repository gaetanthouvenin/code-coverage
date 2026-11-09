// ------------------------------------------------------------------------------------------------
// <copyright file="PullRequest.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using CodeCoverage.Domain.Commits;
using CodeCoverage.Domain.Contracts.Forges;
using CodeCoverage.Domain.Git;

namespace CodeCoverage.Domain.PullRequests;

/// <summary>A pull request of a tracked repository; its forge is the authority on what it holds.</summary>
public sealed class PullRequest
{
  #region Fields

  /// <summary>The maximum length of <see cref="Title" />.</summary>
  public const int TitleMaxLength = 512;

  /// <summary>The maximum length of <see cref="AuthorLogin" />.</summary>
  public const int AuthorLoginMaxLength = 255;

  /// <summary>The maximum length of <see cref="HeadBranch" /> and <see cref="BaseBranch" />.</summary>
  public const int BranchMaxLength = Commit.BranchMaxLength;

  /// <summary>The length of <see cref="CommentBodyHash" />.</summary>
  public const int CommentBodyHashLength = 32;

  #endregion

  #region Ctors

  /// <summary>Initializes a new instance of the <see cref="PullRequest" /> class.</summary>
  /// <param name="repositoryId">The repository.</param>
  /// <param name="number">The number of the pull request.</param>
  /// <param name="state">Its state.</param>
  /// <param name="title">Its title.</param>
  /// <param name="headSha">The SHA of its head.</param>
  /// <param name="headBranch">The branch of its head.</param>
  /// <param name="baseBranch">The branch it targets.</param>
  /// <param name="baseShaGitHub">The SHA of the base that the forge reports.</param>
  /// <param name="updatedAt">When the row last changed, in UTC.</param>
  private PullRequest(
    long repositoryId,
    int number,
    PullRequestState state,
    string title,
    GitSha headSha,
    string headBranch,
    string baseBranch,
    GitSha baseShaGitHub,
    DateTimeOffset updatedAt)
  {
    RepositoryId = repositoryId;
    Number = number;
    State = state;
    Title = title;
    HeadSha = headSha;
    HeadBranch = headBranch;
    BaseBranch = baseBranch;
    BaseShaGitHub = baseShaGitHub;
    UpdatedAt = updatedAt;
  }

  #endregion

  #region Properties

  /// <summary>Gets the identifier of the row, assigned when it is saved.</summary>
  public long Id { get; private set; }

  /// <summary>Gets the identifier of the repository.</summary>
  public long RepositoryId { get; private set; }

  /// <summary>Gets the number of the pull request in its repository.</summary>
  public int Number { get; private set; }

  /// <summary>Gets the state of the pull request.</summary>
  public PullRequestState State { get; private set; }

  /// <summary>Gets its title.</summary>
  public string Title { get; private set; }

  /// <summary>Gets the login of its author, or <see langword="null" /> when the author has no account any more.</summary>
  public string? AuthorLogin { get; private set; }

  /// <summary>Gets the SHA of its head commit, never the synthetic merge commit.</summary>
  public GitSha HeadSha { get; private set; }

  /// <summary>Gets the branch of its head.</summary>
  public string HeadBranch { get; private set; }

  /// <summary>Gets the branch it targets.</summary>
  public string BaseBranch { get; private set; }

  /// <summary>Gets the SHA of the base that the forge reports.</summary>
  public GitSha BaseShaGitHub { get; private set; }

  /// <summary>Gets the commit its branch forked from, or <see langword="null" /> before it is resolved.</summary>
  public GitSha? ForkPointSha { get; private set; }

  /// <summary>Gets the commit it was last compared to, or <see langword="null" />.</summary>
  public GitSha? ComparedToSha { get; private set; }

  /// <summary>Gets the identifier of its comment on the forge, or <see langword="null" />.</summary>
  public long? CommentId { get; private set; }

  /// <summary>Gets the hash of the body of its comment, or <see langword="null" />.</summary>
  public byte[]? CommentBodyHash { get; private set; }

  /// <summary>Gets the head its comment was written for, or <see langword="null" />.</summary>
  public GitSha? CommentedHeadSha { get; private set; }

  /// <summary>Gets when the row last changed, in UTC.</summary>
  public DateTimeOffset UpdatedAt { get; private set; }

  /// <summary>Gets the version of the row, incremented by each change.</summary>
  public int Version { get; private set; }

  /// <summary>Gets a value indicating whether a comment can still be written on it.</summary>
  public bool AcceptsComments => State is PullRequestState.Open;

  #endregion

  #region Methods

  /// <summary>Records a pull request as its forge reports it.</summary>
  /// <param name="repositoryId">The identifier of the repository.</param>
  /// <param name="pull">What the forge reports.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <returns>The pull request, not saved yet.</returns>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="repositoryId" /> or the number is not positive.</exception>
  /// <exception cref="ArgumentNullException"><paramref name="pull" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">A branch is too long.</exception>
  public static PullRequest Create(long repositoryId, ForgePullInfo pull, DateTimeOffset at)
  {
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(repositoryId);
    ArgumentNullException.ThrowIfNull(pull);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pull.Number, nameof(pull));
    ThrowIfBranchTooLong(pull);

    return new PullRequest(
      repositoryId,
      pull.Number,
      ToState(pull.State),
      Shorten(pull.Title),
      pull.HeadSha,
      pull.HeadBranch,
      pull.BaseBranch,
      pull.BaseSha,
      at.ToUniversalTime()
    )
    {
      AuthorLogin = ShortenLogin(pull.AuthorLogin),
    };
  }

  /// <summary>Applies what the forge reports now; nothing changes when it reports the same.</summary>
  /// <param name="pull">What the forge reports.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  /// <exception cref="ArgumentNullException"><paramref name="pull" /> is <see langword="null" />.</exception>
  /// <exception cref="ArgumentException">A branch is too long, or the number is not the one of this pull request.</exception>
  public void Refresh(ForgePullInfo pull, DateTimeOffset at)
  {
    ArgumentNullException.ThrowIfNull(pull);
    ThrowIfBranchTooLong(pull);

    if (pull.Number != Number)
    {
      throw new ArgumentException($"The forge reports pull request {pull.Number}, not {Number}.", nameof(pull));
    }

    var state = ToState(pull.State);
    var title = Shorten(pull.Title);
    var author = ShortenLogin(pull.AuthorLogin);

    if (state == State
        && title == Title
        && author == AuthorLogin
        && pull.HeadSha == HeadSha
        && pull.HeadBranch == HeadBranch
        && pull.BaseBranch == BaseBranch
        && pull.BaseSha == BaseShaGitHub)
    {
      return;
    }

    State = state;
    Title = title;
    AuthorLogin = author;
    HeadSha = pull.HeadSha;
    HeadBranch = pull.HeadBranch;
    BaseBranch = pull.BaseBranch;
    BaseShaGitHub = pull.BaseSha;
    Touch(at);
  }

  /// <summary>Records that the pull request was closed; no comment is written on it from now on.</summary>
  /// <param name="merged">Whether it was merged.</param>
  /// <param name="at">The current time; converted to UTC.</param>
  public void Close(bool merged, DateTimeOffset at)
  {
    var state = merged ? PullRequestState.Merged : PullRequestState.Closed;

    if (state != State)
    {
      State = state;
      Touch(at);
    }
  }

  /// <summary>Reads the state a forge reports.</summary>
  /// <param name="state">The state on the forge.</param>
  /// <returns>The state of the pull request.</returns>
  private static PullRequestState ToState(ForgePullState state) =>
    state switch
    {
      ForgePullState.Open => PullRequestState.Open,
      ForgePullState.Merged => PullRequestState.Merged,
      var _ => PullRequestState.Closed,
    };

  /// <summary>Cuts a title that does not fit its column: only a display, it never refuses a pull request.</summary>
  /// <param name="title">The title.</param>
  /// <returns>The title that is kept.</returns>
  private static string Shorten(string title) =>
    title.Length <= TitleMaxLength ? title : title[..(char.IsHighSurrogate(title[TitleMaxLength - 1]) ? TitleMaxLength - 1 : TitleMaxLength)];

  /// <summary>Drops a login that cannot fit its column.</summary>
  /// <param name="login">The login, or <see langword="null" />.</param>
  /// <returns>The login that is kept.</returns>
  private static string? ShortenLogin(string? login) => login?.Length > AuthorLoginMaxLength ? null : login;

  /// <summary>Refuses a branch that does not fit its column: a cut branch would name another branch.</summary>
  /// <param name="pull">What the forge reports.</param>
  /// <exception cref="ArgumentException">A branch is too long.</exception>
  private static void ThrowIfBranchTooLong(ForgePullInfo pull)
  {
    if (pull.HeadBranch.Length > BranchMaxLength || pull.BaseBranch.Length > BranchMaxLength)
    {
      throw new ArgumentException($"A branch cannot be longer than {BranchMaxLength} characters.", nameof(pull));
    }
  }

  /// <summary>Dates a change and increments the version.</summary>
  /// <param name="at">The current time; converted to UTC.</param>
  private void Touch(DateTimeOffset at)
  {
    UpdatedAt = at.ToUniversalTime();
    Version++;
  }

  #endregion
}
