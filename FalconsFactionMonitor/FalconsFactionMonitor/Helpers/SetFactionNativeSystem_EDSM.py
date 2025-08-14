#!/usr/bin/env python3
"""
SetFactionNativeSystem.py — EDSM mapper with DB-aware SystemID resolution.

What it does
------------
- Connects to SQL Server (pyodbc) and loads ref.System(SystemID, SystemName)
- Searches EDSM for each faction (ID-based page), parses Home system + Player flag
- APPENDS per-faction SQL immediately (resume-friendly)
- UPDATE uses a literal SystemID from your DB (no JOIN)
- '--retry-misses' mode reprocesses previous MISS entries only
- Hard per-faction timeout; no page can hang the run

Example usage
-------------
python SetFactionNativeSystem.py factions.txt ^
  --conn "Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=EliteDB;Trusted_Connection=yes;Encrypt=yes;TrustServerCertificate=yes" ^
  -o update_native_system_ids.sql

# SQL auth example:
# --conn "Driver={ODBC Driver 18 for SQL Server};Server=localhost;Database=EliteDB;Uid=sa;Pwd=YourStrong!Passw0rd;Encrypt=yes;TrustServerCertificate=yes"
"""

import sys
import os
import time
import html
import logging
from pathlib import Path
from typing import Iterable, Tuple, Optional, Set, List, Dict
from urllib.parse import quote_plus, urljoin
import re
from datetime import datetime

import requests
from bs4 import BeautifulSoup
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

try:
    import pyodbc  # SQL Server driver
except Exception as e:
    pyodbc = None

BASE = "https://www.edsm.net"
HEADERS = {"User-Agent": "SetFactionNativeSystem/2.0 (EDSM home system mapper)"}

# --- Sessions -------------------------------------------------------------------
# Search: light page; mild retries are fine
search_session = requests.Session()
search_session.headers.update(HEADERS)
search_retry = Retry(
    total=4, connect=2, read=2,
    backoff_factor=1.2,
    status_forcelist=(429, 500, 502, 503, 504),
    allowed_methods=("GET",),
    raise_on_status=False,
)
search_session.mount("https://", HTTPAdapter(max_retries=search_retry))
search_session.mount("http://", HTTPAdapter(max_retries=search_retry))

# Details: heavy page; *no read retries* so we control timing with our own loop
details_session = requests.Session()
details_session.headers.update(HEADERS)
no_retry = Retry(total=0, connect=0, read=0, allowed_methods=("GET",), raise_on_status=False)
details_session.mount("https://", HTTPAdapter(max_retries=no_retry))
details_session.mount("http://", HTTPAdapter(max_retries=no_retry))

# --- Helpers --------------------------------------------------------------------

def escape_sql_literal(value: str) -> str:
    return value.replace("'", "''")

def read_faction_names(path: Path) -> List[str]:
    return [ln.strip() for ln in path.read_text(encoding="utf-8").splitlines() if ln.strip()]

def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip().lower()

def search_faction(faction_name: str, timeout: float = 25.0) -> Optional[str]:
    """
    Use EDSM's name-index search to find the canonical ID-based page:
    /en/faction/id/<id>/name/<slug>
    Returns absolute URL or None.
    """
    q = quote_plus(faction_name)
    url = f"{BASE}/en/search/factions/index/name/{q}"
    r = search_session.get(url, timeout=timeout)
    if r.status_code != 200:
        return None

    soup = BeautifulSoup(r.text, "html.parser")
    candidates: list[tuple[str, str]] = []

    for a in soup.select("a[href]"):
        href = a["href"]
        full = href if href.startswith("http") else urljoin(BASE, href)
        if re.search(r"/en/faction/id/\d+/name/", full):
            candidates.append((a.get_text(strip=True) or "", full))

    if not candidates:
        return None

    target = _norm(faction_name)
    for text, full in candidates:
        if _norm(text) == target:
            return full
    return candidates[0][1]

