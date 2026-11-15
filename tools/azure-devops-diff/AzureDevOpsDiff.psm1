# ------------------------------------------------------------------------------------------------
# AzureDevOpsDiff - the pure functions of the Azure DevOps diff spike (M7.01): reading git diff -U0,
# turning lineDiffBlocks and two blobs into changed lines, comparing them, and scrubbing what is
# written. No network and no git here: Measure-AzureDevOpsDiff.ps1 does the calls, and the offline
# self-test (Test-AzureDevOpsDiff.ps1) checks these functions.
# ------------------------------------------------------------------------------------------------
Set-StrictMode -Version Latest

if (-not ('CodeCoverageSpike.LineDiff' -as [type])) {
  Add-Type -Language CSharp -TypeDefinition @'
namespace CodeCoverageSpike
{
  using System;
  using System.Collections.Generic;

  // A port of the line diff of git's xdiff (xprepare.c, xdiffi.c) with its default options: trimmed
  // ends, discarded lines without a match, the bidirectional Myers split with its cost heuristics,
  // and the group compaction with or without the indent heuristic. What M7.46 would write in C#,
  // with no package, to reproduce git diff from two blobs.
  public static class LineDiff
  {
    private const int MaxEqLimit = 1024, SimScanWindow = 100, KpdisRun = 4, MaxCostMin = 256, SnakeCnt = 20, HeurMinCost = 256, KHeur = 4;
    private const long LineMax = long.MaxValue / 4;

    private sealed class XFile
    {
      public string[] Recs; public int[] Ha; public bool[] Raw; public int N;
      public int DStart, DEnd, NReff; public int[] RIndex; public int[] HaReduced;
      public bool Chg(long i) { return Raw[i + 1]; }
      public void Set(long i, bool v) { Raw[i + 1] = v; }
    }

    private struct Split { public long I1, I2; public bool MinLo, MinHi; }

    public static bool[][] Diff(string[] a, string[] b, bool indentHeuristic)
    {
      var classes = new Dictionary<string, int>(StringComparer.Ordinal);
      var len1 = new List<int>(); var len2 = new List<int>();
      XFile x1 = Prepare(a, classes, len1, len2, true), x2 = Prepare(b, classes, len1, len2, false);
      TrimEnds(x1, x2);
      Cleanup(x1, x2, len1, len2);

      long ndiags = x1.NReff + x2.NReff + 3;
      var kvd = new long[2 * ndiags + 2];
      long kvdfOff = x2.NReff + 1, kvdbOff = kvdfOff + ndiags;
      long mxcost = BogoSqrt(ndiags); if (mxcost < MaxCostMin) mxcost = MaxCostMin;
      RecsCmp(x1, 0, x1.NReff, x2, 0, x2.NReff, kvd, kvdfOff, kvdbOff, false, mxcost);

      Compact(x1, x2, indentHeuristic);
      Compact(x2, x1, indentHeuristic);
      var removed = new bool[x1.N]; var added = new bool[x2.N];
      for (int i = 0; i < x1.N; i++) removed[i] = x1.Chg(i);
      for (int j = 0; j < x2.N; j++) added[j] = x2.Chg(j);
      return new[] { removed, added };
    }

    private static XFile Prepare(string[] recs, Dictionary<string, int> classes, List<int> len1, List<int> len2, bool first)
    {
      var f = new XFile { Recs = recs, N = recs.Length, Ha = new int[recs.Length], Raw = new bool[recs.Length + 2] };
      for (int i = 0; i < recs.Length; i++)
      {
        int c;
        if (!classes.TryGetValue(recs[i], out c)) { c = classes.Count; classes[recs[i]] = c; len1.Add(0); len2.Add(0); }
        f.Ha[i] = c;
        if (first) len1[c]++; else len2[c]++;
      }
      return f;
    }

    private static long BogoSqrt(long n) { long i; for (i = 1; n > 0; n >>= 2) i <<= 1; return i; }

    private static void TrimEnds(XFile x1, XFile x2)
    {
      int i, lim = Math.Min(x1.N, x2.N);
      for (i = 0; i < lim; i++) if (x1.Ha[i] != x2.Ha[i]) break;
      x1.DStart = x2.DStart = i;
      int j;
      for (lim -= i, j = 0; j < lim; j++) if (x1.Ha[x1.N - 1 - j] != x2.Ha[x2.N - 1 - j]) break;
      x1.DEnd = x1.N - j - 1; x2.DEnd = x2.N - j - 1;
    }

    private static bool CleanMatch(byte[] dis, long i, long s, long e)
    {
      if (i - s > SimScanWindow) s = i - SimScanWindow;
      if (e - i > SimScanWindow) e = i + SimScanWindow;
      long r, rdis0 = 0, rpdis0 = 1, rdis1 = 0, rpdis1 = 1;
      for (r = 1; i - r >= s; r++) { if (dis[i - r] == 0) rdis0++; else if (dis[i - r] == 2) rpdis0++; else break; }
      if (rdis0 == 0) return false;
      for (r = 1; i + r <= e; r++) { if (dis[i + r] == 0) rdis1++; else if (dis[i + r] == 2) rpdis1++; else break; }
      if (rdis1 == 0) return false;
      rdis1 += rdis0; rpdis1 += rpdis0;
      return rpdis1 * KpdisRun < (rpdis1 + rdis1);
    }

    private static void Cleanup(XFile x1, XFile x2, List<int> len1, List<int> len2)
    {
      var dis1 = new byte[x1.N + 1]; var dis2 = new byte[x2.N + 1];
      long mlim = BogoSqrt(x1.N); if (mlim > MaxEqLimit) mlim = MaxEqLimit;
      for (int i = x1.DStart; i <= x1.DEnd; i++) { int nm = len2[x1.Ha[i]]; dis1[i] = (byte)(nm == 0 ? 0 : nm >= mlim ? 2 : 1); }
      mlim = BogoSqrt(x2.N); if (mlim > MaxEqLimit) mlim = MaxEqLimit;
      for (int i = x2.DStart; i <= x2.DEnd; i++) { int nm = len1[x2.Ha[i]]; dis2[i] = (byte)(nm == 0 ? 0 : nm >= mlim ? 2 : 1); }
      Reduce(x1, dis1); Reduce(x2, dis2);
    }

    private static void Reduce(XFile f, byte[] dis)
    {
      var rindex = new List<int>(); var ha = new List<int>();
      for (int i = f.DStart; i <= f.DEnd; i++)
      {
        if (dis[i] == 1 || (dis[i] == 2 && !CleanMatch(dis, i, f.DStart, f.DEnd))) { rindex.Add(i); ha.Add(f.Ha[i]); }
        else f.Set(i, true);
      }
      f.RIndex = rindex.ToArray(); f.HaReduced = ha.ToArray(); f.NReff = f.RIndex.Length;
    }

    private static void RecsCmp(XFile d1, long off1, long lim1, XFile d2, long off2, long lim2, long[] kvd, long fo, long bo, bool needMin, long mxcost)
    {
      int[] ha1 = d1.HaReduced, ha2 = d2.HaReduced;
      for (; off1 < lim1 && off2 < lim2 && ha1[off1] == ha2[off2]; off1++, off2++) { }
      for (; off1 < lim1 && off2 < lim2 && ha1[lim1 - 1] == ha2[lim2 - 1]; lim1--, lim2--) { }
      if (off1 == lim1) { for (; off2 < lim2; off2++) d2.Set(d2.RIndex[off2], true); }
      else if (off2 == lim2) { for (; off1 < lim1; off1++) d1.Set(d1.RIndex[off1], true); }
      else
      {
        Split spl = SplitBox(ha1, off1, lim1, ha2, off2, lim2, kvd, fo, bo, needMin, mxcost);
        RecsCmp(d1, off1, spl.I1, d2, off2, spl.I2, kvd, fo, bo, spl.MinLo, mxcost);
        RecsCmp(d1, spl.I1, lim1, d2, spl.I2, lim2, kvd, fo, bo, spl.MinHi, mxcost);
      }
    }

    private static Split SplitBox(int[] ha1, long off1, long lim1, int[] ha2, long off2, long lim2, long[] kvd, long fo, long bo, bool needMin, long mxcost)
    {
      long dmin = off1 - lim2, dmax = lim1 - off2, fmid = off1 - off2, bmid = lim1 - lim2;
      bool odd = ((fmid - bmid) & 1) != 0;
      long fmin = fmid, fmax = fmid, bmin = bmid, bmax = bmid, ec, d, i1, i2, prev1, best, dd, v, k;
      var spl = new Split();
      kvd[fo + fmid] = off1; kvd[bo + bmid] = lim1;
      for (ec = 1; ; ec++)
      {
        bool gotSnake = false;
        if (fmin > dmin) kvd[fo + (--fmin) - 1] = -1; else ++fmin;
        if (fmax < dmax) kvd[fo + (++fmax) + 1] = -1; else --fmax;
        for (d = fmax; d >= fmin; d -= 2)
        {
          if (kvd[fo + d - 1] >= kvd[fo + d + 1]) i1 = kvd[fo + d - 1] + 1; else i1 = kvd[fo + d + 1];
          prev1 = i1; i2 = i1 - d;
          for (; i1 < lim1 && i2 < lim2 && ha1[i1] == ha2[i2]; i1++, i2++) { }
          if (i1 - prev1 > SnakeCnt) gotSnake = true;
          kvd[fo + d] = i1;
          if (odd && bmin <= d && d <= bmax && kvd[bo + d] <= i1) { spl.I1 = i1; spl.I2 = i2; spl.MinLo = spl.MinHi = true; return spl; }
        }
        if (bmin > dmin) kvd[bo + (--bmin) - 1] = LineMax; else ++bmin;
        if (bmax < dmax) kvd[bo + (++bmax) + 1] = LineMax; else --bmax;
        for (d = bmax; d >= bmin; d -= 2)
        {
          if (kvd[bo + d - 1] < kvd[bo + d + 1]) i1 = kvd[bo + d - 1]; else i1 = kvd[bo + d + 1] - 1;
          prev1 = i1; i2 = i1 - d;
          for (; i1 > off1 && i2 > off2 && ha1[i1 - 1] == ha2[i2 - 1]; i1--, i2--) { }
          if (prev1 - i1 > SnakeCnt) gotSnake = true;
          kvd[bo + d] = i1;
          if (!odd && fmin <= d && d <= fmax && i1 <= kvd[fo + d]) { spl.I1 = i1; spl.I2 = i2; spl.MinLo = spl.MinHi = true; return spl; }
        }
        if (needMin) continue;
        if (gotSnake && ec > HeurMinCost)
        {
          for (best = 0, d = fmax; d >= fmin; d -= 2)
          {
            dd = d > fmid ? d - fmid : fmid - d;
            i1 = kvd[fo + d]; i2 = i1 - d;
            v = (i1 - off1) + (i2 - off2) - dd;
            if (v > KHeur * ec && v > best && off1 + SnakeCnt <= i1 && i1 < lim1 && off2 + SnakeCnt <= i2 && i2 < lim2)
              for (k = 1; ha1[i1 - k] == ha2[i2 - k]; k++)
                if (k == SnakeCnt) { best = v; spl.I1 = i1; spl.I2 = i2; break; }
          }
          if (best > 0) { spl.MinLo = true; spl.MinHi = false; return spl; }
          for (best = 0, d = bmax; d >= bmin; d -= 2)
          {
            dd = d > bmid ? d - bmid : bmid - d;
            i1 = kvd[bo + d]; i2 = i1 - d;
            v = (lim1 - i1) + (lim2 - i2) - dd;
            if (v > KHeur * ec && v > best && off1 < i1 && i1 <= lim1 - SnakeCnt && off2 < i2 && i2 <= lim2 - SnakeCnt)
              for (k = 0; ha1[i1 + k] == ha2[i2 + k]; k++)
                if (k == SnakeCnt - 1) { best = v; spl.I1 = i1; spl.I2 = i2; break; }
          }
          if (best > 0) { spl.MinLo = false; spl.MinHi = true; return spl; }
        }
        if (ec >= mxcost)
        {
          long fbest = -1, fbest1 = -1, bbest = LineMax, bbest1 = LineMax;
          for (d = fmax; d >= fmin; d -= 2)
          {
            i1 = Math.Min(kvd[fo + d], lim1); i2 = i1 - d;
            if (lim2 < i2) { i1 = lim2 + d; i2 = lim2; }
            if (fbest < i1 + i2) { fbest = i1 + i2; fbest1 = i1; }
          }
          for (d = bmax; d >= bmin; d -= 2)
          {
            i1 = Math.Max(off1, kvd[bo + d]); i2 = i1 - d;
            if (i2 < off2) { i1 = off2 + d; i2 = off2; }
            if (i1 + i2 < bbest) { bbest = i1 + i2; bbest1 = i1; }
          }
          if ((lim1 + lim2) - bbest < fbest - (off1 + off2)) { spl.I1 = fbest1; spl.I2 = fbest - fbest1; spl.MinLo = true; spl.MinHi = false; }
          else { spl.I1 = bbest1; spl.I2 = bbest - bbest1; spl.MinLo = false; spl.MinHi = true; }
          return spl;
        }
      }
    }

    private struct Group { public long Start, End; }

    private static void GroupInit(XFile f, ref Group g) { g.Start = g.End = 0; while (f.Chg(g.End)) g.End++; }

    private static bool GroupNext(XFile f, ref Group g)
    {
      if (g.End == f.N) return false;
      g.Start = g.End + 1;
      for (g.End = g.Start; f.Chg(g.End); g.End++) { }
      return true;
    }

    private static bool GroupPrevious(XFile f, ref Group g)
    {
      if (g.Start == 0) return false;
      g.End = g.Start - 1;
      for (g.Start = g.End; f.Chg(g.Start - 1); g.Start--) { }
      return true;
    }

    private static bool SlideDown(XFile f, ref Group g)
    {
      if (g.End < f.N && f.Ha[g.Start] == f.Ha[g.End])
      {
        f.Set(g.Start++, false); f.Set(g.End++, true);
        while (f.Chg(g.End)) g.End++;
        return true;
      }
      return false;
    }

    private static bool SlideUp(XFile f, ref Group g)
    {
      if (g.Start > 0 && f.Ha[g.Start - 1] == f.Ha[g.End - 1])
      {
        f.Set(--g.Start, true); f.Set(--g.End, false);
        while (f.Chg(g.Start - 1)) g.Start--;
        return true;
      }
      return false;
    }

    private static void Compact(XFile f, XFile o, bool indentHeuristic)
    {
      var g = new Group(); var go = new Group();
      GroupInit(f, ref g); GroupInit(o, ref go);
      while (true)
      {
        if (g.End != g.Start)
        {
          long groupSize, earliestEnd, endMatchingOther;
          do
          {
            groupSize = g.End - g.Start;
            endMatchingOther = -1;
            while (SlideUp(f, ref g)) if (!GroupPrevious(o, ref go)) throw new InvalidOperationException("group sync broken sliding up");
            earliestEnd = g.End;
            if (go.End > go.Start) endMatchingOther = g.End;
            while (true)
            {
              if (!SlideDown(f, ref g)) break;
              if (!GroupNext(o, ref go)) throw new InvalidOperationException("group sync broken sliding down");
              if (go.End > go.Start) endMatchingOther = g.End;
            }
          } while (groupSize != g.End - g.Start);

          if (g.End == earliestEnd) { }
          else if (endMatchingOther != -1)
          {
            while (go.End == go.Start)
            {
              if (!SlideUp(f, ref g)) throw new InvalidOperationException("match disappeared");
              if (!GroupPrevious(o, ref go)) throw new InvalidOperationException("group sync broken sliding to match");
            }
          }
          else if (indentHeuristic)
          {
            long shift = earliestEnd, bestShift = -1;
            int bestIndent = 0, bestPenalty = 0;
            if (g.End - groupSize - 1 > shift) shift = g.End - groupSize - 1;
            if (g.End - 100 > shift) shift = g.End - 100;
            for (; shift <= g.End; shift++)
            {
              int indent = 0, penalty = 0;
              ScoreAddSplit(f, shift, ref indent, ref penalty);
              ScoreAddSplit(f, shift - groupSize, ref indent, ref penalty);
              int cmp = 60 * ((indent > bestIndent ? 1 : 0) - (indent < bestIndent ? 1 : 0)) + (penalty - bestPenalty);
              if (bestShift == -1 || cmp <= 0) { bestIndent = indent; bestPenalty = penalty; bestShift = shift; }
            }
            while (g.End > bestShift)
            {
              if (!SlideUp(f, ref g)) throw new InvalidOperationException("best shift unreached");
              if (!GroupPrevious(o, ref go)) throw new InvalidOperationException("group sync broken sliding to blank line");
            }
          }
        }
        if (!GroupNext(f, ref g)) break;
        if (!GroupNext(o, ref go)) throw new InvalidOperationException("group sync broken moving to next group");
      }
    }

    private static int GetIndent(string rec)
    {
      int ret = 0;
      foreach (char c in rec)
      {
        bool space = c == ' ' || c == '\t' || c == '\n' || c == '\v' || c == '\f' || c == '\r';
        if (!space) return ret;
        if (c == ' ') ret += 1; else if (c == '\t') ret += 8 - ret % 8;
        if (ret >= 200) return 200;
      }
      return -1;
    }

    private static void ScoreAddSplit(XFile f, long split, ref int effectiveIndent, ref int penalty)
    {
      bool endOfFile; int indent, preBlank = 0, preIndent = -1, postBlank = 0, postIndent = -1;
      if (split >= f.N) { endOfFile = true; indent = -1; } else { endOfFile = false; indent = GetIndent(f.Recs[split]); }
      for (long i = split - 1; i >= 0; i--)
      {
        preIndent = GetIndent(f.Recs[i]);
        if (preIndent != -1) break;
        preBlank++;
        if (preBlank == 20) { preIndent = 0; break; }
      }
      for (long i = split + 1; i < f.N; i++)
      {
        postIndent = GetIndent(f.Recs[i]);
        if (postIndent != -1) break;
        postBlank++;
        if (postBlank == 20) { postIndent = 0; break; }
      }

      if (preIndent == -1 && preBlank == 0) penalty += 1;
      if (endOfFile) penalty += 21;
      int pb = indent == -1 ? 1 + postBlank : 0;
      int totalBlank = preBlank + pb;
      penalty += -30 * totalBlank;
      penalty += 6 * pb;
      int ind = indent != -1 ? indent : postIndent;
      bool anyBlanks = totalBlank != 0;
      effectiveIndent += ind;
      if (ind == -1) { }
      else if (preIndent == -1) { }
      else if (ind > preIndent) penalty += anyBlanks ? 10 : -4;
      else if (ind == preIndent) { }
      else if (postIndent != -1 && postIndent > ind) penalty += anyBlanks ? 17 : 24;
      else penalty += anyBlanks ? 17 : 23;
    }
  }
}
'@
}

