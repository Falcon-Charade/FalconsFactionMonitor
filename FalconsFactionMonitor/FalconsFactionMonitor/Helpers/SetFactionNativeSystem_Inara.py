#!/usr/bin/env python3
"""
SetFactionNativeSystem_Inara.py — Map factions to Native System using INARA.

What it does
------------
- Connects to SQL Server (pyodbc) and loads ref.System(SystemID, SystemName) into memory
- For each faction name:
  * Searches INARA: https://inara.cz/elite/minorfaction/?search=<name>
  * Opens the faction page, parses "Origin" (Native System)
  * (If present) parses "Player minor faction: Yes/No" and sets IsPlayer = 1 when Yes
  * Resolves SystemID from DB and APPENDS an UPDATE statement immediately
- Resume-safe (skips names already written), supports --retry-misses, and adds COMMIT at the end

Usage (Azure SQL example)
-------------------------
python SetFactionNativeSystem_Inara.py factions.txt ^
  --conn "Driver={ODBC Driver 18 for SQL Server};Server=tcp:falcons-sql.database.windows.net,1433;Database=FalconsFactionMonitor;Uid=<uid>;Pwd=<pwd>;Encrypt=yes;TrustServerCertificate=no;Login Timeout=30;Connection Timeout=60" ^
  -o update_native_system_ids.sql

Notes
-----
- Default delay between factions is 2.0 seconds (per your request).
- Be polite: do not reduce the delay unless necessary.
"""

import sys
import os
import re
import time
import html
import logging
from datetime import datetime
from pathlib import Path
from typing import Iterable, Optional, Tuple, Dict, List, Set
from urllib.parse import quote_plus, urljoin

import requests
from bs4 import BeautifulSoup
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

try:
    import pyodbc
except Exception:
    pyodbc = None

BASE = "https://inara.cz"
SEARCH_URL = f"{BASE}/elite/minorfaction/?search={{q}}"

HEADERS = {
    # A realistic UA helps avoid being flagged as a bot.
    "User-Agent": "SetFactionNativeSystem_Inara/1.0 (+FalconsFactionMonitor | polite fetcher)",
    "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
    "Accept-Language": "en-US,en;q=0.9",
    "Connection": "keep-alive",
}

# ---- Sessions (with mild retries) ---------------------------------------------
search_session = requests.Session()
search_session.headers.update(HEADERS)
search_retry = Retry(
    total=3, connect=2, read=2,
    backoff_factor=1.2,
    status_forcelist=(429, 500, 502, 503, 504),
    allowed_methods=("GET",),
    raise_on_status=False,
)
search_session.mount("https://", HTTPAdapter(max_retries=search_retry))
search_session.mount("http://", HTTPAdapter(max_retries=search_retry))

details_session = requests.Session()
details_session.headers.update(HEADERS)
# No internal read retries on details to keep control with our outer loop
details_session.mount("https://", HTTPAdapter(max_retries=Retry(total=0)))
details_session.mount("http://", HTTPAdapter(max_retries=Retry(total=0)))

# ---- Helpers -------------------------------------------------------------------
def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", s or "").strip().lower()

def escape_sql_literal(value: str) -> str:
    return value.replace("'", "''")

def read_faction_names(path: Path) -> List[str]:
    return [ln.strip() for ln in path.read_text(encoding="utf-8").splitlines() if ln.strip()]

# ---- INARA scraping ------------------------------------------------------------
def search_inara_faction_url(name: str, timeout: float = 25.0) -> Optional[str]:
    """
    Returns absolute URL to the faction page ( /elite/minorfaction/<id>/ ) or None.
    """
    q = quote_plus(name)
    url = SEARCH_URL.format(q=q)
    r = search_session.get(url, timeout=timeout)
    if r.status_code != 200:
        return None

    soup = BeautifulSoup(r.text, "html.parser")
    # Results table typically contains anchors to individual faction pages.
    candidates: List[Tuple[str, str]] = []
    for a in soup.select("a[href]"):
        href = a.get("href", "")
        # Match minor faction detail links (e.g., /elite/minorfaction/12345/)
        if re.search(r"/elite/minorfaction/\d+/?", href):
            full = href if href.startswith("http") else urljoin(BASE, href)
            txt = a.get_text(strip=True) or ""
            candidates.append((txt, full))

    if not candidates:
        return None

    target = _norm(name)
    for txt, full in candidates:
        if _norm(txt) == target:
            return full
    # Fallback: first candidate
    return candidates[0][1]