def parse_home_system_and_player(html_text: str) -> tuple[Optional[str], Optional[bool]]:
    soup = BeautifulSoup(html_text, "html.parser")

    def value_after_label(label: str) -> Optional[str]:
        for b in soup.select("b, strong"):
            if b.get_text(strip=True).lower() == f"{label.lower()}:".lower():
                a = b.find_next("a")
                if a and a.get_text(strip=True):
                    return html.unescape(a.get_text(strip=True))
                nxt = b.next_sibling
                if nxt and isinstance(nxt, str) and nxt.strip():
                    return html.unescape(nxt.strip())
        hit = soup.find(string=lambda s: isinstance(s, str) and label in s)
        if hit:
            par = getattr(hit, "parent", soup)
            a = par.find_next("a")
            if a and a.get_text(strip=True):
                return html.unescape(a.get_text(strip=True))
        return None

    home = value_after_label("Home system")
    pf_text = value_after_label("Player faction")
    is_player: Optional[bool] = None
    if pf_text:
        low = pf_text.strip().lower()
        if low.startswith("yes"):
            is_player = True
        elif low.startswith("no"):
            is_player = False

    return home, is_player

def fetch_details_html(details_url: str, timeout: float = 75.0) -> Optional[str]:
    r = details_session.get(details_url, timeout=timeout)
    if r.status_code == 404:
        return None
    r.raise_for_status()
    return r.text

def fetch_home_system_and_player(
    faction_name: str,
    search_timeout: float = 25.0,
    details_timeout: float = 75.0,
    max_retries: int = 3,
    hard_deadline_secs: float = 150.0,
) -> tuple[Optional[str], Optional[bool], Optional[str]]:
    """
    Resolves the ID-based page via search, then parses Home system and Player flag.
    Returns (home_system, is_player, miss_reason)
      - miss_reason is None if success; otherwise "timeout" or "not found" etc.
    """
    start = time.monotonic()

    details_url = search_faction(faction_name, timeout=search_timeout)
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
            home, is_player = parse_home_system_and_player(html_text)
            if home:
                return home, is_player, None
            return None, None, "no 'Home system' field"
        except requests.exceptions.ReadTimeout:
            last_exc = "read-timeout"
        except Exception as e:
            last_exc = str(e)

        time.sleep(1.2 * attempt)

    reason = "timeout" if last_exc == "read-timeout" else f"error: {last_exc}"
    return None, None, reason

# ------------- Database: load SystemID map --------------------------------------

def connect_db(connection_string: str):
    if pyodbc is None:
        raise RuntimeError("pyodbc is not installed. Please `pip install pyodbc`.")
    return pyodbc.connect(connection_string)

def load_system_map(cnx, table: str = "ref.System", id_col: str = "SystemID", name_col: str = "SystemName") -> Dict[str, int]:
    """
    Load all systems into a dict keyed by lower(SystemName) -> SystemID.
    """
    sql = f"SELECT {id_col}, {name_col} FROM {table};"
    cur = cnx.cursor()
    cur.execute(sql)
    mapping: Dict[str, int] = {}
    for sid, sname in cur.fetchall():
        if sname is None:
            continue
        mapping[str(sname).strip().lower()] = int(sid)
    cur.close()
    return mapping

def resolve_system_id(system_map: Dict[str, int], system_name: Optional[str]) -> Optional[int]:
    if not system_name:
        return None
    return system_map.get(system_name.strip().lower())

# ------------- SQL block creation ------------------------------------------------

def make_update_sql_literal_id(faction_name: str, system_id: int, is_player: Optional[bool]) -> str:
    """
    Build the UPDATE using a literal SystemID (no JOIN). If is_player is True, also set IsPlayer = 1.
    """
    f = escape_sql_literal(faction_name)
    set_bits = [f"f.NativeSystemID = {system_id}"]
    if is_player is True:
        set_bits.append("f.IsPlayer = 1")
    return (
        "UPDATE f\n"
        f"SET {', '.join(set_bits)}\n"
        "FROM ref.Faction AS f\n"
        f"WHERE f.FactionName = '{f}';"
    )

