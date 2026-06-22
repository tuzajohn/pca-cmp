/**
 * Fetch-based table loader with Bootstrap 5 pagination and sortable headers.
 * The container element (tableId) is a plain <div>; all table DOM is created by this module.
 *
 * Usage:
 *   const tbl = PagedTable.init({
 *     tableId:     'my-container',          // id of a <div> — table is injected inside
 *     url:         '/Controller/Data',
 *     pageSize:    25,
 *     defaultSort: { col: 'name', dir: 'asc' },
 *     filters:     () => ({ status: document.getElementById('f-status').value }),
 *     tableClass:  'table mb-0',            // optional, default 'table mb-0'
 *     tableStyle:  'font-size:13px;',       // optional, default 'font-size:13px;'
 *     columns: [
 *       { key: 'name',   label: 'Name',   render: row => `<a href="...">${row.name}</a>` },
 *       { key: 'status', label: 'Status', sortable: false, render: row => `...` },
 *       { key: null,     label: '',       sortable: false, render: row => `<a href="...">View</a>` }
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
            pageSize = 25,
            defaultSort = null,
            filters = () => ({}),
            columns,
            tableClass = 'table mb-0',
            tableStyle = 'font-size:13px;'
        } = config;

        const state = {
            page: 1,
            pageSize,
            sortCol: defaultSort?.col ?? null,
            sortDir: defaultSort?.dir ?? 'asc',
            totalPages: 1
        };

        const container = document.getElementById(tableId);
        if (!container) return { reload() {}, setPage() {} };

        // Build table structure
        container.innerHTML = '';

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

        // Build header row
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

        async function load() {
            const colCount = columns.length;
            tbody.innerHTML = `<tr><td colspan="${colCount}" class="text-center py-3 text-muted"><span class="spinner-border spinner-border-sm me-2" role="status"></span>Loading…</td></tr>`;

            const raw = { page: state.page, pageSize: state.pageSize };
            if (state.sortCol) { raw.sortCol = state.sortCol; raw.sortDir = state.sortDir; }
            Object.assign(raw, filters());

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
            state.page = result.currentPage || state.page;

            tbody.innerHTML = '';
            const items = result.items || [];
            if (items.length === 0) {
                tbody.innerHTML = `<tr><td colspan="${colCount}" class="text-center py-4 text-muted">No records found.</td></tr>`;
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
            }

            updateSortIcons();
            renderPagination();
        }

        function renderPagination() {
            if (state.totalPages <= 1) { paginationEl.innerHTML = ''; return; }

            const cur = state.page, tot = state.totalPages;
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

            const pageItems = pages.map(p =>
                p === '…'
                    ? `<li class="page-item disabled"><span class="page-link">…</span></li>`
                    : `<li class="page-item ${p === cur ? 'active' : ''}"><a class="page-link" href="#" data-p="${p}">${p}</a></li>`
            );

            paginationEl.innerHTML = `<nav aria-label="Table pagination"><ul class="pagination pagination-sm mb-0 flex-wrap">
                <li class="page-item ${cur === 1 ? 'disabled' : ''}"><a class="page-link" href="#" data-p="${cur - 1}">‹</a></li>
                ${pageItems.join('')}
                <li class="page-item ${cur === tot ? 'disabled' : ''}"><a class="page-link" href="#" data-p="${cur + 1}">›</a></li>
            </ul></nav>`;

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
        }

        load();

        return {
            reload() { state.page = 1; load(); },
            setPage(n) { state.page = n; load(); }
        };
    }

    return { init };
})();
