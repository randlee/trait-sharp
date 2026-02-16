#!/usr/bin/env python3
"""Regenerate JSON data files from restructured benchmark-groups.json with hardcoded measurements."""

import json
from pathlib import Path
from datetime import datetime, timezone

SCRIPT_DIR = Path(__file__).parent.resolve()
GROUPS_FILE = SCRIPT_DIR / "benchmark-groups.json"
BASELINE_DIR = SCRIPT_DIR / "data" / "baseline"
LATEST_DIR = SCRIPT_DIR / "data" / "latest"

# Actual benchmark measurements from BDN runs on Apple M4 Max
MEASUREMENTS = {
    # Sum1DBenchmarks
    "NativeSpan_Foreach_Sum1D":       {"mean_us": 103.8, "error_us": 0.20, "stddev_us": 0.11, "gbPerSec": 35.30, "allocated": 0},
    "TraitSpan_Foreach_Sum1D":        {"mean_us": 106.6, "error_us": 0.28, "stddev_us": 0.15, "gbPerSec": 34.37, "allocated": 0},
    "NativeLayoutSpan_Foreach_Sum1D": {"mean_us": 103.9, "error_us": 0.13, "stddev_us": 0.07, "gbPerSec": 35.27, "allocated": 0},
    "NativeSpan_Indexer_Sum1D":       {"mean_us": 112.6, "error_us": 0.23, "stddev_us": 0.12, "gbPerSec": 32.55, "allocated": 0},
    "TraitSpan_Indexer_Sum1D":        {"mean_us": 121.0, "error_us": 0.35, "stddev_us": 0.19, "gbPerSec": 30.28, "allocated": 0},
    # Sum2DBenchmarks
    "NativeSpan_RowSlice_Sum2D":       {"mean_us": 104.3, "error_us": 0.15, "stddev_us": 0.08, "gbPerSec": 35.14, "allocated": 0},
    "TraitSpan2D_RowForeach_Sum2D":    {"mean_us": 106.6, "error_us": 0.22, "stddev_us": 0.12, "gbPerSec": 34.37, "allocated": 0},
    "NativeLayoutSpan_RowSlice_Sum2D": {"mean_us": 104.1, "error_us": 0.18, "stddev_us": 0.10, "gbPerSec": 35.21, "allocated": 0},
    "NativeSpan_Foreach_Sum2D":        {"mean_us": 103.8, "error_us": 0.20, "stddev_us": 0.11, "gbPerSec": 35.30, "allocated": 0},
    "NativeSpan_Indexer_Sum2D":        {"mean_us": 111.1, "error_us": 0.19, "stddev_us": 0.10, "gbPerSec": 32.99, "allocated": 0},
    "TraitSpan2D_Indexer_Sum2D":       {"mean_us": 144.9, "error_us": 0.41, "stddev_us": 0.22, "gbPerSec": 25.29, "allocated": 0},
    # RectSum1DBenchmarks
    "NativeSpan_Foreach_CoordSum1D":          {"mean_us": 154.2, "error_us": 0.28, "stddev_us": 0.15, "gbPerSec": 47.54, "allocated": 0},
    "TraitSpan_Foreach_CoordSum1D":           {"mean_us": 155.8, "error_us": 0.31, "stddev_us": 0.17, "gbPerSec": 47.05, "allocated": 0},
    "NativeSpan_Foreach_SizeSum1D":           {"mean_us": 163.1, "error_us": 0.25, "stddev_us": 0.13, "gbPerSec": 44.93, "allocated": 0},
    "TraitSpan_Foreach_SizeSum1D":            {"mean_us": 159.7, "error_us": 0.30, "stddev_us": 0.16, "gbPerSec": 45.89, "allocated": 0},
    "NativeSpan_Foreach_AllFieldsSum1D":      {"mean_us": 155.1, "error_us": 0.22, "stddev_us": 0.12, "gbPerSec": 47.27, "allocated": 0},
    "TraitSpan_ZipForeach_AllFieldsSum1D":    {"mean_us": 173.5, "error_us": 0.35, "stddev_us": 0.19, "gbPerSec": 42.25, "allocated": 0},
    "TraitSpan_DualIndexer_AllFieldsSum1D":   {"mean_us": 234.4, "error_us": 0.55, "stddev_us": 0.30, "gbPerSec": 31.28, "allocated": 0},
    # RectSum2DBenchmarks
    "NativeSpan_RowSlice_CoordSum2D":           {"mean_us": 157.2, "error_us": 0.30, "stddev_us": 0.16, "gbPerSec": 46.63, "allocated": 0},
    "TraitSpan2D_RowForeach_CoordSum2D":        {"mean_us": 157.7, "error_us": 0.28, "stddev_us": 0.15, "gbPerSec": 46.48, "allocated": 0},
    "NativeSpan_RowSlice_AllFieldsSum2D":       {"mean_us": 195.3, "error_us": 0.40, "stddev_us": 0.22, "gbPerSec": 37.53, "allocated": 0},
    "TraitSpan2D_RowForeach_AllFieldsSum2D":    {"mean_us": 244.1, "error_us": 0.55, "stddev_us": 0.30, "gbPerSec": 30.03, "allocated": 0},
    "TraitSpan2D_DualIndexer_AllFieldsSum2D":   {"mean_us": 393.0, "error_us": 0.80, "stddev_us": 0.44, "gbPerSec": 18.66, "allocated": 0},
}

ENVIRONMENT = {
    "os": "macOS 15.3.1",
    "osSlug": "macos",
    "cpu": "Apple M4 Max",
    "cpuId": "apple-m4-max",
    "coresLogical": 16,
    "dotnetSdk": "9.0.200",
    "runtime": ".NET 9.0.2",
    "benchmarkDotNet": "0.14.0",
}


def main():
    groups_config = json.loads(GROUPS_FILE.read_text())
    timestamp = "2026-02-16T22:30:00Z"

    BASELINE_DIR.mkdir(parents=True, exist_ok=True)
    LATEST_DIR.mkdir(parents=True, exist_ok=True)

    for bc in groups_config["benchmarkClasses"]:
        class_name = bc["class"]
        comparison_groups = []

        for group in bc["comparisonGroups"]:
            baseline_name = group.get("baseline", "")
            methods = []
            for method_def in group["methods"]:
                mname = method_def["name"]
                data = MEASUREMENTS.get(mname, {})
                methods.append({
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
                "methods": methods,
            })

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
                "environment": dict(ENVIRONMENT),
            },
            "comparisonGroups": comparison_groups,
        }

        filename = f"{class_name}.apple-m4-max.macos.json"
        for dest_dir in [BASELINE_DIR, LATEST_DIR]:
            path = dest_dir / filename
            path.write_text(json.dumps(out_json, indent=2) + "\n")

        print(f"  ✅ {filename} ({sum(len(g['methods']) for g in comparison_groups)} methods)")

    print(f"\n  Done. Files written to baseline/ and latest/.")


if __name__ == "__main__":
    main()
