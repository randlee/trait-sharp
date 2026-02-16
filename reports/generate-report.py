#!/usr/bin/env python3
"""
TraitSharp Benchmark Report Generator

Standalone tool that:
  1. Converts BenchmarkDotNet CSV output to standardized JSON
  2. Generates public-facing HTML benchmark reports
  3. Generates regression comparison reports (baseline vs latest)

Usage:
  python3 reports/generate-report.py convert [--artifacts PATH]
  python3 reports/generate-report.py public
  python3 reports/generate-report.py regression
"""

import argparse
import csv
import json
import os
import platform
import re
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from html import escape

SCRIPT_DIR = Path(__file__).parent.resolve()
GROUPS_FILE = SCRIPT_DIR / "benchmark-groups.json"
DATA_DIR = SCRIPT_DIR / "data"
BASELINE_DIR = DATA_DIR / "baseline"
LATEST_DIR = DATA_DIR / "latest"
FRAGMENTS_DIR = SCRIPT_DIR / "fragments"
DEFAULT_ARTIFACTS = SCRIPT_DIR.parent / "BenchmarkDotNet.Artifacts" / "results"


# ─── Environment Detection ───────────────────────────────────────────────────

def detect_cpu_id() -> str:
    """Return a slug-friendly CPU identifier."""
    raw = platform.processor() or "unknown"
    # macOS: platform.processor() returns 'arm' — use sysctl
    if sys.platform == "darwin":
        try:
            raw = subprocess.check_output(
                ["sysctl", "-n", "machdep.cpu.brand_string"],
                text=True, stderr=subprocess.DEVNULL
            ).strip()
        except Exception:
            pass
    elif sys.platform == "linux":
        try:
            with open("/proc/cpuinfo") as f:
                for line in f:
                    if line.startswith("model name"):
                        raw = line.split(":", 1)[1].strip()
                        break
        except Exception:
            pass
    return slugify_cpu(raw)


def slugify_cpu(name: str) -> str:
    """Convert CPU brand string to a filename-friendly slug."""
    name = name.lower()
    # Common substitutions
    for pattern, repl in [
        (r"apple\s+m(\d+)\s*(pro|max|ultra)?", r"apple-m\1-\2"),
        (r"intel.*core.*i(\d+)-(\w+)", r"intel-i\1-\2"),
        (r"amd\s+ryzen\s+(\d+)\s+(\w+)", r"amd-ryzen-\1-\2"),
        (r"intel.*xeon.*w-(\w+)", r"xeon-w-\1"),
    ]:
        m = re.search(pattern, name)
        if m:
            slug = m.expand(repl).strip("-").strip()
            return re.sub(r"[^a-z0-9-]", "", slug)
    # Fallback: generic slugify
    slug = re.sub(r"[^a-z0-9]+", "-", name).strip("-")
    return slug[:40] if slug else "unknown-cpu"


def detect_os_slug() -> str:
    s = sys.platform
    if s == "darwin":
        return "macos"
    elif s == "linux":
        return "linux"
    elif s.startswith("win"):
        return "windows"
    return s


def get_environment_info() -> dict:
    """Collect full environment details for JSON metadata."""
    cpu_brand = "Unknown"
    if sys.platform == "darwin":
        try:
            cpu_brand = subprocess.check_output(
                ["sysctl", "-n", "machdep.cpu.brand_string"],
                text=True, stderr=subprocess.DEVNULL
            ).strip()
        except Exception:
            pass
    elif sys.platform == "linux":
        try:
            with open("/proc/cpuinfo") as f:
                for line in f:
                    if line.startswith("model name"):
                        cpu_brand = line.split(":", 1)[1].strip()
                        break
        except Exception:
            pass
    else:
        cpu_brand = platform.processor() or "Unknown"

    cores_logical = os.cpu_count() or 0
    os_version = platform.platform()

    # Try to get .NET SDK version
    dotnet_sdk = "unknown"
    try:
        dotnet_sdk = subprocess.check_output(
            ["dotnet", "--version"], text=True, stderr=subprocess.DEVNULL
        ).strip()
    except Exception:
        pass

    return {
        "os": os_version,
        "osSlug": detect_os_slug(),
        "cpu": cpu_brand,
        "cpuId": detect_cpu_id(),
        "coresLogical": cores_logical,
        "dotnetSdk": dotnet_sdk,
        "runtime": "",  # filled from BDN CSV
        "benchmarkDotNet": "",  # filled from BDN CSV
    }