function Split-GitLine {
  <#
  .SYNOPSIS
    Splits the bytes of a blob into lines as git counts them: line feeds only, each line keeping its
    terminator, so "x" and "x`n" differ (the "\ No newline at end of file" case).
  .OUTPUTS
    string[] decoded as Latin-1, byte for byte.
  #>
  param([byte[]] $Bytes)

  if ($null -eq $Bytes -or $Bytes.Length -eq 0) { return , [string[]] @() }

  $text = [System.Text.Encoding]::Latin1.GetString($Bytes)
  $lines = [System.Collections.Generic.List[string]]::new()
  $start = 0
  while ($start -lt $text.Length) {
    $lf = $text.IndexOf("`n", $start)
    if ($lf -lt 0) { $lines.Add($text.Substring($start)); break }
    $lines.Add($text.Substring($start, $lf - $start + 1))
    $start = $lf + 1
  }

  return , $lines.ToArray()
}

function Test-BinaryContent {
  <#
  .SYNOPSIS
    Git's heuristic: a NUL byte in the first 8000 bytes makes a blob binary.
  #>
  param([byte[]] $Bytes)

  if ($null -eq $Bytes) { return $false }
  $limit = [Math]::Min($Bytes.Length, 8000)
  for ($i = 0; $i -lt $limit; $i++) { if ($Bytes[$i] -eq 0) { return $true } }
  return $false
}