def fetch_details_html(details_url: str, timeout: float = 60.0) -> Optional[str]:
    r = details_session.get(details_url, timeout=timeout)
    if r.status_code == 404:
        return None
    r.raise_for_status()
    return r.text

def parse_origin_and_player(html_text: str) -> Tuple[Optional[str], Optional[bool]]:
    """
    Returns (origin_system, is_player) where:
      - origin_system = value of 'Origin' field on the faction page
      - is_player     = True/False if 'Player minor faction: Yes/No' is present; else None
    """
    soup = BeautifulSoup(html_text, "html.parser")

    def value_after_label(label: str) -> Optional[str]:
        # Look for a row/line containing the label and return nearby anchor/text.
        # INARA often presents info in a dl/table-like structure.
        label_l = label.lower()
        # Strategy A: exact strong/b/td/th match
        for el in soup.select("b, strong, th, td, div, span"):
            txt = el.get_text(strip=True)
            if txt and txt.lower().rstrip(":") == label_l:
                # Prefer next link text, then next sibling text
                a = el.find_next("a")
                if a and a.get_text(strip=True):
                    return html.unescape(a.get_text(strip=True))
                sib = el.find_next(string=True)
                if sib and str(sib).strip():
                    return html.unescape(str(sib).strip())

        # Strategy B: any text node containing label
        hit = soup.find(string=lambda s: isinstance(s, str) and label in s)
        if hit:
            par = getattr(hit, "parent", soup)
            a = par.find_next("a")
            if a and a.get_text(strip=True):
                return html.unescape(a.get_text(strip=True))
        return None

    origin = value_after_label("Origin")
    # Some pages might use "Home system" wording; try that as a fallback
    if not origin:
        origin = value_after_label("Home system")

    is_player: Optional[bool] = None
    pf = value_after_label("Player minor faction")
    if pf:
        low = pf.strip().lower()
        if low.startswith("yes"):
            is_player = True
        elif low.startswith("no"):
            is_player = False

    return origin, is_player

def fetch_origin_and_player(
    faction_name: str,
    search_timeout: float = 25.0,
    details_timeout: float = 60.0,
    max_retries: int = 3,
    hard_deadline_secs: float = 120.0,
) -> Tuple[Optional[str], Optional[bool], Optional[str]]:
    """
    Returns (origin_system, is_player, miss_reason).
    Enforces a hard per-faction deadline and outer retries for details page.
    """
    start = time.monotonic()

    details_url = search_inara_faction_url(faction_name, timeout=search_timeout)
    if not details_url:
        return None, None, "not found (search)"

    last_exc = None
    for attempt in range(1, max_retries + 1):
        if time.monotonic() - start > hard_deadline_secs:
            return None, None, "timeout"

        try:
            html_text = fetch_details_html(details_url, timeout=details_timeout)
            if not html_text:
                return None, None, "details 404"
            origin, is_player = parse_origin_and_player(html_text)
            if origin:
                return origin, is_player, None
            return None, None, "no 'Origin' field"
        except requests.exceptions.ReadTimeout:
            last_exc = "read-timeout"
        except Exception as e:
            last_exc = str(e)

        time.sleep(1.1 * attempt)

    reason = "timeout" if last_exc == "read-timeout" else f"error: {last_exc}"
    return None, None, reason

# ---- DB helpers ----------------------------------------------------------------
def connect_db(connection_string: str):
    if pyodbc is None:
        raise RuntimeError("pyodbc is not installed. Please `pip install pyodbc`.")
    return pyodbc.connect(connection_string)

def load_system_map(cnx, table: str = "ref.System", id_col: str = "SystemID", name_col: str = "SystemName") -> Dict[str, int]:
    cur = cnx.cursor()
    cur.execute(f"SELECT {id_col}, {name_col} FROM {table};")
    mapping: Dict[str, int] = {}
    for sid, sname in cur.fetchall():
        if sname is None:
            continue
        mapping[_norm(str(sname))] = int(sid)
    cur.close()
    return mapping