# ─── CSV Converter ────────────────────────────────────────────────────────────

def convert_bdn_csv(artifacts_dir: Path) -> list[Path]:
    """Convert BenchmarkDotNet CSV files to our JSON format. Returns list of created files."""
    groups_config = json.loads(GROUPS_FILE.read_text())
    env = get_environment_info()
    cpu_id = env["cpuId"]
    os_slug = env["osSlug"]
    timestamp = datetime.now(timezone.utc).isoformat()

    LATEST_DIR.mkdir(parents=True, exist_ok=True)
    created = []

    for bc in groups_config["benchmarkClasses"]:
        class_name = bc["class"]
        csv_file = artifacts_dir / f"TraitSharp.Benchmarks.{class_name}-report.csv"
        if not csv_file.exists():
            print(f"  ⚠ Skipping {class_name} — no CSV found at {csv_file}")
            continue

        # Parse CSV
        methods_data = {}
        runtime_info = ""
        bdn_version = ""
        with open(csv_file, newline="") as f:
            reader = csv.DictReader(f)
            for row in reader:
                name = row.get("Method", "").strip()
                if not name:
                    continue
                mean_str = row.get("Mean", "").strip()
                error_str = row.get("Error", "").strip()
                stddev_str = row.get("StdDev", "").strip()
                allocated_str = row.get("Allocated", "").strip()
                gb_s_str = row.get("GB/s", "").strip()
                ratio_str = row.get("Ratio", "").strip()

                # Parse numeric values (handle units like "168.0 us" or "1.29 us")
                def parse_us(s):
                    if not s or s == "NA" or s == "N/A":
                        return None
                    s = s.replace(",", "").strip()
                    # Normalize Unicode mu (μ) to ASCII u
                    s = s.replace("\u00b5s", "us").replace("\u03bcs", "us")
                    for suffix in [" us", " ns", " ms", " s"]:
                        if s.endswith(suffix):
                            val = float(s[: -len(suffix)])
                            if suffix == " ns":
                                val /= 1000.0
                            elif suffix == " ms":
                                val *= 1000.0
                            elif suffix == " s":
                                val *= 1_000_000.0
                            return round(val, 3)
                    try:
                        return round(float(s), 3)
                    except ValueError:
                        return None

                def parse_bytes(s):
                    if not s or s == "-" or s == "NA":
                        return 0
                    s = s.strip()
                    if s.endswith(" B"):
                        return int(float(s[:-2]))
                    if s.endswith(" KB"):
                        return int(float(s[:-3]) * 1024)
                    try:
                        return int(float(s))
                    except ValueError:
                        return 0

                methods_data[name] = {
                    "mean_us": parse_us(mean_str),
                    "error_us": parse_us(error_str),
                    "stddev_us": parse_us(stddev_str),
                    "gbPerSec": round(float(gb_s_str), 2) if gb_s_str and gb_s_str not in ("NA", "N/A") else None,
                    "allocated": parse_bytes(allocated_str),
                }

                # Try to extract runtime from CSV rows (BDN includes it in some fields)
                rt = row.get("Runtime", "").strip()
                if rt:
                    runtime_info = rt
                job = row.get("Job", "").strip()

        # Try to extract runtime info from log file
        log_pattern = list(artifacts_dir.parent.glob(f"*{class_name}*.log"))
        for log_file in log_pattern:
            try:
                text = log_file.read_text(errors="ignore")[:5000]
                m = re.search(r"BenchmarkDotNet v([\d.]+)", text)
                if m:
                    bdn_version = m.group(1)
                m = re.search(r"\.NET (\d+\.\d+\.\d+)", text)
                if m:
                    runtime_info = f".NET {m.group(1)}"
            except Exception:
                pass

        env_copy = dict(env)
        env_copy["runtime"] = runtime_info
        env_copy["benchmarkDotNet"] = bdn_version

        # Build comparison groups with data
        comparison_groups = []
        for group in bc["comparisonGroups"]:
            group_methods = []
            baseline_name = group.get("baseline", "")
            for method_def in group["methods"]:
                mname = method_def["name"]
                data = methods_data.get(mname, {})
                group_methods.append({
                    "name": mname,
                    "label": method_def["label"],
                    "codeSnippet": method_def["codeSnippet"],
                    "isBaseline": mname == baseline_name,
                    "mean_us": data.get("mean_us"),
                    "error_us": data.get("error_us"),
                    "stddev_us": data.get("stddev_us"),
                    "gbPerSec": data.get("gbPerSec"),
                    "allocated": data.get("allocated", 0),
                })
            comparison_groups.append({
                "id": group["id"],
                "name": group["name"],
                "description": group["description"],
                "baseline": baseline_name,
                "methods": group_methods,
            })

        # Write JSON
        out_json = {
            "metadata": {
                "benchmarkClass": class_name,
                "title": bc["title"],
                "description": bc["description"],
                "elementType": bc["elementType"],
                "elementSize": bc["elementSize"],
                "arrayLength": bc["arrayLength"],
                "totalBytes": bc["totalBytes"],
                "timestamp": timestamp,
                "environment": env_copy,
            },
            "comparisonGroups": comparison_groups,
        }

        out_path = LATEST_DIR / f"{class_name}.{cpu_id}.{os_slug}.json"
        out_path.write_text(json.dumps(out_json, indent=2) + "\n")
        created.append(out_path)
        print(f"  ✅ {out_path.name} ({len(methods_data)} methods)")

    return created