function New-LineSet {
  # The changed lines of one file: Added numbered in the new file, Removed in the old one (1-based).
  param([int[]] $Added = @(), [int[]] $Removed = @())

  return [pscustomobject]@{ Added = [int[]] @($Added | Sort-Object -Unique); Removed = [int[]] @($Removed | Sort-Object -Unique) }
}

function Get-BlobLineDiff {
  <#
  .SYNOPSIS
    The local path (C): the changed lines between two blobs, by the port of git's xdiff.
  .PARAMETER NoIndentHeuristic
    Leaves out the indent heuristic, as git diff --no-indent-heuristic.
  #>
  param([byte[]] $Old, [byte[]] $New, [switch] $NoIndentHeuristic)

  $a = Split-GitLine $Old
  $b = Split-GitLine $New
  $result = [CodeCoverageSpike.LineDiff]::Diff($a, $b, -not $NoIndentHeuristic)

  $removed = for ($i = 0; $i -lt $a.Length; $i++) { if ($result[0][$i]) { $i + 1 } }
  $added = for ($j = 0; $j -lt $b.Length; $j++) { if ($result[1][$j]) { $j + 1 } }
  return New-LineSet -Added @($added) -Removed @($removed)
}

function ConvertFrom-LineDiffBlock {
  <#
  .SYNOPSIS
    The FileDiffs path (B): the changed lines of the lineDiffBlocks of one file. Blocks of type None
    (0, "none") are unchanged; Add (1), Delete (2) and Edit (3) give their modified and original
    ranges.
  #>
  param([object[]] $Blocks)

  $added = [System.Collections.Generic.List[int]]::new()
  $removed = [System.Collections.Generic.List[int]]::new()
  foreach ($block in @($Blocks)) {
    if ($null -eq $block) { continue }
    $type = "$($block.changeType)".ToLowerInvariant()
    if ($type -in @('0', 'none')) { continue }
    for ($i = 0; $i -lt [int] $block.modifiedLinesCount; $i++) { $added.Add([int] $block.modifiedLineNumberStart + $i) }
    for ($i = 0; $i -lt [int] $block.originalLinesCount; $i++) { $removed.Add([int] $block.originalLineNumberStart + $i) }
  }

  return New-LineSet -Added $added.ToArray() -Removed $removed.ToArray()
}