def resolve_system_id(system_map: Dict[str, int], system_name: Optional[str]) -> Optional[int]:
    if not system_name:
        return None
    return system_map.get(_norm(system_name))

# ---- SQL emit ------------------------------------------------------------------
HEADER_LINE = "-- Generated by SetFactionNativeSystem_Inara.py (Inara-based, player-aware if available)"

def append_lines(out_path: Path, lines: Iterable[str]) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "a", encoding="utf-8", newline="\n") as f:
        for line in lines:
            f.write(line + "\n")
        f.flush()
        os.fsync(f.fileno())

def ensure_header(out_path: Path) -> None:
    if not out_path.exists() or out_path.stat().st_size == 0:
        append_lines(out_path, [
            HEADER_LINE,
            f"-- Started: {datetime.now().isoformat(timespec='seconds')}",
            "BEGIN TRAN;",
            "",
            "-- Incremental output; safe to resume. Origin field on INARA is treated as Native System.",
            "",
        ])

def has_commit(out_path: Path) -> bool:
    if not out_path.exists():
        return False
    try:
        return "COMMIT;" in out_path.read_text(encoding="utf-8", errors="ignore")
    except Exception:
        return False

def load_processed_names(out_path: Path, names: List[str], treat_miss_as_processed: bool = True) -> Set[str]:
    done: Set[str] = set()
    if not out_path.exists():
        return done
    txt = out_path.read_text(encoding="utf-8", errors="ignore")
    for n in names:
        where_sig = f"WHERE f.FactionName = '{escape_sql_literal(n)}';"
        if where_sig in txt:
            done.add(n)
        elif treat_miss_as_processed and (f"-- MISS: {n}" in txt or f"-- RETRY MISS: {n}" in txt):
            done.add(n)
    return done

def load_miss_names(out_path: Path, names: List[str]) -> Set[str]:
    misses: Set[str] = set()
    if not out_path.exists():
        return misses
    name_set = set(names)
    for line in out_path.read_text(encoding="utf-8", errors="ignore").splitlines():
        line = line.strip()
        if line.startswith("-- MISS: ") or line.startswith("-- RETRY MISS: "):
            raw = line.split(": ", 1)[1]
            name_only = raw
            idx = raw.rfind(" (")
            if idx != -1 and raw.endswith(")"):
                name_only = raw[:idx]
            candidate = name_only.strip()
            if candidate in name_set:
                misses.add(candidate)
    return misses

def make_update_sql_literal_id(faction_name: str, system_id: int, is_player: Optional[bool]) -> str:
    f = escape_sql_literal(faction_name)
    sets = [f"f.NativeSystemID = {system_id}"]
    if is_player is True:
        sets.append("f.IsPlayer = 1")
    return (
        "UPDATE f\n"
        f"SET {', '.join(sets)}\n"
        "FROM ref.Faction AS f\n"
        f"WHERE f.FactionName = '{f}';"
    )

