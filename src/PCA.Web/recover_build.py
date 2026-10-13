#!/usr/bin/env python3
"""
Builds /root/recovery/pca_cmp_data.sql for the new pca_cmp database from:
  1. recovered InnoDB pages (decoded with c_parser)
  2. binlog row changes for the old pca_admin (Sep 13 to Oct 1 drop)
Writes only a SQL file. Nothing touches MySQL until you load it yourself.
"""
import collections
import os
import re
import subprocess

BASE = '/root/recovery'
SCHEMA = f'{BASE}/prod_schema.sql'
BINLOG = '/root/rescue/old_pca_admin_changes.txt'
CPARSER = f'{BASE}/undrop-for-innodb/c_parser'
OUT = f'{BASE}/pca_cmp_data.sql'

# index id -> table, confirmed by content and scoring
PAGES = {
    742: 'ServerRoomAccessRequests',
    700: 'AppLogs',
    715: 'ApiKeys',
    720: 'ApprovalFlows',
    729: 'ApprovalSteps',
}
# page copy is complete and newer; binlog rows for these would only clobber it
SKIP_BINLOG = {'ServerRoomAccessRequests'}

GUID = re.compile(r'[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}')


def load_columns(path):
    """Physical column order per table: CREATE TABLE columns, then ALTER TABLE ADD columns."""
    src = open(path).read()
    cols = {}
    for m in re.finditer(r'CREATE TABLE (?:IF NOT EXISTS )?`(\w+)` \((.*?)\n\) CHARACTER SET=utf8mb4;', src, re.S):
        cols[m.group(1)] = [re.match(r'\s*`(\w+)`', l).group(1)
                            for l in m.group(2).split('\n') if re.match(r'\s*`\w+`', l)]
    for m in re.finditer(r'ALTER TABLE `(\w+)` ADD `(\w+)`', src):
        cols.setdefault(m.group(1), []).append(m.group(2))
    return cols


def upsert(table, names, values):
    c = ', '.join(f'`{n}`' for n in names)
    u = ', '.join(f'`{n}`=VALUES(`{n}`)' for n in names)
    return f'INSERT INTO `{table}` ({c}) VALUES ({", ".join(values)}) ON DUPLICATE KEY UPDATE {u};'


def page_rows(cols, counts):
    out = []
    for idx, table in PAGES.items():
        page = f'{BASE}/scan/FIL_PAGE_INDEX/{idx:016d}.page'
        definition = f'{BASE}/pdefs/{table}__0.sql'
        r = subprocess.run([CPARSER, '-6f', page, '-t', definition, '-b', f'{BASE}/scan/FIL_PAGE_TYPE_BLOB'],
                           capture_output=True)
        for line in r.stdout.decode('utf-8', 'replace').split('\n'):
            if not line or line.startswith('--') or '\t' not in line:
                continue
            parts = line.split('\t')
            if len(parts) < 3 or parts[2] != table:
                continue
            values = parts[3:]
            if len(values) != len(cols[table]):
                counts[f'skipped page row {table}'] += 1
                continue
            out.append(upsert(table, cols[table], values))
            counts[f'page   {table}'] += 1
    return out


def clean_value(raw):
    v = raw.rsplit(' /* ', 1)[0].strip()      # drop mysqlbinlog type comment
    return re.sub(r' \(\d+\)$', '', v)        # drop unsigned alternative, e.g. -1 (4294967295)


def binlog_rows(cols, counts):
    out = []
    lines = open(BINLOG, encoding='utf-8', errors='replace').read().split('\n')
    head = re.compile(r'^### (INSERT INTO|UPDATE|DELETE FROM) `pca_admin`\.`(\w+)`')
    i = 0
    while i < len(lines):
        m = head.match(lines[i])
        if not m:
            i += 1
            continue
        op, table = m.groups()
        i += 1
        sections, cur = {}, None
        while i < len(lines) and lines[i].startswith('###') and not head.match(lines[i]):
            line = lines[i].strip()
            if line in ('### SET', '### WHERE'):
                cur = line[4:]
                sections[cur] = []
            else:
                mm = re.match(r'^###\s+@(\d+)=(.*)$', line)
                if mm and cur:
                    sections[cur].append(clean_value(mm.group(2)))
            i += 1
        if table not in cols or table in SKIP_BINLOG:
            continue
        if op == 'DELETE FROM':
            where = sections.get('WHERE', [])
            if where and cols[table][0] == 'Id':
                out.append(f'DELETE FROM `{table}` WHERE `Id`={where[0]};')
                counts[f'delete {table}'] += 1
            continue
        values = sections.get('SET', [])
        if not values:
            continue
        out.append(upsert(table, cols[table][:len(values)], values))
        counts[f'{"insert" if op == "INSERT INTO" else "update"} {table}'] += 1
    return out


def main():
    cols = load_columns(SCHEMA)
    counts = collections.Counter()
    stmts = ['SET FOREIGN_KEY_CHECKS=0;', "SET SESSION sql_mode='';"]
    stmts += page_rows(cols, counts)
    if os.path.exists(BINLOG):
        stmts += binlog_rows(cols, counts)
    else:
        print(f'WARNING: {BINLOG} not found, binlog rows skipped')
    stmts.append('SET FOREIGN_KEY_CHECKS=1;')
    with open(OUT, 'w') as f:
        f.write('\n'.join(stmts) + '\n')

    print(f'Wrote {len(stmts) - 3} statements to {OUT}\n')
    for k in sorted(counts):
        print(f'  {counts[k]:4d}  {k}')
    guids = collections.Counter(GUID.findall('\n'.join(stmts)))
    print('\nUser GUIDs referenced (recreate these in AspNetUsers):')
    for g, n in guids.most_common():
        print(f'  {g}  ({n} refs)')


if __name__ == '__main__':
    main()