# ─── HTML Generation Helpers ──────────────────────────────────────────────────

CSS = """
<style>
  :root {
    --bg: #ffffff; --fg: #1a1a2e; --accent: #0f3460;
    --green: #27ae60; --red: #e74c3c; --yellow: #f39c12;
    --border: #dfe6e9; --code-bg: #f8f9fa; --table-stripe: #f5f7fa;
    --header-bg: #0f3460; --header-fg: #ffffff;
  }
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
         color: var(--fg); background: var(--bg); line-height: 1.6; max-width: 1200px;
         margin: 0 auto; padding: 2rem; }
  h1 { color: var(--accent); border-bottom: 3px solid var(--accent); padding-bottom: 0.5rem;
       margin-bottom: 1rem; font-size: 1.8rem; }
  h2 { color: var(--accent); margin: 2rem 0 0.5rem; font-size: 1.4rem;
       border-bottom: 1px solid var(--border); padding-bottom: 0.3rem; }
  h3 { color: var(--fg); margin: 1.5rem 0 0.5rem; font-size: 1.1rem; }
  p { margin: 0.5rem 0; }
  .subtitle { color: #636e72; font-size: 0.9rem; margin-bottom: 1.5rem; }
  .env-table { width: 100%; border-collapse: collapse; margin: 1rem 0; font-size: 0.85rem; }
  .env-table th { background: var(--table-stripe); text-align: left; padding: 0.4rem 0.8rem;
                  border: 1px solid var(--border); width: 150px; }
  .env-table td { padding: 0.4rem 0.8rem; border: 1px solid var(--border); }
  .benchmark-table { width: 100%; border-collapse: collapse; margin: 1rem 0; font-size: 0.85rem; }
  .benchmark-table th { background: var(--header-bg); color: var(--header-fg);
                        padding: 0.5rem 0.8rem; text-align: right; border: 1px solid var(--border); }
  .benchmark-table th:first-child { text-align: left; }
  .benchmark-table td { padding: 0.5rem 0.8rem; text-align: right; border: 1px solid var(--border); }
  .benchmark-table td:first-child { text-align: left; font-weight: 500; }
  .benchmark-table tr:nth-child(even) { background: var(--table-stripe); }
  .benchmark-table tr.baseline { font-weight: 600; }
  .benchmark-table .faster { color: var(--green); font-weight: 600; }
  .benchmark-table .slower { color: var(--red); font-weight: 600; }
  .benchmark-table .neutral { color: #636e72; }
  pre { background: var(--code-bg); border: 1px solid var(--border); border-radius: 4px;
        padding: 0.8rem 1rem; overflow-x: auto; font-size: 0.8rem; line-height: 1.5;
        margin: 0.5rem 0; }
  code { font-family: 'SF Mono', 'Fira Code', 'Consolas', monospace; }
  .group-section { margin: 1.5rem 0; padding: 1rem; border: 1px solid var(--border);
                   border-radius: 6px; background: #fefefe; }
  .group-description { color: #636e72; font-size: 0.9rem; margin-bottom: 0.5rem; }
  .code-comparison { display: grid; grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
                     gap: 0.8rem; margin: 1rem 0; }
  .code-card { border: 1px solid var(--border); border-radius: 4px; overflow: hidden; }
  .code-card-header { background: var(--table-stripe); padding: 0.3rem 0.6rem;
                      font-size: 0.8rem; font-weight: 600; border-bottom: 1px solid var(--border); }
  .code-card pre { margin: 0; border: none; border-radius: 0; }
  .delta-positive { color: var(--green); }
  .delta-negative { color: var(--red); }
  .stale-banner { background: #ffeaa7; border: 1px solid var(--yellow); border-radius: 4px;
                  padding: 0.5rem 1rem; margin: 1rem 0; font-size: 0.9rem; }
  .summary-pass { background: #d4edda; border: 1px solid var(--green); border-radius: 4px;
                  padding: 0.5rem 1rem; margin: 1rem 0; }
  .summary-fail { background: #f8d7da; border: 1px solid var(--red); border-radius: 4px;
                  padding: 0.5rem 1rem; margin: 1rem 0; }
  .note { border-left: 3px solid var(--accent); padding: 0.8rem 1rem; margin: 1rem 0;
          background: var(--code-bg); border-radius: 0 4px 4px 0; }
  .note h3 { margin-top: 0; font-size: 0.95rem; }
  .note-meta { font-size: 0.75rem; color: #636e72; }
  .scratchpad { border: 2px dashed var(--border); border-radius: 6px; padding: 1rem;
                margin: 2rem 0; background: #fffdf5; }
  .scratchpad h2 { border-bottom: 1px dashed var(--border); color: #636e72; }
  .footer { margin-top: 3rem; padding-top: 1rem; border-top: 1px solid var(--border);
            font-size: 0.75rem; color: #636e72; text-align: center; }
  .platform-columns { display: grid; gap: 0; }
  .root-cause td { font-style: italic; color: #636e72; font-size: 0.8rem;
                   padding: 0.2rem 0.8rem 0.4rem; background: #fffdf5; border-top: none; }
</style>
"""


