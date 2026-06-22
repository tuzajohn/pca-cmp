/**
 * Fetch-based table loader with Bootstrap 5 pagination, sortable headers, and built-in filter bar.
 *
 * Usage:
 *   const tbl = PagedTable.init({
 *     tableId:     'my-container',   // id of a <div> — table is injected inside
 *     url:         '/Controller/Data',
 *     pageSize:    20,
 *     defaultSort: { col: 'name', dir: 'asc' },
 *     tableClass:  'table mb-0',     // optional
 *     tableStyle:  'font-size:13px;',// optional
 *     filterBar: [
 *       { key: 'status', label: 'Status', type: 'select',
 *         options: ['Draft','Submitted','Approved'] },
 *       { key: 'search', label: 'Search', type: 'text', placeholder: 'Name…' },
 *       { key: 'from',   label: 'From',   type: 'date' },
 *       { key: 'allTime',label: 'All Time', type: 'checkbox' },
 *     ],
 *     // filters() still works alongside or instead of filterBar
 *     filters: () => ({ extra: 'value' }),
 *     columns: [
 *       { key: 'name', label: 'Name', render: row => `...` },
 *       { key: null,   label: '',     sortable: false, render: row => `...` }
 *     ]
 *   });
 *   tbl.reload();   // re-fetch from page 1
 *   tbl.setPage(3); // jump to page 3
 */
