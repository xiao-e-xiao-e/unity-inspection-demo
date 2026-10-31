import { useEffect, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { defaultBaseUrl, normalizeBaseUrl, priorities, priorityLabels, statuses, statusLabels } from './api';
import type { Issue, Status } from './api';
import { useIssues } from './useIssues';

function Symbol({ kind = 'grid' }: { kind?: 'grid' | 'refresh' | 'search' | 'arrow' | 'settings' }) {
  const paths = { grid: 'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z', refresh: 'M20 7v5h-5 M4 17v-5h5 M5 8a8 8 0 0 1 13-3l2 3 M19 16a8 8 0 0 1-13 3l-2-3', search: 'M21 21l-5-5 M18 10a8 8 0 1 1-16 0 8 8 0 0 1 16 0', arrow: 'M5 12h14 M13 6l6 6-6 6', settings: 'M4 7h16 M4 17h16 M8 4v6 M16 14v6' };
  return <svg width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[kind]} /></svg>;
}
export function formatTime(value: string) {
  const date = new Date(value); return Number.isNaN(date.getTime()) ? '时间未知' : date.toLocaleString('zh-CN', { hour12: false });
}
function Modal({ children, titleId, onClose, drawer = false }: { children: ReactNode; titleId: string; onClose: () => void; drawer?: boolean }) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => { ref.current?.showModal(); }, []);
  return <dialog ref={ref} className={drawer ? 'drawer' : 'modal'} aria-labelledby={titleId} onCancel={onClose} onClick={e => { if (e.target === e.currentTarget) onClose(); }}><div className="dialog-inner">{children}</div></dialog>;
}
function StatusSelect({ issue, disabled, onChange, detail = false }: { issue: Issue; disabled: boolean; onChange: (issue: Issue, status: Status) => void; detail?: boolean }) {
  return <select className={`status-select ${issue.status}`} aria-label={`${detail ? '详情' : '列表'}状态：${issue.title}`} value={issue.status} disabled={disabled} onChange={e => onChange(issue, e.target.value as Status)}>
    {statuses.map(status => <option key={status} value={status}>{statusLabels[status]}</option>)}
  </select>;
}
function getInitialBase() { try { return sessionStorage.getItem('inspection-api-base') || defaultBaseUrl; } catch { return defaultBaseUrl; } }

