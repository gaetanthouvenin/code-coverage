// The annotated source page (M3.29): colours the whole file once with the vendored highlight.js, then hands each line
// its share of the coloured HTML, and moves to the next line missed or partially hit.
//
// Only HTML that highlight.js produced is assigned: it escapes the text of the source, which comes from the forge and is
// never trusted. The language comes from the extension of the path; highlightAuto is never used.

const LANGUAGES = {
  bash: 'bash', sh: 'bash', zsh: 'bash',
  c: 'c', h: 'c',
  cc: 'cpp', cpp: 'cpp', cxx: 'cpp', hh: 'cpp', hpp: 'cpp', hxx: 'cpp',
  cs: 'csharp',
  go: 'go',
  java: 'java',
  cjs: 'javascript', js: 'javascript', jsx: 'javascript', mjs: 'javascript',
  json: 'json',
  kt: 'kotlin', kts: 'kotlin',
  php: 'php',
  py: 'python',
  rb: 'ruby',
  rs: 'rust',
  scala: 'scala', sc: 'scala',
  sql: 'sql',
  swift: 'swift',
  mts: 'typescript', cts: 'typescript', ts: 'typescript', tsx: 'typescript',
  csproj: 'xml', htm: 'xml', html: 'xml', props: 'xml', svg: 'xml', targets: 'xml', xaml: 'xml', xml: 'xml',
  yaml: 'yaml', yml: 'yaml',
};

const TOKEN = /<span class="[^"]*">|<\/span>|\n|[^<\n]+/g;

let core;

/** The language of a path, from its extension, or null when none of the subset fits. */
export function languageOf(path) {
  const name = path.slice(path.lastIndexOf('/') + 1).toLowerCase();
  const dot = name.lastIndexOf('.');

  return dot < 0 ? null : LANGUAGES[name.slice(dot + 1)] ?? null;
}

async function load(language) {
  if (!core) {
    core = (await import('../lib/highlightjs/core.min.js')).default;
    core.configure({ classPrefix: 'cc-hl-' });
  }

  if (!core.getLanguage(language)) {
    core.registerLanguage(language, (await import(`../lib/highlightjs/languages/${language}.min.js`)).default);
  }

  return core;
}

/** Cuts coloured HTML into lines, closing the spans a line leaves open and opening them again on the next one. */
export function splitLines(html) {
  const lines = [];
  const open = [];
  let line = '';

  for (const [token] of html.matchAll(TOKEN)) {
    if (token === '\n') {
      lines.push(line + '</span>'.repeat(open.length));
      line = open.join('');
    } else if (token === '</span>') {
      open.pop();
      line += token;
    } else {
      if (token.startsWith('<span')) {
        open.push(token);
      }

      line += token;
    }
  }

  lines.push(line + '</span>'.repeat(open.length));

  return lines;
}

/** Colours the code cells of the table: the whole file at once, so a comment or a string spanning lines stays right. */
export async function highlight(table, path) {
  const language = languageOf(path);

  if (!table || !language) {
    return false;
  }

  const cells = table.querySelectorAll('td.cc-source__code');
  const text = Array.from(cells, cell => cell.textContent).join('\n');
  const hljs = await load(language);
  const lines = splitLines(hljs.highlight(text, { language, ignoreIllegals: true }).value);

  cells.forEach((cell, index) => {
    cell.innerHTML = lines[index] ?? '';
  });

  return true;
}

/** Moves to the next line missed or partially hit after the one last reached, from the top after the end. */
export function nextUncovered(table) {
  if (!table) {
    return 0;
  }

  const rows = Array.from(table.querySelectorAll('tr[data-state="miss"], tr[data-state="partial"]'));

  if (rows.length === 0) {
    return 0;
  }

  const current = Number(table.dataset.lastLine ?? 0);
  const next = rows.find(row => Number(row.dataset.line) > current) ?? rows[0];

  table.dataset.lastLine = next.dataset.line;
  next.scrollIntoView({ block: 'center' });
  next.focus({ preventScroll: true });

  return Number(next.dataset.line);
}
