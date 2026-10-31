import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import App from './App';
import type { Issue } from './api';

const issue: Issue = { id: 'test-001', title: '1 号机柜温度异常', description: '右侧温度偏高\n请检查风扇', priority: 'high', status: 'open', position: { x: 1.25, y: 0.8, z: 2.4 }, createdAt: '2026-10-31T08:00:00Z', updatedAt: '2026-10-31T08:00:00Z' };
const reply = (value: unknown, status = 200) => Promise.resolve(new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } }));
beforeAll(() => {
  HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
  HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
});
afterEach(() => { cleanup(); vi.unstubAllGlobals(); sessionStorage.clear(); });

describe('问题管理交互', () => {
  it('空列表显示引导，不伪造数据', async () => {
    vi.stubGlobal('fetch', vi.fn(() => reply({ items: [] })));
    render(<App />); expect(await screen.findByText('暂无上报问题')).toBeTruthy();
    expect(screen.getByText('在 Unity 中提交第一条问题，再回来刷新列表。')).toBeTruthy();
  });
  it('后端不可用时显示错误，恢复后重试能读取数据', async () => {
    const fetcher = vi.fn().mockRejectedValueOnce(new TypeError('offline')).mockImplementation(() => reply({ items: [issue] }));
    vi.stubGlobal('fetch', fetcher); render(<App />);
    expect(await screen.findByRole('alert')).toBeTruthy(); expect(screen.getByRole('heading', { level: 1 })).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: '重新获取' }));
    expect(await screen.findByRole('button', { name: issue.title })).toBeTruthy();
    expect(screen.queryByRole('alert')).toBeNull();
  });
  it('筛选与详情显示完整字段', async () => {
    vi.stubGlobal('fetch', vi.fn(() => reply({ items: [issue] }))); render(<App />);
    fireEvent.click(await screen.findByRole('button', { name: issue.title }));
    expect(screen.getByRole('dialog')).toBeTruthy(); expect(screen.getByText('test-001', { selector: 'dd' })).toBeTruthy();
    expect(screen.getByText('1.25')).toBeTruthy(); expect(screen.getByText(/右侧温度偏高/)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: '关闭详情' }));
    fireEvent.change(screen.getByLabelText('筛选状态'), { target: { value: 'resolved' } });
    expect(screen.getByText('没有符合筛选条件的问题')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: '清除筛选' }));
    expect(screen.getByRole('button', { name: issue.title })).toBeTruthy();
    fireEvent.change(screen.getByLabelText('搜索问题'), { target: { value: '不存在的标题' } });
    expect(screen.getByText('没有符合筛选条件的问题')).toBeTruthy();
  });
  it('等待后端确认才更新状态，提交中不能重复修改', async () => {
    let resolvePatch!: (value: Response) => void;
    const fetcher = vi.fn((_url: string, options: RequestInit) => options.method === 'PATCH' ? new Promise<Response>(resolve => { resolvePatch = resolve; }) : reply({ items: [issue] }));
    vi.stubGlobal('fetch', fetcher); render(<App />);
    const select = await screen.findByLabelText(`列表状态：${issue.title}`) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'resolved' } });
    expect(select.value).toBe('open'); expect(select.disabled).toBe(true);
    fireEvent.change(select, { target: { value: 'in_progress' } });
    expect(fetcher.mock.calls.filter(([, options]) => options.method === 'PATCH')).toHaveLength(1);
    await act(async () => resolvePatch(new Response(JSON.stringify({ ...issue, status: 'resolved' }), { status: 200 })));
    await waitFor(() => expect(select.value).toBe('resolved'));
    expect(screen.getByText(/的状态已保存/)).toBeTruthy();
  });
  it('修改失败保留旧状态，显示错误而不报告成功', async () => {
    vi.stubGlobal('fetch', vi.fn((_url: string, options: RequestInit) => options.method === 'PATCH' ? reply({ error: { message: '数据库不可用' } }, 500) : reply({ items: [issue] })));
    render(<App />); const select = await screen.findByLabelText(`列表状态：${issue.title}`) as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'resolved' } });
    expect(await screen.findByRole('alert')).toBeTruthy(); expect(select.value).toBe('open');
    expect(screen.queryByText(/的状态已保存/)).toBeNull();
  });
  it('刷新失败保留上次数据，并禁止对旧列表继续修改', async () => {
    vi.stubGlobal('fetch', vi.fn().mockImplementationOnce(() => reply({ items: [issue] })).mockRejectedValue(new TypeError('offline')));
    render(<App />); await screen.findByRole('button', { name: issue.title });
    fireEvent.click(screen.getByRole('button', { name: '刷新问题' }));
    expect(await screen.findByText(/以下为上次获取的数据/)).toBeTruthy();
    expect(screen.getByRole('button', { name: issue.title })).toBeTruthy();
    expect((screen.getByLabelText(`列表状态：${issue.title}`) as HTMLSelectElement).disabled).toBe(true);
  });
  it('异常响应不会导致白屏，连接设置拒绝非法地址', async () => {
    vi.stubGlobal('fetch', vi.fn(() => reply({ items: [{ ...issue, position: null }] })));
    render(<App />); expect(await screen.findByRole('alert')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: '连接设置' }));
    fireEvent.change(screen.getByLabelText('后端地址'), { target: { value: 'javascript:bad' } });
    fireEvent.click(screen.getByRole('button', { name: '应用并刷新' }));
    expect(screen.getByText(/后端地址仅支持/)).toBeTruthy(); expect(screen.getByRole('dialog')).toBeTruthy();
  });
});