def html_header(title: str, subtitle: str = "") -> str:
    now = datetime.now().strftime("%Y-%m-%d %H:%M")
    sub = f'<p class="subtitle">{escape(subtitle)}</p>' if subtitle else ""
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>{escape(title)}</title>
  {CSS}
</head>
<body>
  <h1>{escape(title)}</h1>
  {sub}
  <p class="subtitle">Generated: {now}</p>
"""


def html_footer() -> str:
    return """
  <div class="footer">
    Generated by TraitSharp Benchmark Report Generator
  </div>
</body>
</html>
"""


def html_env_table(env: dict) -> str:
    rows = ""
    for label, key in [
        ("CPU", "cpu"), ("OS", "os"), ("Logical Cores", "coresLogical"),
        (".NET SDK", "dotnetSdk"), ("Runtime", "runtime"),
        ("BenchmarkDotNet", "benchmarkDotNet"),
    ]:
        val = env.get(key, "—")
        if val:
            rows += f"    <tr><th>{escape(str(label))}</th><td>{escape(str(val))}</td></tr>\n"
    return f'  <table class="env-table">\n{rows}  </table>\n'


def fmt_us(val) -> str:
    if val is None:
        return "—"
    return f"{val:,.1f} µs"


def fmt_gbs(val) -> str:
    if val is None:
        return "—"
    return f"{val:.2f}"


def fmt_alloc(val) -> str:
    if val is None or val == 0:
        return "0 B"
    if val >= 1024:
        return f"{val / 1024:.1f} KB"
    return f"{val} B"


def compute_ratio(method_mean, baseline_mean):
    """Returns (ratio_str, css_class) comparing method to baseline."""
    if method_mean is None or baseline_mean is None or baseline_mean == 0:
        return "—", "neutral"
    ratio = method_mean / baseline_mean
    if abs(ratio - 1.0) < 0.015:
        return "1.00x", "neutral"
    elif ratio < 1.0:
        return f"{1.0 / ratio:.2f}x faster", "faster"
    else:
        return f"{ratio:.2f}x slower", "slower"


def html_code_cards(methods: list) -> str:
    cards = ""
    for m in methods:
        snippet = escape(m.get("codeSnippet", ""))
        cards += f"""    <div class="code-card">
      <div class="code-card-header">{escape(m['label'])}</div>
      <pre><code class="language-csharp">{snippet}</code></pre>
    </div>
