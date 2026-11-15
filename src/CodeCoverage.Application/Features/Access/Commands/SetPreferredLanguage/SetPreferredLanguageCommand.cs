// ------------------------------------------------------------------------------------------------
// <copyright file="SetPreferredLanguageCommand.cs" company="Gaëtan Thouvenin">
//   Copyright (c) Gaëtan Thouvenin. All rights reserved.
// </copyright>
// ------------------------------------------------------------------------------------------------

using Mediator;

using CodeCoverage.Application.Security;

namespace CodeCoverage.Application.Features.Access.Commands.SetPreferredLanguage;

/// <summary>Remembers the language of the console chosen by the signed-in user, for every browser (M4.02u).</summary>
/// <param name="Language">
///   The two-letter code of the language, in lower case. The Web checks it against the languages of the console; the
///   Application only checks its form.
/// </param>
public sealed record SetPreferredLanguageCommand(string Language) : ICommand, IUserScoped;
