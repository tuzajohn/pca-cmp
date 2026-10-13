#!/usr/bin/env python3
"""
Writes /root/recovery/finalize_pca_cmp.sql: recreates the lost users, invoice lenders and
HCM reference file records in pca_cmp so the recovered rows link up again.

EDIT THE USERS BLOCK BELOW before running. Run this BEFORE starting the dashboard on pca_cmp,
so the app's seeder does not create conflicting accounts.
"""
import base64
import hashlib
import os
import secrets
import string
import subprocess
import uuid

DB = 'pca_cmp'
OUT = '/root/recovery/finalize_pca_cmp.sql'
OWNER = '73c7091d-77e1-439f-b569-7ee71c85b8ea'

# ---- EDIT ME --------------------------------------------------------------------------
# GUID: (full name, email, department). Leave email as None for a locked placeholder account
# that only keeps history linked; rename it later in the dashboard's admin screen.
USERS = {
    '73c7091d-77e1-439f-b569-7ee71c85b8ea': ('John Tuza', 'john@pcuganda.com', 'IT'),
    '33fee22c-99ec-4ef6-8b29-80196c925286': ('Recovered User 33fee22c', None, 'Unknown'),
    'a37355fc-07de-4919-9bfe-6030ba8bb5aa': ('Recovered User a37355fc', None, 'Unknown'),
    '2710e930-5c93-4a19-ab0d-8ef5298d1ebb': ('Recovered User 2710e930', None, 'Unknown'),
    'eb5e37e2-c181-4ec4-bce8-f5b57bb4e01c': ('Recovered User eb5e37e2', None, 'Unknown'),
}
# ---------------------------------------------------------------------------------------

# Lender id (from recovered InvoiceSchedules) -> name, type, IPPS deduction code (from B's companies)
LENDERS = {
    1:  ('ABSA Bank Uganda Limited', 'BANK', '278'),
    4:  ('Centenary Rural Development Bank Limited', 'BANK', '307'),
    8:  ('Equity Bank', 'BANK', '340'),
    15: ('KCB Bank Uganda Limited', 'BANK', '311'),
    16: ('PEARL BANK UGANDA LIMITED', 'BANK', '271'),
    17: ('Stanbic Bank Uganda Limted', 'BANK', '265'),
}

# HCM reference files found on disk: (schedule id, month, file name, uploaded at)
HCM_FILES = [
    (2, '2026-06', 'hcm_ref_2_2026-06.xlsx', '2026-06-26 15:38:18'),
    (6, '2026-07', 'hcm_ref_6_2026-07.xlsx', '2026-07-31 09:19:00'),
]


def q(v):
    if v is None:
        return 'NULL'
    if isinstance(v, (int, float)):
        return str(v)
    return "'" + str(v).replace('\\', '\\\\').replace("'", "\\'") + "'"


def identity_v3_hash(password):
    """ASP.NET Core Identity V3 format: PBKDF2 HMAC-SHA512, 100000 iterations, 16-byte salt."""
    salt = secrets.token_bytes(16)
    subkey = hashlib.pbkdf2_hmac('sha512', password.encode(), salt, 100000, 32)
    header = b'\x01' + (2).to_bytes(4, 'big') + (100000).to_bytes(4, 'big') + (16).to_bytes(4, 'big')
    return base64.b64encode(header + salt + subkey).decode()


def hcm_prefix():
    """Derive the stored path style from a recovered InvoiceRuns.FilePath."""
    try:
        r = subprocess.run(['mysql', '-N', '-B', DB, '-e',
                            "SELECT FilePath FROM InvoiceRuns WHERE FilePath LIKE '%invoices/%' LIMIT 1"],
                           capture_output=True, text=True, timeout=20)
        fp = r.stdout.strip()
        if fp:
            return fp.split('invoices/')[0] + 'invoices/hcm-ref/'
    except Exception:
        pass
    return 'uploads/documents/invoices/hcm-ref/'


def main():
    alphabet = string.ascii_letters + string.digits
    temp_pw = 'Pca!' + ''.join(secrets.choice(alphabet) for _ in range(12))
    sql = ['SET FOREIGN_KEY_CHECKS=0;']

    for uid, (name, email, dept) in USERS.items():
        placeholder = email is None
        login = email or f'recovered-{uid[:8]}@pca.local'
        sql.append(
            'INSERT IGNORE INTO `AspNetUsers` (`Id`,`FullName`,`Department`,`UserName`,`NormalizedUserName`,'
            '`Email`,`NormalizedEmail`,`EmailConfirmed`,`PasswordHash`,`SecurityStamp`,`ConcurrencyStamp`,'
            '`PhoneNumber`,`PhoneNumberConfirmed`,`TwoFactorEnabled`,`LockoutEnd`,`LockoutEnabled`,`AccessFailedCount`) '
            f'VALUES ({q(uid)},{q(name)},{q(dept)},{q(login)},{q(login.upper())},{q(login)},{q(login.upper())},1,'
            f'{q(None if placeholder else identity_v3_hash(temp_pw))},{q(str(uuid.uuid4()).upper())},'
            f'{q(str(uuid.uuid4()))},NULL,0,0,{q("9999-12-31 23:59:59" if placeholder else None)},1,0);')

    for lid, (name, ctype, code) in LENDERS.items():
        sql.append('INSERT IGNORE INTO `InvoiceLenders` (`Id`,`Name`,`CompanyType`,`DeductionCode`,`IsActive`,'
                   f'`CreatedAt`,`CreatedById`) VALUES ({lid},{q(name)},{q(ctype)},{q(code)},1,NOW(6),{q(OWNER)});')

    prefix = hcm_prefix()
    for sched, month, fname, at in HCM_FILES:
        sql.append('INSERT IGNORE INTO `InvoiceHcmRefFiles` (`ScheduleId`,`MonthYear`,`FilePath`,`OriginalFileName`,'
                   f'`UploadedAt`,`UploadedById`) VALUES ({sched},{q(month)},{q(prefix + fname)},{q(fname)},'
                   f'{q(at)},{q(OWNER)});')

    sql.append('SET FOREIGN_KEY_CHECKS=1;')
    with open(OUT, 'w') as f:
        f.write('\n'.join(sql) + '\n')

    print(f'Wrote {OUT}')
    print(f'HCM file path style: {prefix}')
    print('\nAccounts with a login (temporary password, change it at first sign-in):')
    for uid, (name, email, _) in USERS.items():
        if email:
            print(f'  {email:30s} {name}')
    print(f'\nTemporary password: {temp_pw}')
    print('(shown once, not stored anywhere)')


if __name__ == '__main__':
    main()
