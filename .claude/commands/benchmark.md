---
model: sonnet
---

# TraitSharp Benchmark Skill

You are the TraitSharp benchmark assistant. Interpret the user's natural language request and execute the appropriate benchmark workflow.

## User Request
$ARGUMENTS

## Available Benchmark Classes
- **Sum1DBenchmarks** — 1D array sum (BenchmarkPoint[480000]): NativeArray, NativeSpan, TraitSpan, TraitSpan foreach, TraitNativeSpan
- **Sum2DBenchmarks** — 2D array sum (BenchmarkPoint[800×600]): NativeArray, NativeSpan, TraitSpan2D, TraitSpan2D row, TraitNativeSpan
- **RectSum1DBenchmarks** — 1D strided sum (BenchmarkRect[480000]): Coord, Size, AllFields, ZipForeach
- **RectSum2DBenchmarks** — 2D strided sum (BenchmarkRect[800×600]): Coord, AllFields, BothTraits

## Workflow Commands

### Run benchmarks
Run one or more benchmark classes. Use `--filter` to select specific classes.

```bash
# All benchmarks
cd /Users/randlee/Documents/github/trait-sharp-worktrees/feature/benchmark-reporting
dotnet run --project benchmarks/TraitSharp.Benchmarks -c Release -- --filter '*' --exporters csv

# Specific class (example: RectSum1DBenchmarks)
dotnet run --project benchmarks/TraitSharp.Benchmarks -c Release -- --filter '*RectSum1DBenchmarks*' --exporters csv
```

### Convert results to JSON
After benchmarks complete, convert CSV output to standardized JSON:
```bash
cd /Users/randlee/Documents/github/trait-sharp-worktrees/feature/benchmark-reporting
python3 reports/generate-report.py convert
```

### Generate regression report
Compare latest results against baseline:
```bash
python3 reports/generate-report.py regression
```

### Generate public report
Generate the public-facing benchmark report from baseline data:
```bash
python3 reports/generate-report.py public
```

### Promote baseline
Copy latest data to become the new baseline:
```bash
python3 reports/generate-report.py baseline
```

## Standard Workflow (when user says "run benchmarks" or similar)

Execute this sequence:

1. **Run benchmarks** — Select the appropriate `--filter` based on user request. If unclear, run all.
2. **Convert results** — Run `generate-report.py convert` to produce JSON in `reports/data/latest/`
3. **Analyze results** — Read the JSON files. Compute key ratios. Identify any regressions (>5% slower than baseline) or improvements.
4. **Update scratchpad** — Read `reports/fragments/regression/scratchpad.xhtml` and append a dated analysis entry with:
   - Date/time header: `<h3>YYYY-MM-DD HH:MM — [brief description]</h3>`
   - Update the `scratchpad-meta` paragraph with current timestamp
   - Key findings as bullet points
   - Any regressions flagged with ⚠️
   - Any improvements noted with ✅
5. **Generate regression report** — Run `generate-report.py regression`
6. **Open report** — Use `open reports/Benchmark-Regression-Report.html` to show results

## Interpreting User Requests

Map natural language to actions:
- "run benchmarks" / "run all" → Run all 4 classes, convert, analyze, report
- "run zip benchmarks" / "run rect" → Filter to RectSum1DBenchmarks (has zip)
- "run 1d" / "run sum1d" → Filter to Sum1DBenchmarks
- "run 2d" / "run sum2d" → Filter to Sum2DBenchmarks
- "show report" / "open report" → Generate and open regression report from existing data
- "compare" / "regression" → Generate regression report (baseline vs latest)
- "promote baseline" / "save baseline" → Promote latest to baseline
- "public report" → Generate public report from baseline data
- "what changed" / "any regressions" → Read latest JSON and analyze without re-running

## Key Ratios to Watch
- TraitSpan vs NativeSpan (same work): Target <5% overhead
- TraitSpan2D vs NativeSpan (same work): Target <15% overhead
- ZipForeach vs NativeSpan AllFields: Should be ≤1.0x (zip can beat native)
- Foreach enumerator vs indexer: Foreach should match or beat indexer

## Important Notes
- Always use `MediumRunJob` config (the default FastBenchmarkConfig) — never ShortRun for final numbers
- BenchmarkDotNet ratios compare against the class `[Baseline]`, NOT the semantically correct comparison. Always compute ratios manually using the group baselines from `benchmark-groups.json`.
- The scratchpad file is never overwritten by the generator. AI always appends new entries.
- Working directory: the current worktree or main repo (check `git rev-parse --show-toplevel`)