"""
    return f'  <div class="code-comparison">\n{cards}  </div>\n'


def html_data_table_public(group: dict) -> str:
    """Render a benchmark results table for the public report."""
    baseline_name = group.get("baseline", "")
    baseline_mean = None
    for m in group["methods"]:
        if m["name"] == baseline_name:
            baseline_mean = m.get("mean_us")
            break

    header = """  <table class="benchmark-table">
    <tr>
      <th>Method</th><th>Mean</th><th>Error</th><th>StdDev</th>
      <th>vs Baseline</th><th>GB/s</th><th>Allocated</th>
    </tr>
"""
    rows = ""
    for m in group["methods"]:
        is_bl = m.get("isBaseline", False)
        ratio_str, ratio_class = compute_ratio(m.get("mean_us"), baseline_mean)
        if is_bl:
            ratio_str = "baseline"
            ratio_class = "neutral"
        tr_class = ' class="baseline"' if is_bl else ""
        rows += f"""    <tr{tr_class}>
      <td>{escape(m['label'])}</td>
      <td>{fmt_us(m.get('mean_us'))}</td>
      <td>±{fmt_us(m.get('error_us'))}</td>
      <td>{fmt_us(m.get('stddev_us'))}</td>
      <td class="{ratio_class}">{ratio_str}</td>
      <td>{fmt_gbs(m.get('gbPerSec'))}</td>
      <td>{fmt_alloc(m.get('allocated', 0))}</td>
    </tr>
"""
    return header + rows + "  </table>\n"


def _load_root_cause_lookup() -> dict[str, str]:
    """Load rootCause annotations from benchmark-groups.json. Returns {method_name: rootCause}."""
    lookup = {}
    try:
        groups_config = json.loads(GROUPS_FILE.read_text())
        for bc in groups_config.get("benchmarkClasses", []):
            for group in bc.get("comparisonGroups", []):
                for method in group.get("methods", []):
                    rc = method.get("rootCause", "")
                    if rc:
                        lookup[method["name"]] = rc
    except Exception:
        pass
    return lookup


# Module-level cache (populated on first use)
_ROOT_CAUSE_CACHE: dict[str, str] | None = None


def get_root_cause(method_name: str) -> str:
    """Get rootCause for a method, or empty string if none."""
    global _ROOT_CAUSE_CACHE
    if _ROOT_CAUSE_CACHE is None:
        _ROOT_CAUSE_CACHE = _load_root_cause_lookup()
    return _ROOT_CAUSE_CACHE.get(method_name, "")


def html_data_table_regression(group_baseline: dict, group_latest: dict) -> str:
    """Render a baseline vs latest comparison table for regression report.

    Includes:
      - Temporal delta columns (baseline vs latest)
      - 'vs Group Baseline' column showing within-group ratio (TraitSpan vs Span)
      - Root cause annotation rows for methods with rootCause in benchmark-groups.json
    """
    bl_by_name = {m["name"]: m for m in group_baseline.get("methods", [])}
    lt_by_name = {m["name"]: m for m in group_latest.get("methods", [])}
    all_names = []
    seen = set()
    for m in group_latest.get("methods", []) + group_baseline.get("methods", []):
        if m["name"] not in seen:
            all_names.append(m["name"])
            seen.add(m["name"])

    # Find within-group baseline mean from latest data
    group_baseline_name = group_latest.get("baseline", "")
    group_baseline_mean = None
    for m in group_latest.get("methods", []):
        if m["name"] == group_baseline_name:
            group_baseline_mean = m.get("mean_us")
            break

    num_cols = 8  # total columns for root-cause colspan

    header = """  <table class="benchmark-table">
    <tr>
      <th>Method</th>
      <th>Baseline (µs)</th><th>Current (µs)</th>
      <th>Δ (µs)</th><th>Δ (%)</th>
      <th>vs Group Baseline</th>
      <th>Baseline GB/s</th><th>Current GB/s</th>
    </tr>
