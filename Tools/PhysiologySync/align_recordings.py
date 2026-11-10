"""Join one session's valid eye frames to nearest clock-mapped respiration samples.

No interpolation or filling missing samples. Raw input CSVs are never changed.
"""
import argparse
from bisect import bisect_left
import csv
import json
import math
from pathlib import Path


def csv_rows(path):
    with Path(path).open(encoding="utf-8-sig", newline="") as stream:
        yield from csv.DictReader(line for line in stream if not line.startswith("#"))


def align(eye_csv, output, max_delta=0.1):
    eye_csv, output = Path(eye_csv), Path(output)
    if not math.isfinite(max_delta) or max_delta < 0:
        raise ValueError("max_delta must be finite and nonnegative")
    prefix = eye_csv.with_suffix("")
    breath_csv = Path(str(prefix) + "_respiration.csv")
    manifest = json.loads(Path(str(prefix) + "_session.json").read_text(encoding="utf-8"))
    if manifest["eye_csv"] != eye_csv.name or manifest["session_id"] != prefix.name:
        raise ValueError("Session identity mismatch")
    if output.resolve() in {eye_csv.resolve(), breath_csv.resolve(), Path(str(prefix) + "_session.json").resolve(), Path(str(prefix) + "_clock_sync.csv").resolve()}:
        raise ValueError("Output must not overwrite source recording files")
    samples = sorted((r for r in csv_rows(breath_csv) if r["kind"] == "sample" and r["sync_valid"] == "1"
                      and r["sequence_state"] == "in_order" and r["app_active"] == "1"),
                     key=lambda r: float(r["app_seconds"]))
    times = [float(r["app_seconds"]) for r in samples]
    extra = ["session_id", "resp_matched", "resp_app_seconds", "resp_minus_eye_seconds", "resp_value",
             "resp_unit", "resp_sequence", "resp_time_basis", "resp_rtt_seconds"]
    matched = total = 0
    # Exclusive creation protects previous analyses and raw data.
    with output.open("x", encoding="utf-8-sig", newline="") as stream:
        writer = None
        for row in csv_rows(eye_csv):
            if writer is None:
                writer = csv.DictWriter(stream, list(row) + extra)
                writer.writeheader()
            total += 1
            t = float(row["app_seconds"])
            i = bisect_left(times, t)
            candidates = [j for j in (i - 1, i) if 0 <= j < len(times)]
            best = min(candidates, key=lambda j: abs(times[j] - t)) if candidates else None
            result = dict.fromkeys(extra, "")
            result.update(session_id=manifest["session_id"], resp_matched="0")
            if row["valid"] == "1" and best is not None and abs(times[best] - t) <= max_delta:
                b = samples[best]
                result.update(resp_matched="1", resp_app_seconds=b["app_seconds"],
                              resp_minus_eye_seconds=repr(times[best] - t), resp_value=b["value"],
                              resp_unit=b["unit"], resp_sequence=b["sequence"],
                              resp_time_basis=b["time_basis"], resp_rtt_seconds=b["rtt_seconds"])
                matched += 1
            writer.writerow(dict(row, **result))
    return {"eye_rows": total, "matched_rows": matched, "unmatched_rows": total - matched,
            "maximum_pair_delta_seconds": max_delta}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("eye_csv")
    parser.add_argument("output_csv")
    parser.add_argument("--max-delta", type=float, default=0.1,
                        help="Maximum allowed pairing difference in seconds; not an accuracy claim")
    args = parser.parse_args()
    print(json.dumps(align(args.eye_csv, args.output_csv, args.max_delta), indent=2))