# ------------- Incremental output utils -----------------------------------------

HEADER_LINE = "-- Generated by SetFactionNativeSystem.py (ID-based, player-aware)"

def ensure_header(out_path: Path) -> None:
    if not out_path.exists() or out_path.stat().st_size == 0:
        lines = [
            HEADER_LINE,
            f"-- Started: {datetime.now().isoformat(timespec='seconds')}",
            "BEGIN TRAN;",
            "",
            "-- This file is written incrementally; safe to resume after interruption.",
            "",
        ]
        append_lines(out_path, lines)

def has_commit(out_path: Path) -> bool:
    if not out_path.exists():
        return False
    try:
        txt = out_path.read_text(encoding="utf-8", errors="ignore")
        return "COMMIT;" in txt
    except Exception:
        return False

def append_lines(out_path: Path, lines: Iterable[str]) -> None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "a", encoding="utf-8", newline="\n") as f:
        for line in lines:
            f.write(line + "\n")
        f.flush()
        os.fsync(f.fileno())

def load_processed_names(out_path: Path, all_names: List[str], treat_miss_as_processed: bool = True) -> Set[str]:
    """
    Consider a faction 'processed' if its UPDATE already exists
    (identified by the exact WHERE clause). If treat_miss_as_processed is True,
    also treat any '-- MISS: <name>' or '-- RETRY MISS: <name>' as processed.
    """
    processed: Set[str] = set()
    if not out_path.exists():
        return processed
    txt = out_path.read_text(encoding="utf-8", errors="ignore")
    for name in all_names:
        escaped = escape_sql_literal(name)
        where_sig = f"WHERE f.FactionName = '{escaped}';"
        if where_sig in txt:
            processed.add(name)
            continue
        if treat_miss_as_processed:
            if f"-- MISS: {name}" in txt or f"-- RETRY MISS: {name}" in txt:
                processed.add(name)
    return processed

def load_miss_names(out_path: Path, all_names: List[str]) -> Set[str]:
    """
    Return set of faction names that appear as MISS (or RETRY MISS) in the output SQL.
    Only keep names that are present in all_names (input file).
    """
    misses: Set[str] = set()
    if not out_path.exists():
        return misses

    name_set = set(all_names)
    for line in out_path.read_text(encoding="utf-8", errors="ignore").splitlines():
        line = line.strip()
        if line.startswith("-- MISS: ") or line.startswith("-- RETRY MISS: "):
            raw = line.split(": ", 1)[1]  # "<name>" or "<name> (reason)"
            name_only = raw
            idx = raw.rfind(" (")
            if idx != -1 and raw.endswith(")"):
                name_only = raw[:idx]
            candidate = name_only.strip()
            if candidate in name_set:
                misses.add(candidate)
    return misses

# ------------- Main --------------------------------------------------------------