export default function App() {
  const [base, setBase] = useState(getInitialBase);
  const state = useIssues(base);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [priority, setPriority] = useState('all');
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [draftBase, setDraftBase] = useState(base);
  const [settingsError, setSettingsError] = useState('');
  const [help, setHelp] = useState(false);
  const selected = state.issues.find(issue => issue.id === selectedId);
  const blocked = state.loading || !!state.pendingId || !!state.loadError;
  const visible = useMemo(() => state.issues.filter(issue =>
    (status === 'all' || issue.status === status) && (priority === 'all' || issue.priority === priority) &&
    `${issue.title} ${issue.description} ${issue.id}`.toLocaleLowerCase().includes(query.trim().toLocaleLowerCase())), [state.issues, query, status, priority]);
  const metrics = [
    { name: '全部问题', value: state.issues.length, filter: 'all', caption: '当前后端中的全部记录', className: 'all' },
    ...statuses.map(s => ({ name: statusLabels[s], value: state.issues.filter(i => i.status === s).length, filter: s, caption: s === 'open' ? '等待检查与安排' : s === 'in_progress' ? '正在跟进处理' : '已完成问题处理', className: s })),
  ];
  const clearFilters = () => { setQuery(''); setStatus('all'); setPriority('all'); };

  return <div className="app-shell">
    <aside className="sidebar">
      <div className="brand"><span className="brand-mark"><Symbol /></span><div>巡检工作台<small>INSPECTION DESK</small></div></div>
      <span className="nav-caption">现场管理</span>
      <nav aria-label="主导航"><button className="nav-active" onClick={() => { clearFilters(); setSelectedId(null); }}><Symbol />问题管理<span className="nav-count">{state.updatedAt ? state.issues.length : '—'}</span></button></nav>
      <div className="sidebar-note"><span className="note-dot" />从现场发现，到问题解决<p>接收 Unity 现场上报，集中查看并跟进处理进度。</p></div>
      <div className="sidebar-bottom"><span>虚拟设备间</span><small>本地演示 · 第四阶段</small><button onClick={() => setHelp(value => !value)}>使用说明 <span aria-hidden="true">↗</span></button></div>
    </aside>
    <div className="workspace">
      <header className="topbar"><div><span>现场管理</span><span className="slash">/</span><strong>问题管理</strong></div><button className="text-button" disabled={!!state.pendingId} onClick={() => { setDraftBase(base); setSettingsError(''); setSettingsOpen(true); }}><Symbol kind="settings" />连接设置</button></header>
      <main>
        <div className="page-heading"><div><p className="eyebrow">ISSUE MANAGEMENT</p><h1>每一个问题，都有迹可循。</h1><p className="intro">查看现场上报，确认问题详情，推动处理进度。</p></div><button className="primary" onClick={() => void state.reload()} disabled={state.loading || !!state.pendingId}><Symbol kind="refresh" />{state.loading ? '正在刷新…' : '刷新问题'}</button></div>
        {help && <section className="help-box"><h2>一次完整巡检流程</h2><p>在 Unity 第三阶段程序中点击设备并上报 → 点击此页“刷新问题” → 打开问题详情 → 选择处理状态。状态保存到 Go 后端，刷新网页后仍然保留。</p><p>当前页面不会自动轮询。Unity 重启恢复标记、同步管理端状态尚未实现。</p><button className="text-button" onClick={() => setHelp(false)}>收起说明</button></section>}
        <div className="metrics" aria-label="问题统计">{metrics.map(metric => <button key={metric.filter} className={`metric ${metric.className} ${status === metric.filter ? 'chosen' : ''}`} aria-pressed={status === metric.filter} onClick={() => setStatus(metric.filter)}><span>{metric.name}<i /></span><strong>{state.updatedAt ? metric.value : '—'}</strong><small>{metric.caption}</small></button>)}</div>
        {(state.loadError || state.actionError) && <div className="alert error" role="alert"><strong>{state.loadError ? '无法获取最新问题' : '状态修改未确认'}</strong><p>{state.loadError || state.actionError}</p>{state.updatedAt && state.loadError && <p>以下为上次获取的数据，恢复连接并刷新后才能修改。</p>}<button onClick={() => void state.reload()} disabled={state.loading || !!state.pendingId}>重新获取</button></div>}
        {state.notice && <div className="alert success" role="status">✓ {state.notice}</div>}
        <section className="issues-panel" aria-labelledby="list-title">
          <div className="panel-heading"><div><h2 id="list-title">问题列表 <span>{state.updatedAt ? state.issues.length : '—'}</span></h2><p>{state.updatedAt ? `最近获取 ${state.updatedAt.toLocaleTimeString('zh-CN', { hour12: false })}` : '等待获取现场数据'}</p></div><span className={`connection ${state.loadError ? 'offline' : state.loading ? 'loading' : 'online'}`}><i />{state.loadError ? '连接异常' : state.loading ? '连接中' : '已连接后端'}</span></div>
          <div className="toolbar"><label className="search"><Symbol kind="search" /><input aria-label="搜索问题" placeholder="搜索标题、描述或问题 ID" value={query} onChange={e => setQuery(e.target.value)} /></label><label className="filter">状态<select aria-label="筛选状态" value={status} onChange={e => setStatus(e.target.value)}><option value="all">全部状态</option>{statuses.map(s => <option key={s} value={s}>{statusLabels[s]}</option>)}</select></label><label className="filter">优先级<select aria-label="筛选优先级" value={priority} onChange={e => setPriority(e.target.value)}><option value="all">全部优先级</option>{priorities.map(p => <option key={p} value={p}>{priorityLabels[p]}优先级</option>)}</select></label></div>
          <div className="table-scroll"><table aria-label="巡检问题列表"><thead><tr><th>问题 / 编号</th><th>优先级</th><th>处理状态</th><th>上报时间</th><th><span className="sr-only">操作</span></th></tr></thead><tbody>
            {state.loading && !state.updatedAt ? <tr><td colSpan={5} className="empty" role="status">正在获取巡检问题…</td></tr> : visible.map(issue => <tr key={issue.id} aria-busy={state.pendingId === issue.id}><td><button className="issue-title" onClick={() => setSelectedId(issue.id)}>{issue.title}</button><span className="issue-id" title={issue.id}>{issue.id.slice(0, 12)}</span></td><td><span className={`priority ${issue.priority}`}><i />{priorityLabels[issue.priority]}优先级</span></td><td><StatusSelect issue={issue} disabled={blocked} onChange={state.changeStatus} />{state.pendingId === issue.id && <small className="saving" role="status">正在保存…</small>}</td><td className="date-cell">{formatTime(issue.createdAt)}</td><td><button className="detail-button" aria-label={`查看详情：${issue.title}`} onClick={() => setSelectedId(issue.id)}><Symbol kind="arrow" /></button></td></tr>)}
            {!state.loading && visible.length === 0 && <tr><td colSpan={5} className="empty"><span className="empty-icon"><Symbol /></span><strong>{state.loadError ? '暂时没有可展示的数据' : state.issues.length ? '没有符合筛选条件的问题' : '暂无上报问题'}</strong><p>{state.loadError ? '请检查 Go 服务，然后点击重新获取。' : state.issues.length ? '试试其他关键词，或清除筛选条件。' : '在 Unity 中提交第一条问题，再回来刷新列表。'}</p>{state.issues.length > 0 && <button className="text-button" onClick={clearFilters}>清除筛选</button>}</td></tr>}
          </tbody></table></div>
          <div className="table-footer"><span>显示 {visible.length} / {state.updatedAt ? state.issues.length : '—'} 条问题</span><span>修改状态后自动保存</span></div>
        </section>
        <footer className="page-footer"><span>数据来源 <code>{base}</code></span><span>Unity 现场上报 · Go 持久化保存</span></footer>
      </main>
    </div>
    {selected && <Modal titleId="detail-title" onClose={() => setSelectedId(null)} drawer><div className="dialog-header"><span className="eyebrow">ISSUE DETAILS</span><button aria-label="关闭详情" className="close" onClick={() => setSelectedId(null)}>×</button></div><h2 id="detail-title">{selected.title}</h2><span className={`priority ${selected.priority}`}><i />{priorityLabels[selected.priority]}优先级</span><dl className="details"><dt>问题编号</dt><dd className="mono">{selected.id}</dd><dt>现场描述</dt><dd className="description">{selected.description || '未填写描述'}</dd><dt>现场位置 · 世界坐标</dt><dd className="coordinates">{(['x', 'y', 'z'] as const).map(axis => <div key={axis}><small>{axis.toUpperCase()}</small><strong>{selected.position[axis]}</strong></div>)}</dd><dt>上报时间</dt><dd>{formatTime(selected.createdAt)}</dd><dt>最后更新</dt><dd>{formatTime(selected.updatedAt)}</dd></dl><div className="detail-action"><label>处理状态</label><StatusSelect issue={selected} disabled={blocked} onChange={state.changeStatus} detail /><p>选择状态后保存到后端。</p>{state.pendingId === selected.id && <p role="status">正在保存…</p>}{state.actionError && <p role="alert" className="inline-error">{state.actionError}</p>}{state.loadError && <p role="alert" className="inline-error">连接异常，请关闭详情后刷新列表。</p>}{state.notice && <p role="status">{state.notice}</p>}</div></Modal>}
    {settingsOpen && <Modal titleId="settings-title" onClose={() => setSettingsOpen(false)}><div className="dialog-header"><h2 id="settings-title">连接设置</h2><button className="close" aria-label="关闭连接设置" onClick={() => setSettingsOpen(false)}>×</button></div><p className="settings-copy">与 Unity 使用同一个 Go 后端地址。设置仅保存在当前浏览器会话。</p><form onSubmit={e => { e.preventDefault(); try { const normalized = normalizeBaseUrl(draftBase); if (normalized === base) void state.reload(); else setBase(normalized); try { sessionStorage.setItem('inspection-api-base', normalized); } catch { /* Private browsing may disable storage. */ } setSelectedId(null); clearFilters(); setSettingsOpen(false); } catch (error) { setSettingsError((error as Error).message); } }}><label className="settings-label">后端地址<input value={draftBase} onChange={e => setDraftBase(e.target.value)} autoFocus placeholder="http://127.0.0.1:8080" /></label>{settingsError && <p className="inline-error" role="alert">{settingsError}</p>}<div className="settings-actions"><button type="button" className="secondary" onClick={() => setDraftBase(defaultBaseUrl)}>恢复默认地址</button><button className="primary" type="submit">应用并刷新</button></div></form></Modal>}
  </div>;
}
