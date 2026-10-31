import { afterEach, expect, it, vi } from 'vitest';
import { listIssues, updateStatus } from './api';
afterEach(() => { vi.unstubAllGlobals(); vi.useRealTimers(); });
it('拒绝非 JSON 响应', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('<html>error</html>', { status: 200 })));
  await expect(listIssues('http://127.0.0.1:8080')).rejects.toThrow('无法解析');
});
it('错误的成功响应不能确认状态修改', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })));
  await expect(updateStatus('http://127.0.0.1:8080', 'id', 'resolved')).rejects.toThrow('结果未确认');
});
it('8 秒超时终止请求并给出可操作提示', async () => {
  vi.useFakeTimers();
  vi.stubGlobal('fetch', vi.fn((_url: string, options: RequestInit) => new Promise((_resolve, reject) => {
    options.signal?.addEventListener('abort', () => reject(new DOMException('Aborted', 'AbortError')));
  })));
  const check = expect(listIssues('http://127.0.0.1:8080')).rejects.toThrow('请求超时');
  await vi.advanceTimersByTimeAsync(8001); await check;
});