function ConvertFrom-GitPath {
  <#
  .SYNOPSIS
    Reads a path of a git diff header: C-style quoted (backslash escapes, octal bytes of UTF-8) or
    bare, without its a/ or b/ prefix.
  #>
  param([string] $Text)

  if ($null -eq $Text) { return $null }
  $value = $Text
  if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
    $bytes = [System.Collections.Generic.List[byte]]::new()
    $inner = $value.Substring(1, $value.Length - 2)
    $escapes = @{ [char] 'n' = 10; [char] 't' = 9; [char] 'r' = 13; [char] 'a' = 7; [char] 'b' = 8; [char] 'f' = 12; [char] 'v' = 11 }
    for ($k = 0; $k -lt $inner.Length; $k++) {
      $c = $inner[$k]
      if ($c -ne [char] '\' -or $k -eq $inner.Length - 1) { $bytes.AddRange([System.Text.Encoding]::UTF8.GetBytes([string] $c)); continue }
      $k++
      $e = $inner[$k]
      if ($e -match '[0-7]' -and $k + 2 -lt $inner.Length) { $bytes.Add([Convert]::ToByte($inner.Substring($k, 3), 8)); $k += 2 }
      elseif ($escapes.ContainsKey($e)) { $bytes.Add([byte] $escapes[$e]) }
      else { $bytes.AddRange([System.Text.Encoding]::UTF8.GetBytes([string] $e)) }
    }
    $value = [System.Text.Encoding]::UTF8.GetString($bytes.ToArray())
  }
  if ($value.StartsWith('a/') -or $value.StartsWith('b/')) { return $value.Substring(2) }
  return $value
}