const PagedTable = (() => {
    function escHtml(s) {
        return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
    }

    function init(config) {
        const {
            tableId,
            url,
            pageSize: initPageSize = 20,
            defaultSort = null,
            filters: extraFilters = () => ({}),
            filterBar: filterBarDef = null,
            columns,
            tableClass = 'table mb-0',
            tableStyle = 'font-size:13px;'
        } = config;

        const state = {
            page: 1,
            pageSize: initPageSize,
            sortCol: defaultSort?.col ?? null,
            sortDir: defaultSort?.dir ?? 'asc',
            totalPages: 1,
            totalCount: 0
        };

        const container = document.getElementById(tableId);
        if (!container) return { reload() {}, setPage() {} };

        container.innerHTML = '';

        // ── Filter bar ────────────────────────────────────────────────────────
        const filterEls = {};   // key → input/select element

        if (filterBarDef && filterBarDef.length) {
            const bar = document.createElement('div');
            bar.className = 'px-3 py-2 d-flex flex-wrap gap-2 align-items-end';
            bar.style.cssText = 'background:var(--bs-tertiary-bg);border-bottom:1px solid var(--bs-border-color);';

            filterBarDef.forEach(f => {
                const wrap = document.createElement('div');
                wrap.style.cssText = 'display:flex;flex-direction:column;gap:3px;';

                const lbl = document.createElement('label');
                lbl.textContent = f.label;
                lbl.style.cssText = 'font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:.04em;color:var(--bs-secondary-color);white-space:nowrap;';

                let el;
                if (f.type === 'select') {
                    el = document.createElement('select');
                    el.className = 'form-select form-select-sm';
                    el.style.minWidth = '120px';
                    const blank = document.createElement('option');
                    blank.value = ''; blank.textContent = 'All';
                    el.appendChild(blank);
                    (f.options || []).forEach(opt => {
                        const o = document.createElement('option');
                        const val  = typeof opt === 'object' ? opt.value : opt;
                        const text = typeof opt === 'object' ? opt.label : opt;
                        o.value = val; o.textContent = text;
                        el.appendChild(o);
                    });
                } else if (f.type === 'checkbox') {
                    const row = document.createElement('div');
                    row.className = 'd-flex align-items-center gap-1';
                    row.style.height = '31px';
                    el = document.createElement('input');
                    el.type = 'checkbox';
                    el.className = 'form-check-input';
                    el.style.marginTop = '0';
                    row.appendChild(el);
                    wrap.appendChild(lbl);
                    wrap.appendChild(row);
                    filterEls[f.key] = el;
                    bar.appendChild(wrap);
                    return;
                } else if (f.type === 'date') {
                    el = document.createElement('input');
                    el.type = 'date';
                    el.className = 'form-control form-control-sm';
                    el.style.minWidth = '130px';
                } else {
                    el = document.createElement('input');
                    el.type = 'text';
                    el.className = 'form-control form-control-sm';
                    el.style.minWidth = '160px';
                    if (f.placeholder) el.placeholder = f.placeholder;
                    el.addEventListener('keydown', e => { if (e.key === 'Enter') { state.page = 1; load(); } });
                }

                wrap.appendChild(lbl);
                wrap.appendChild(el);
                filterEls[f.key] = el;
                bar.appendChild(wrap);
            });

            // Filter + Reset buttons
            const btnWrap = document.createElement('div');
            btnWrap.className = 'd-flex gap-1 align-items-end';
            btnWrap.style.paddingBottom = '1px';

            const btnFilter = document.createElement('button');
            btnFilter.className = 'btn btn-primary btn-sm';
            btnFilter.textContent = 'Filter';
            btnFilter.style.cssText = 'background:var(--color-primary);border-color:var(--color-primary);';
            btnFilter.addEventListener('click', () => { state.page = 1; load(); });

            const btnReset = document.createElement('button');
            btnReset.className = 'btn btn-outline-secondary btn-sm';
            btnReset.textContent = 'Reset';
            btnReset.addEventListener('click', () => {
                Object.values(filterEls).forEach(el => {
                    if (el.type === 'checkbox') el.checked = false;
                    else el.value = '';
                });
                state.page = 1; load();
            });

            btnWrap.appendChild(btnFilter);
            btnWrap.appendChild(btnReset);
            bar.appendChild(btnWrap);
            container.appendChild(bar);
        }

        // ── Table ─────────────────────────────────────────────────────────────
        const table = document.createElement('table');
        table.className = tableClass;
        table.style.cssText = tableStyle;

        const thead = document.createElement('thead');
        thead.style.cssText = 'background:var(--bs-tertiary-bg);';
        const tbody = document.createElement('tbody');
        table.appendChild(thead);
        table.appendChild(tbody);
        container.appendChild(table);

        const paginationEl = document.createElement('div');
        paginationEl.className = 'px-3 py-2';
        container.appendChild(paginationEl);

        // ── Header row ────────────────────────────────────────────────────────
        const headerRow = document.createElement('tr');
        thead.appendChild(headerRow);

        columns.forEach(col => {
            const th = document.createElement('th');
            if (col.thClass) th.className = col.thClass;
            th.dataset.key = col.key || '';
            th.innerHTML = escHtml(col.label);
            if (col.sortable !== false && col.key) {
                th.style.cursor = 'pointer';
                th.style.userSelect = 'none';
                th.addEventListener('click', () => {
                    if (state.sortCol === col.key) {
                        state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc';
                    } else {
                        state.sortCol = col.key;
                        state.sortDir = 'asc';
                    }
                    state.page = 1;
                    load();
                });
            }
            headerRow.appendChild(th);
        });

        function updateSortIcons() {
            headerRow.querySelectorAll('th').forEach(th => {
                const key = th.dataset.key;
                const col = columns.find(c => c.key === key);
                if (!col || col.sortable === false || !key) return;
                const base = escHtml(col.label);
                if (state.sortCol === key) {
                    const icon = state.sortDir === 'asc'
                        ? '<i class="bi bi-sort-up ms-1" style="opacity:.7;"></i>'
                        : '<i class="bi bi-sort-down ms-1" style="opacity:.7;"></i>';
                    th.innerHTML = base + icon;
                } else {
                    th.innerHTML = base + '<i class="bi bi-arrow-down-up ms-1" style="opacity:.2;font-size:.85em;"></i>';
                }
            });
        }

        // ── Load ──────────────────────────────────────────────────────────────
        async function load() {
            const colCount = columns.length;
            tbody.innerHTML = `<tr><td colspan="${colCount}" class="text-center py-3 text-muted"><span class="spinner-border spinner-border-sm me-2" role="status"></span>Loading…</td></tr>`;

            const raw = { page: state.page, pageSize: state.pageSize };
            if (state.sortCol) { raw.sortCol = state.sortCol; raw.sortDir = state.sortDir; }

            // Collect filterBar values
            Object.entries(filterEls).forEach(([key, el]) => {
                const val = el.type === 'checkbox' ? (el.checked ? 'true' : '') : el.value;
                if (val !== '') raw[key] = val;
            });

            // Merge extra filters
            Object.assign(raw, extraFilters());

            const params = new URLSearchParams();
            Object.entries(raw).forEach(([k, v]) => {
                if (v !== '' && v !== null && v !== undefined) params.set(k, v);
            });

            let result;
            try {
                const resp = await fetch(`${url}?${params}`);
                if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
                result = await resp.json();
            } catch {
                tbody.innerHTML = `<tr><td colspan="${colCount}" class="text-center py-3 text-danger">Failed to load data.</td></tr>`;
                return;
            }

            state.totalPages = result.totalPages || 1;
            state.totalCount = result.totalCount || 0;
            state.page = result.currentPage || state.page;

            tbody.innerHTML = '';
            const items = result.items || [];
            if (items.length === 0) {
                tbody.innerHTML = `<tr><td colspan="${colCount}" class="text-center py-4 text-muted">No records found.</td></tr>`;
                paginationEl.innerHTML = '';
            } else {
                items.forEach(row => {
                    const tr = document.createElement('tr');
                    columns.forEach(col => {
                        const td = document.createElement('td');
                        if (col.tdClass) td.className = col.tdClass;
                        td.innerHTML = col.render ? col.render(row) : escHtml(String(row[col.key] ?? ''));
                        tr.appendChild(td);
                    });
                    tbody.appendChild(tr);
                });
                updateSortIcons();
                renderPagination();
            }
        }

        // ── Pagination ────────────────────────────────────────────────────────
        function renderPagination() {
            const cur = state.page, tot = state.totalPages, total = state.totalCount;
            const rowFrom = total === 0 ? 0 : (cur - 1) * state.pageSize + 1;
            const rowTo   = Math.min(cur * state.pageSize, total);

            let pages = [];
            if (tot <= 7) {
                pages = Array.from({ length: tot }, (_, i) => i + 1);
            } else {
                pages.push(1);
                if (cur > 3) pages.push('…');
                for (let i = Math.max(2, cur - 1); i <= Math.min(tot - 1, cur + 1); i++) pages.push(i);
                if (cur < tot - 2) pages.push('…');
                pages.push(tot);
            }

            const primary = 'var(--color-primary)';
            const linkStyle = `style="color:${primary};"`;
            const activeStyle = `style="background:${primary};border-color:${primary};"`;

            const pageItems = pages.map(p => {
                if (p === '…') return `<li class="page-item disabled"><span class="page-link">…</span></li>`;
                if (p === cur) return `<li class="page-item active"><a class="page-link" href="#" data-p="${p}" ${activeStyle}>${p}</a></li>`;
                return `<li class="page-item"><a class="page-link" href="#" data-p="${p}" ${linkStyle}>${p}</a></li>`;
            });

            const sizeOpts = [10, 20].map(n =>
                `<option value="${n}"${state.pageSize === n ? ' selected' : ''}>${n}</option>`
            ).join('');

            paginationEl.innerHTML = `<div class="d-flex align-items-center justify-content-between flex-wrap gap-2">
                <div class="d-flex align-items-center gap-2" style="font-size:12px;color:var(--bs-secondary-color);">
                    <span>Rows per page</span>
                    <select class="form-select form-select-sm pt-size-select" style="width:65px;">${sizeOpts}</select>
                    <span>${rowFrom}–${rowTo} of ${total} rows</span>
                </div>
                <nav aria-label="Table pagination"><ul class="pagination pagination-sm mb-0 flex-wrap">
                    <li class="page-item ${cur === 1 ? 'disabled' : ''}"><a class="page-link" href="#" data-p="1" ${linkStyle}>«</a></li>
                    <li class="page-item ${cur === 1 ? 'disabled' : ''}"><a class="page-link" href="#" data-p="${cur - 1}" ${linkStyle}>‹</a></li>
                    ${pageItems.join('')}
                    <li class="page-item ${cur === tot ? 'disabled' : ''}"><a class="page-link" href="#" data-p="${cur + 1}" ${linkStyle}>›</a></li>
                    <li class="page-item ${cur === tot ? 'disabled' : ''}"><a class="page-link" href="#" data-p="${tot}" ${linkStyle}>»</a></li>
                </ul></nav>
            </div>`;

            paginationEl.querySelectorAll('[data-p]').forEach(a => {
                a.addEventListener('click', e => {
                    e.preventDefault();
                    const p = parseInt(a.dataset.p);
                    if (p >= 1 && p <= state.totalPages && p !== state.page) {
                        state.page = p;
                        load();
                    }
                });
            });

            paginationEl.querySelector('.pt-size-select').addEventListener('change', function () {
                state.pageSize = parseInt(this.value);
                state.page = 1;
                load();
            });
        }

        load();

        return {
            reload() { state.page = 1; load(); },
            setPage(n) { state.page = n; load(); }
        };
    }

    return { init };
})();
