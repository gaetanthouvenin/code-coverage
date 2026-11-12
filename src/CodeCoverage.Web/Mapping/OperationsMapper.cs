// ------------------------------------------------------------------------------------------------
// <copyright file="OperationsMapper.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Features.Operations.Queries.ListAuditEntries;
using CodeCoverage.Domain.Audit;
using CodeCoverage.Web.ViewModels.Operations;

namespace CodeCoverage.Web.Mapping;

/// <summary>Reads the operations of the instance for the pages and converts them to view models: no page names a Domain type.</summary>
internal static class OperationsMapper
{
  #region Fields

  /// <summary>The number of entries of a page of the audit log.</summary>
  public const int AuditPageSize = ListAuditEntriesQuery.DefaultPageSize;

  /// <summary>The value of the filter that keeps every family.</summary>
  public const string AllFamilies = "all";

  // The actions with a label of their own; any other one is shown with its raw code.
  private static readonly HashSet<string> LabelledActions = new(StringComparer.Ordinal)
  {
    AuditCategories.ReplaceSecretAction,
    AuditCategories.ResetSettingAction,
    "Settings.Update",
    "Settings.CompleteSetup",
    "Settings.CompleteGitHubAppManifest",
    "Organization.Admit",
    "Organization.Suspend",
    "ApiTokens.Create",
    "ApiTokens.Revoke",
    AuditCategories.RetryJobAction,
  };

  #endregion

  #region Properties

  /// <summary>Gets the families the filter offers, as their names.</summary>
  public static IReadOnlyList<string> Families { get; } = [.. Enum.GetNames<AuditCategory>()];

  #endregion

  #region Methods

  /// <summary>Reads one page of the audit log of the instance.</summary>
  /// <param name="sender">The request sender.</param>
  /// <param name="family">The name of the family kept, or <see cref="AllFamilies" />; an unknown one keeps every family.</param>
  /// <param name="search">A part of the actor, the action or the target, or <see langword="null" />; cut to its longest allowed length.</param>
  /// <param name="page">The page asked; kept within the pages a list can hold.</param>
  /// <param name="cancellationToken">A token to cancel the reading.</param>
  /// <returns>The page.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="sender" /> is <see langword="null" />.</exception>
  public static async Task<AuditPageViewModel> GetAuditPageViewModelAsync(
    this ISender sender,
    string? family,
    string? search,
    int page,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(sender);

    AuditCategory? category = Enum.TryParse<AuditCategory>(family, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
    var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

    if (term?.Length > ListAuditEntriesQuery.SearchMaxLength)
    {
      term = term[..ListAuditEntriesQuery.SearchMaxLength];
    }

    var query = new ListAuditEntriesQuery(category, term, Math.Clamp(page, 1, ListAuditEntriesQuery.MaxPage), AuditPageSize);
    var entries = await sender.Send(query, cancellationToken).ConfigureAwait(false);

    return new AuditPageViewModel([.. entries.Items.Select(ToViewModel)], entries.Page, entries.PageSize, entries.TotalCount);
  }

  private static AuditEntryViewModel ToViewModel(AuditEntrySummary entry)
  {
    // A reset of a secret is the clearing of that secret: it has its own label.
    var label = entry.Category is AuditCategory.Secrets && entry.Action == AuditCategories.ResetSettingAction
                  ? "AuditAction_Settings_ClearSecret"
                  : LabelledActions.Contains(entry.Action) ? "AuditAction_" + entry.Action.Replace('.', '_') : null;

    return new AuditEntryViewModel(entry.Id, entry.At, entry.Actor, entry.Action, label, entry.Category?.ToString(), entry.Target);
  }

  #endregion
}