def main(argv: list[str]) -> int:
    import argparse

    parser = argparse.ArgumentParser(
        description="Generate SQL UPDATEs for ref.Faction.NativeSystemID (and IsPlayer) using EDSM. Resolves SystemID from DB; writes incrementally; resumes safely; supports --retry-misses."
    )
    parser.add_argument("input", help="UTF-8 text file of faction names (one per line).")
    parser.add_argument("-o", "--output", default="update_native_system_ids.sql",
                        help="Output .sql file (appended incrementally).")
    parser.add_argument("--conn", required=True,
                        help="ODBC connection string for SQL Server (pyodbc).")
    parser.add_argument("--system-table", default="ref.System",
                        help="Table holding systems (default: ref.System)")
    parser.add_argument("--system-id-col", default="SystemID",
                        help="System ID column name (default: SystemID)")
    parser.add_argument("--system-name-col", default="SystemName",
                        help="System name column name (default: SystemName)")
    parser.add_argument("--sleep", type=float, default=0.4,
                        help="Delay between lookups in seconds.")
    parser.add_argument("--search-timeout", type=float, default=25.0,
                        help="Seconds allowed for the search page.")
    parser.add_argument("--details-timeout", type=float, default=75.0,
                        help="Seconds allowed for the details page.")
    parser.add_argument("--retries", type=int, default=3,
                        help="Max outer retries for a details page.")
    parser.add_argument("--hard-timeout", type=float, default=150.0,
                        help="Maximum seconds to spend on a single faction before marking as MISS (timeout).")
    parser.add_argument("--no-commit", dest="no_commit", action="store_true",
                        help="Do not auto-append COMMIT; even if all names are processed.")
    parser.add_argument("--retry-misses", action="store_true",
                        help="Process only factions previously marked as MISS in the output SQL.")
    args = parser.parse_args(argv[1:])

    logging.basicConfig(level=logging.INFO, format="%(levelname)s: %(message)s")

    names = read_faction_names(Path(args.input))
    if not names:
        logging.error("No faction names found in %s", args.input)
        return 2

    # --- DB connect + load System map
    try:
        cnx = connect_db(args.conn)
    except Exception as e:
        logging.error("Could not connect to SQL Server with provided --conn: %s", e)
        return 3

    try:
        system_map = load_system_map(cnx, table=args.system_table, id_col=args.system_id_col, name_col=args.system_name_col)
    except Exception as e:
        logging.error("Failed to load system map from %s: %s", args.system_table, e)
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
        already_updates = load_processed_names(out_path, list(prior_misses), treat_miss_as_processed=False)
        to_process = [n for n in names if n in prior_misses and n not in already_updates]
        total = len(to_process)
        done = 0
        logging.info("Retrying %d previously missed factions…", total)
    else:
        already = load_processed_names(out_path, names, treat_miss_as_processed=True)
        to_process = [n for n in names if n not in already]
        total = len(names)
        done = len(already)
        logging.info("Looking up home systems on EDSM for %d factions…", total)
        if already:
            logging.info("Resuming: %d/%d already present in output.", done, total)

    updates_this_run = 0
    misses_this_run = 0

    for name in to_process:
        logging.info("… %s → (searching %d/%d)", name, (done + 1 if not args.retry_misses else done + 1), (total if not args.retry_misses else total))

        home, is_player, miss_reason = fetch_home_system_and_player(
            name,
            search_timeout=args.search_timeout,
            details_timeout=args.details_timeout,
            max_retries=args.retries,
            hard_deadline_secs=args.hard_timeout,
        )

        if home:
            sys_id = resolve_system_id(system_map, home)
            if sys_id is not None:
                tag = " (player)" if is_player else ""
                logging.info("✔ %s → %s [SystemID=%d]%s", name, home, sys_id, tag)
                header = f"-- {'RETRY UPDATE' if args.retry_misses else 'UPDATE'} for faction: {name} (System='{home}', SystemID={sys_id})"
                block = [header, make_update_sql_literal_id(name, sys_id, is_player), ""]
                append_lines(out_path, block)
                updates_this_run += 1
            else:
                # SystemName from EDSM not present in DB
                reason = f"system '{home}' not in DB"
                logging.info("✖ %s → (not found) (%s)", name, reason)
                miss_header = f"-- {'RETRY MISS' if args.retry_misses else 'MISS'}: {name} ({reason})"
                append_lines(out_path, [miss_header, ""])
                misses_this_run += 1
        else:
            reason = f"{miss_reason}" if miss_reason else "unknown"
            logging.info("✖ %s → (not found) (%s)", name, reason)
            miss_header = f"-- {'RETRY MISS' if args.retry_misses else 'MISS'}: {name} ({reason})"
            append_lines(out_path, [miss_header, ""])
            misses_this_run += 1

        done += 1
        time.sleep(args.sleep)

    if not args.retry_misses:
        if done == total and not has_commit(out_path) and not args.no_commit:
            append_lines(out_path, ["-- Completed: " + datetime.now().isoformat(timespec="seconds"), "COMMIT;", ""])

    logging.info("Run complete. Appended %d updates, %d misses.", updates_this_run, misses_this_run)
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