function ConvertFrom-GitDiff {
  <#
  .SYNOPSIS
    Reads the output of git diff -U0 (default a/ and b/ prefixes, core.quotePath=false) into one
    entry per file, keyed by its new path (its old path when it was deleted).
  .OUTPUTS
    An ordered dictionary: path -> { Path, OldPath, Status, Binary, Lines }.
  #>
  param([string] $Text)

  $files = [ordered]@{}
  $current = $null
  $added = $null
  $removed = $null

  $flush = {
    if ($null -ne $current) {
      $current.Lines = New-LineSet -Added $added.ToArray() -Removed $removed.ToArray()
      $key = if ($current.Status -eq 'deleted') { $current.OldPath } else { $current.Path }
      if ($null -eq $key) { $key = $current.OldPath ?? $current.Path }
      if ($null -ne $key) { $files[$key] = $current }
    }
  }

  foreach ($line in ($Text -split "`n")) {
    $line = $line.TrimEnd("`r")
    if ($line.StartsWith('diff --git ')) {
      & $flush
      $rest = $line.Substring(11)
      $oldPath = $null
      $path = $null
      if ($rest.Contains('"') -and $rest -match '^("(?:[^"\\]|\\.)*"|a/\S*) ("(?:[^"\\]|\\.)*"|b/\S*)$') {
        $oldPath = ConvertFrom-GitPath $Matches[1]
        $path = ConvertFrom-GitPath $Matches[2]
      }
      else {
        # Without a rename both halves are equal: a/<p> b/<p>, so the split is in the middle.
        $half = [int] (($rest.Length - 1) / 2)
        if ($rest.Length % 2 -eq 1 -and $rest.Substring(0, $half) -ceq ('a/' + $rest.Substring($half + 3))) { $path = $rest.Substring($half + 3); $oldPath = $path }
      }
      $current = [pscustomobject]@{ Path = $path; OldPath = $oldPath; Status = 'modified'; Binary = $false; Lines = $null }
      $added = [System.Collections.Generic.List[int]]::new()
      $removed = [System.Collections.Generic.List[int]]::new()
      continue
    }
    if ($null -eq $current) { continue }

    if ($line.StartsWith('rename from ')) { $current.OldPath = ConvertFrom-GitPath $line.Substring(12); $current.Status = 'renamed' }
    elseif ($line.StartsWith('rename to ')) { $current.Path = ConvertFrom-GitPath $line.Substring(10); $current.Status = 'renamed' }
    elseif ($line.StartsWith('copy from ')) { $current.OldPath = ConvertFrom-GitPath $line.Substring(10); $current.Status = 'copied' }
    elseif ($line.StartsWith('copy to ')) { $current.Path = ConvertFrom-GitPath $line.Substring(8); $current.Status = 'copied' }
    elseif ($line.StartsWith('new file mode')) { $current.Status = 'added' }
    elseif ($line.StartsWith('deleted file mode')) { $current.Status = 'deleted' }
    elseif ($line.StartsWith('Binary files ')) { $current.Binary = $true }
    elseif ($line.StartsWith('--- ') -and $line -ne '--- /dev/null' -and $null -eq $current.OldPath) { $current.OldPath = ConvertFrom-GitPath $line.Substring(4) }
    elseif ($line.StartsWith('+++ ') -and $line -ne '+++ /dev/null' -and $null -eq $current.Path) { $current.Path = ConvertFrom-GitPath $line.Substring(4) }
    elseif ($line -match '^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@') {
      $oldStart = [int] $Matches[1]; $oldCount = if ($Matches[2]) { [int] $Matches[2] } else { 1 }
      $newStart = [int] $Matches[3]; $newCount = if ($Matches[4]) { [int] $Matches[4] } else { 1 }
      for ($i = 0; $i -lt $oldCount; $i++) { $removed.Add($oldStart + $i) }
      for ($i = 0; $i -lt $newCount; $i++) { $added.Add($newStart + $i) }
    }
  }
  & $flush

  # An added file has OldPath = Path from the header; a deleted one keeps it as its key.
  foreach ($file in $files.Values) {
    if ($file.Status -eq 'added') { $file.OldPath = $null }
    if ($file.Status -eq 'deleted') { $file.Path = $null }
  }

  return $files
}

