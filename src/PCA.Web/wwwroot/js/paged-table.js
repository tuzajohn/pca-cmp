/**
 * Fetch-based table loader with Bootstrap 5 pagination and sortable headers.
 *
 * Usage:
 *   const tbl = PagedTable.init({
 *     tableId: 'my-table',
 *     url: '/Controller/Data',
 *     pageSize: 25,
 *     defaultSort: { col: 'name', dir: 'asc' },
 *     filters: () => ({ status: document.getElementById('f-status').value }),
 *     columns: [
 *       { key: 'name',   label: 'Name',   render: row => `<a href="...">${row.name}</a>` },
 *       { key: 'status', label: 'Status', render: row => `<span class="badge ...">${row.status}</span>` },
 *       { key: null,     label: '',       sortable: false, render: row => `<a href="...">View</a>` }
 *     ],
 *     paginationId: 'my-table-pagination'
 *   });
 *   tbl.reload();   // re-fetch from page 1
 *   tbl.setPage(3); // jump to page 3
 */
const PagedTable = (() => {
    function init(config) {
        const {
            tableId,
            url,
            pageSize = 25,
            defaultSort = null,
            filters = () => ({}),
            columns,
            paginationId
        } = config;

        const state = {
            page: 1,
            pageSize,
            sortCol: defaultSort?.col ?? null,
            sortDir: defaultSort?.dir ?? 'asc',
            totalPages: 1
        };

        const table = document.getElementById(tableId);
        if (!table) return { reload() {}, setPage() {} };

        const tbody = table.querySelector('tbody') || table.appendChild(document.createElement('tbody'));
        let thead = table.querySelector('thead');
        if (!thead) { thead = document.createElement('thead'); table.prepend(thead); }

        // Build header row
        const headerRow = thead.querySelector('tr') || thead.appendChild(document.createElement('tr'));
        headerRow.innerHTML = '';
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
            const f = filters();
            Object.assign(raw, f);

            const params = new URLSearchParams();
            Object.entries(raw).forEach(([k, v]) => {
                if (v !== '' && v !== null && v !== undefined) params.set(k, v);
            });

            let result;
            try {
                const resp = await fetch(`${url}?${params}`);
                if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
                result = await resp.json();
            } catch (err) {
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
            const container = paginationId ? document.getElementById(paginationId) : null;
            if (!container) return;
            if (state.totalPages <= 1) { container.innerHTML = ''; return; }

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

            const items = pages.map(p =>
                p === '…'
                    ? `<li class="page-item disabled"><span class="page-link">…</span></li>`
                    : `<li class="page-item ${p === cur ? 'active' : ''}"><a class="page-link" href="#" data-p="${p}">${p}</a></li>`
            );

            container.innerHTML = `<nav aria-label="Table pagination"><ul class="pagination pagination-sm mb-0 flex-wrap">
                <li class="page-item ${cur === 1 ? 'disabled' : ''}"><a class="page-link" href="#" data-p="${cur - 1}">‹</a></li>
                ${items.join('')}
                <li class="page-item ${cur === tot ? 'disabled' : ''}"><a class="page-link" href="#" data-p="${cur + 1}">›</a></li>
            </ul></nav>`;

            container.querySelectorAll('[data-p]').forEach(a => {
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

        function escHtml(s) {
            return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
        }

        load();

        return {
            reload() { state.page = 1; load(); },
            setPage(n) { state.page = n; load(); }
        };
    }

    return { init };
})();