# ---- Main ----------------------------------------------------------------------
def main(argv: List[str]) -> int:
    import argparse

    parser = argparse.ArgumentParser(
        description="Map factions to Native System using INARA (Origin). Writes incremental SQL, resumes safely, supports --retry-misses."
    )
    parser.add_argument("input", help="UTF-8 text file of faction names (one per line).")
    parser.add_argument("-o", "--output", default="update_native_system_ids.sql",
                        help="Output .sql file (appended incrementally).")
    parser.add_argument("--conn", required=True,
                        help="ODBC connection string for SQL Server (pyodbc).")
    parser.add_argument("--system-table", default="ref.System", help="Table with systems.")
    parser.add_argument("--system-id-col", default="SystemID", help="SystemID column name.")
    parser.add_argument("--system-name-col", default="SystemName", help="SystemName column name.")
    parser.add_argument("--sleep", type=float, default=2.0,  # per your request: 2 seconds
                        help="Delay between factions, seconds (default: 2.0).")
    parser.add_argument("--search-timeout", type=float, default=25.0)
    parser.add_argument("--details-timeout", type=float, default=60.0)
    parser.add_argument("--retries", type=int, default=3, help="Max outer retries for details.")
    parser.add_argument("--hard-timeout", type=float, default=120.0, help="Per-faction hard deadline.")
    parser.add_argument("--no-commit", dest="no_commit", action="store_true",
                        help="Do not auto-append COMMIT; even if all names are processed.")
    parser.add_argument("--retry-misses", action="store_true",
                        help="Only retry factions previously marked as MISS in the output SQL.")
    args = parser.parse_args(argv[1:])

    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")

    names = read_faction_names(Path(args.input))
    if not names:
        logging.error("No faction names found in %s", args.input)
        return 2

    # DB: load system map
    try:
        cnx = connect_db(args.conn)
    except Exception as e:
        logging.error("Could not connect to SQL Server with provided --conn: %s", e)
        return 3

    try:
        system_map = load_system_map(
            cnx,
            table=args.system_table,
            id_col=args.system_id_col,
            name_col=args.system_name_col,
        )
    except Exception as e:
        logging.error("Failed to load systems from %s: %s", args.system_table, e)
        try:
            cnx.close()
        except Exception:
            pass
        return 4
    finally:
        try:
            cnx.close()
        except Exception:
            pass

    out_path = Path(args.output)
    ensure_header(out_path)

    if args.retry_misses:
        prior_misses = load_miss_names(out_path, names)
        if not prior_misses:
            logging.info("No prior MISS entries found in %s. Nothing to retry.", out_path)
            return 0
        # Skip any that already got an UPDATE later
        already_updates = load_processed_names(out_path, list(prior_misses), treat_miss_as_processed=False)
        to_process = [n for n in names if n in prior_misses and n not in already_updates]
        total = len(to_process)
        done = 0
        logging.info("Retrying %d previously missed factions via INARA…", total)
    else:
        already = load_processed_names(out_path, names, treat_miss_as_processed=True)
        to_process = [n for n in names if n not in already]
        total = len(names)
        done = len(already)
        logging.info("Looking up Origin on INARA for %d factions…", total)
        if already:
            logging.info("Resuming: %d/%d already present in output.", done, total)

    updates_this_run = 0
    misses_this_run = 0

    for name in to_process:
        logging.info("… %s → (searching %d/%d)", name, (done + 1 if not args.retry_misses else done + 1), (total if not args.retry_misses else total))

        origin, is_player, miss_reason = fetch_origin_and_player(
            name,
            search_timeout=args.search_timeout,
            details_timeout=args.details_timeout,
            max_retries=args.retries,
            hard_deadline_secs=args.hard_timeout,
        )

        if origin:
            sys_id = resolve_system_id(system_map, origin)
            if sys_id is not None:
                tag = " (player)" if is_player else ""
                logging.info("✔ %s → %s [SystemID=%d]%s", name, origin, sys_id, tag)
                header = f"-- {'RETRY UPDATE' if args.retry_misses else 'UPDATE'} for faction: {name} (Origin='{origin}', SystemID={sys_id})"
                append_lines(out_path, [header, make_update_sql_literal_id(name, sys_id, is_player), ""])
                updates_this_run += 1
            else:
                reason = f"origin '{origin}' not in DB"
                logging.info("✖ %s → (not found) (%s)", name, reason)
                append_lines(out_path, [f"-- {'RETRY MISS' if args.retry_misses else 'MISS'}: {name} ({reason})", ""])
                misses_this_run += 1
        else:
            reason = f"{miss_reason}" if miss_reason else "unknown"
            logging.info("✖ %s → (not found) (%s)", name, reason)
            append_lines(out_path, [f"-- {'RETRY MISS' if args.retry_misses else 'MISS'}: {name} ({reason})", ""])
            misses_this_run += 1

        done += 1
        # Per your requirement: 2 seconds between each faction (tunable via --sleep)
        time.sleep(args.sleep)

    # Finalize only if full set processed (normal mode) and not already committed
    if not args.retry_misses:
        if done == total and not has_commit(out_path) and not args.no_commit:
            append_lines(out_path, ["-- Completed: " + datetime.now().isoformat(timespec="seconds"), "COMMIT;", ""])

    logging.info("Run complete. Appended %d updates, %d misses.", updates_this_run, misses_this_run)
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