function Compare-LineSet {
  <#
  .SYNOPSIS
    Compares the changed lines of a path with the reference of git.
  .OUTPUTS
    'match', 'added-differ' (the lines patch coverage reads differ), or 'removed-differ' (only the
    removed lines differ, which moves LineShifter but not the patch).
  #>
  param($Reference, $Candidate)

  if ((@($Reference.Added) -join ',') -ne (@($Candidate.Added) -join ',')) { return 'added-differ' }
  if ((@($Reference.Removed) -join ',') -ne (@($Candidate.Removed) -join ',')) { return 'removed-differ' }
  return 'match'
}

function Get-PathHash {
  # A short, stable stand-in for a file path in the anonymized report.
  param([string] $Path)

  if ([string]::IsNullOrEmpty($Path)) { return '' }
  $bytes = [System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($Path))
  $extension = [System.IO.Path]::GetExtension($Path)
  return ([Convert]::ToHexString($bytes, 0, 4).ToLowerInvariant()) + $extension
}

function Protect-Metadata {
  <#
  .SYNOPSIS
    Scrubs a JSON text of metadata before it is written: identity fields are blanked, the names of
    the organization, the project and the repository are replaced, every GUID becomes a placeholder
    (the same GUID always gets the same one) and every e-mail becomes user@example.com.
  #>
  param([string] $Json, [hashtable] $Names, [hashtable] $GuidMap)

  $text = $Json
  $text = [regex]::Replace($text, '"(displayName|uniqueName|email|mailAddress|imageUrl|descriptor|directoryAlias|principalName)"\s*:\s*"[^"]*"', {
      param($m)
      return '"' + $m.Groups[1].Value + '": "<scrubbed>"'
    })
  foreach ($key in @($Names.Keys | Sort-Object { $_.Length } -Descending)) {
    if ([string]::IsNullOrWhiteSpace($key)) { continue }
    $text = [regex]::Replace($text, [regex]::Escape($key), $Names[$key], 'IgnoreCase')
    $encoded = [Uri]::EscapeDataString($key)
    if ($encoded -ne $key) { $text = [regex]::Replace($text, [regex]::Escape($encoded), $Names[$key], 'IgnoreCase') }
  }
  $text = [regex]::Replace($text, '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', {
      param($m)
      $guid = $m.Value.ToLowerInvariant()
      if (-not $GuidMap.ContainsKey($guid)) { $GuidMap[$guid] = '00000000-0000-0000-0000-{0:d12}' -f ($GuidMap.Count + 1) }
      return $GuidMap[$guid]
    })
  $text = [regex]::Replace($text, '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}', 'user@example.com')
  return $text
}

function Find-ForbiddenText {
  <#
  .SYNOPSIS
    Looks for any forbidden string (case-insensitive) in a text about to be written.
  .OUTPUTS
    The labels of the forbidden strings found (never their values), empty when the text is clean.
  #>
  param([string] $Text, [hashtable] $Forbidden)

  $found = foreach ($label in $Forbidden.Keys) {
    foreach ($value in @($Forbidden[$label])) {
      if (-not [string]::IsNullOrWhiteSpace($value) -and $Text.IndexOf($value, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $label; break }
    }
  }
  return , [string[]] @($found)
}

Export-ModuleMember -Function Split-GitLine, Test-BinaryContent, New-LineSet, Get-BlobLineDiff, ConvertFrom-LineDiffBlock,
  ConvertFrom-GitPath, ConvertFrom-GitDiff, Compare-LineSet, Get-PathHash, Protect-Metadata, Find-ForbiddenText