"""
    rows = ""
    for name in all_names:
        bl = bl_by_name.get(name, {})
        lt = lt_by_name.get(name, {})
        label = lt.get("label", bl.get("label", name))
        bl_mean = bl.get("mean_us")
        lt_mean = lt.get("mean_us")
        is_group_baseline = (name == group_baseline_name)

        # Temporal delta (baseline vs latest)
        if bl_mean is not None and lt_mean is not None:
            delta = lt_mean - bl_mean
            pct = (delta / bl_mean) * 100 if bl_mean != 0 else 0
            if pct < -1.5:
                delta_class = "delta-positive"
                delta_str = f"{delta:+.1f}"
                pct_str = f"{pct:+.1f}%"
            elif pct > 1.5:
                delta_class = "delta-negative"
                delta_str = f"{delta:+.1f}"
                pct_str = f"{pct:+.1f}%"
            else:
                delta_class = "neutral"
                delta_str = f"{delta:+.1f}"
                pct_str = f"{pct:+.1f}%"
        else:
            delta_class = "neutral"
            delta_str = "—"
            pct_str = "—"

        # Within-group ratio (vs group baseline)
        if is_group_baseline:
            ratio_str = "baseline"
            ratio_class = "neutral"
        else:
            ratio_str, ratio_class = compute_ratio(lt_mean, group_baseline_mean)

        tr_class = ' class="baseline"' if is_group_baseline else ""
        rows += f"""    <tr{tr_class}>
      <td>{escape(label)}</td>
      <td>{fmt_us(bl_mean)}</td>
      <td>{fmt_us(lt_mean)}</td>
      <td class="{delta_class}">{delta_str}</td>
      <td class="{delta_class}">{pct_str}</td>
      <td class="{ratio_class}">{ratio_str}</td>
      <td>{fmt_gbs(bl.get('gbPerSec'))}</td>
      <td>{fmt_gbs(lt.get('gbPerSec'))}</td>
    </tr>
"""
        # Root cause annotation row
        root_cause = get_root_cause(name)
        if root_cause:
            rows += f'    <tr class="root-cause"><td colspan="{num_cols}">{escape(root_cause)}</td></tr>\n'

    return header + rows + "  </table>\n"


# ─── Report Generators ────────────────────────────────────────────────────────

def load_data_files(directory: Path) -> dict[str, dict]:
    """Load all JSON data files from a directory. Returns {className: data}."""
    result = {}
    if not directory.exists():
        return result
    for f in sorted(directory.glob("*.json")):
        try:
            data = json.loads(f.read_text())
            class_name = data["metadata"]["benchmarkClass"]
            result[class_name] = data
        except (json.JSONDecodeError, KeyError) as e:
            print(f"  ⚠ Skipping {f.name}: {e}")
    return result


def generate_public_report(data_dir: Path = BASELINE_DIR):
    """Generate the public-facing TraitSharp Benchmark Report."""
    data_files = load_data_files(data_dir)
    if not data_files:
        print(f"  ❌ No data files found in {data_dir}")
        return None

    html = html_header(
        "TraitSharp Benchmark Report",
        "Performance comparison: native Span<T> vs TraitSpan trait-based views"
    )

    # Environment from first data file
    first = next(iter(data_files.values()))
    env = first["metadata"]["environment"]
    html += "  <h2>Environment</h2>\n"
    html += html_env_table(env)

    ts = first["metadata"].get("timestamp", "")
    if ts:
        html += f'  <p class="subtitle">Data collected: {escape(ts[:19].replace("T", " "))} UTC</p>\n'

    # Each benchmark class
    for class_name, data in data_files.items():
        meta = data["metadata"]
        html += f'\n  <h2>{escape(meta["title"])}</h2>\n'
        html += f'  <p class="group-description">{escape(meta["description"])}</p>\n'
        html += f'  <p class="group-description"><strong>Array:</strong> {meta["elementType"]}[{meta["arrayLength"]:,}] ({meta["totalBytes"]:,} bytes total)</p>\n'

        for group in data["comparisonGroups"]:
            html += f'\n  <div class="group-section">\n'
            html += f'    <h3>{escape(group["name"])}</h3>\n'
            html += f'    <p class="group-description">{escape(group["description"])}</p>\n'
            html += html_code_cards(group["methods"])
            html += html_data_table_public(group)
            html += "  </div>\n"

    # Notes fragment
    notes_file = FRAGMENTS_DIR / "public" / "notes.xhtml"
    if notes_file.exists():
        html += "\n  <h2>Notes</h2>\n"
        html += notes_file.read_text()

    html += html_footer()

    out_path = SCRIPT_DIR / "TraitSharp-Benchmark-Report.html"
    out_path.write_text(html)
    print(f"  ✅ Public report: {out_path}")
    return out_path


def generate_regression_report():
    """Generate the Benchmark Regression Report comparing baseline to latest."""
    baseline_data = load_data_files(BASELINE_DIR)
    latest_data = load_data_files(LATEST_DIR)

    if not latest_data:
        print("  ❌ No latest data files found. Run benchmarks first.")
        return None

    html = html_header("Benchmark Regression Report", "Baseline vs Current comparison")

    # Summary banner
    has_baseline = bool(baseline_data)
    if not has_baseline:
        html += '  <div class="stale-banner">⚠️ No baseline data found. Showing current results only. Run <code>generate-report.py baseline</code> to set baseline.</div>\n'

    # Environment from latest
    first_latest = next(iter(latest_data.values()))
    env = first_latest["metadata"]["environment"]
    html += "  <h2>Environment</h2>\n"
    html += html_env_table(env)

    bl_ts = ""
    if baseline_data:
        first_bl = next(iter(baseline_data.values()))
        bl_ts = first_bl["metadata"].get("timestamp", "unknown")
    lt_ts = first_latest["metadata"].get("timestamp", "unknown")
    html += f'  <p class="subtitle">Baseline: {escape(bl_ts[:19] if bl_ts else "none")} | Current: {escape(lt_ts[:19])}</p>\n'

    # Regression summary
    regressions = []
    improvements = []
    for class_name, lt_data in latest_data.items():
        bl_data = baseline_data.get(class_name)
        if not bl_data:
            continue
        bl_groups = {g["id"]: g for g in bl_data["comparisonGroups"]}
        for lt_group in lt_data["comparisonGroups"]:
            bl_group = bl_groups.get(lt_group["id"])
            if not bl_group:
                continue
            bl_by_name = {m["name"]: m for m in bl_group["methods"]}
            for m in lt_group["methods"]:
                bl_m = bl_by_name.get(m["name"])
                if not bl_m or bl_m.get("mean_us") is None or m.get("mean_us") is None:
                    continue
                pct = ((m["mean_us"] - bl_m["mean_us"]) / bl_m["mean_us"]) * 100
                if pct > 5:
                    regressions.append((m["label"], pct))
                elif pct < -5:
                    improvements.append((m["label"], pct))

    if has_baseline:
        if regressions:
            html += f'  <div class="summary-fail">❌ <strong>{len(regressions)} regression(s) detected</strong> (>5% slower): '
            html += ", ".join(f"{name} ({pct:+.1f}%)" for name, pct in regressions)
            html += "</div>\n"
        else:
            html += '  <div class="summary-pass">✅ <strong>No regressions detected</strong> — all methods within 5% of baseline</div>\n'
        if improvements:
            html += f'  <div class="summary-pass">🚀 <strong>{len(improvements)} improvement(s)</strong>: '
            html += ", ".join(f"{name} ({pct:+.1f}%)" for name, pct in improvements)
            html += "</div>\n"

    # Per-class comparison
    for class_name, lt_data in latest_data.items():
        meta = lt_data["metadata"]
        html += f'\n  <h2>{escape(meta["title"])}</h2>\n'

        bl_data = baseline_data.get(class_name)
        bl_groups = {g["id"]: g for g in bl_data["comparisonGroups"]} if bl_data else {}

        for lt_group in lt_data["comparisonGroups"]:
            html += f'\n  <div class="group-section">\n'
            html += f'    <h3>{escape(lt_group["name"])}</h3>\n'
            html += f'    <p class="group-description">{escape(lt_group["description"])}</p>\n'
            html += html_code_cards(lt_group["methods"])

            bl_group = bl_groups.get(lt_group["id"])
            if bl_group:
                html += html_data_table_regression(bl_group, lt_group)
            else:
                html += '    <p><em>No baseline data for this group — showing current only</em></p>\n'
                html += html_data_table_public(lt_group)
            html += "  </div>\n"

    # Scratchpad fragment
    scratchpad_file = FRAGMENTS_DIR / "regression" / "scratchpad.xhtml"
    if scratchpad_file.exists():
        html += '\n  <div class="scratchpad">\n'
        html += scratchpad_file.read_text()
        html += "\n  </div>\n"

    html += html_footer()

    out_path = SCRIPT_DIR / "Benchmark-Regression-Report.html"
    out_path.write_text(html)
    print(f"  ✅ Regression report: {out_path}")
    return out_path


def promote_baseline():
    """Copy latest data to baseline."""
    if not LATEST_DIR.exists() or not list(LATEST_DIR.glob("*.json")):
        print("  ❌ No latest data to promote. Run benchmarks first.")
        return
    BASELINE_DIR.mkdir(parents=True, exist_ok=True)
    for f in LATEST_DIR.glob("*.json"):
        dest = BASELINE_DIR / f.name
        dest.write_text(f.read_text())
        print(f"  📋 {f.name} → baseline/")
    print("  ✅ Baseline updated. Run 'public' to regenerate the public report.")


# ─── CLI ──────────────────────────────────────────────────────────────────────

def main():
    parser = argparse.ArgumentParser(
        description="TraitSharp Benchmark Report Generator",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Commands:
  convert     Convert BenchmarkDotNet CSV to JSON (reports/data/latest/)
  public      Generate public-facing HTML report from baseline data
  regression  Generate regression report (baseline vs latest)
  baseline    Promote latest data to baseline
        """,
    )
    parser.add_argument("command", choices=["convert", "public", "regression", "baseline"],
                        help="Command to run")
    parser.add_argument("--artifacts", type=Path, default=DEFAULT_ARTIFACTS,
                        help=f"Path to BenchmarkDotNet results dir (default: {DEFAULT_ARTIFACTS})")

    args = parser.parse_args()

    print(f"TraitSharp Benchmark Report Generator")
    print(f"  Command: {args.command}")
    print()

    if args.command == "convert":
        print(f"  Converting from: {args.artifacts}")
        created = convert_bdn_csv(args.artifacts)
        if created:
            print(f"\n  Created {len(created)} JSON file(s) in {LATEST_DIR}")
    elif args.command == "public":
        generate_public_report()
    elif args.command == "regression":
        generate_regression_report()
    elif args.command == "baseline":
        promote_baseline()


if __name__ == "__main__":
    main()
