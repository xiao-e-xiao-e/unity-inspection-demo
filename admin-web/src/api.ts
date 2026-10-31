export const statuses = ['open', 'in_progress', 'resolved'] as const;
export const priorities = ['low', 'medium', 'high'] as const;
export type Status = typeof statuses[number];
export type Priority = typeof priorities[number];
export interface Issue {
  id: string; title: string; description: string; priority: Priority; status: Status;
  position: { x: number; y: number; z: number }; createdAt: string; updatedAt: string;
}
export const statusLabels: Record<Status, string> = { open: '待处理', in_progress: '处理中', resolved: '已解决' };
export const priorityLabels: Record<Priority, string> = { low: '低', medium: '中', high: '高' };
export const defaultBaseUrl = import.meta.env.VITE_API_BASE_URL || 'http://127.0.0.1:8080';

export function normalizeBaseUrl(value: string): string {
  let url: URL;
  try { url = new URL(value.trim()); } catch { throw new Error('请输入有效的后端地址，例如 http://127.0.0.1:8080'); }
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password || url.search || url.hash)
    throw new Error('后端地址仅支持 HTTP/HTTPS，不能包含账号、查询参数或片段。');
  return url.toString().replace(/\/$/, '');
}

function object(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null; }
export function isIssue(value: unknown): value is Issue {
  if (!object(value) || !object(value.position)) return false;
  return ['id', 'title', 'description', 'createdAt', 'updatedAt'].every(key => typeof value[key] === 'string') &&
    !!value.id && !!value.title && priorities.includes(value.priority as Priority) && statuses.includes(value.status as Status) &&
    ['x', 'y', 'z'].every(key => typeof (value.position as Record<string, unknown>)[key] === 'number' && Number.isFinite((value.position as Record<string, unknown>)[key]));
}

async function request(base: string, path: string, options: RequestInit = {}, signal?: AbortSignal): Promise<unknown> {
  const controller = new AbortController();
  const abort = () => controller.abort();
  signal?.addEventListener('abort', abort, { once: true });
  if (signal?.aborted) controller.abort();
  let timedOut = false;
  const timer = setTimeout(() => { timedOut = true; controller.abort(); }, 8000);
  const write = options.method === 'PATCH';
  try {
    const response = await fetch(normalizeBaseUrl(base) + path, { ...options, signal: controller.signal, redirect: 'error' });
    let data: unknown;
    try { data = await response.json(); } catch { throw new Error('后端返回了无法解析的数据，请检查服务地址。' + (write ? '修改结果未确认，请刷新核对。' : '')); }
    if (!response.ok) {
      const message = object(data) && object(data.error) && typeof data.error.message === 'string' ? data.error.message : '请求失败';
      throw new Error(`HTTP ${response.status}：${message}` + (write && response.status >= 500 ? '；修改结果未确认，请刷新核对。' : ''));
    }
    return data;
  } catch (error) {
    if (signal?.aborted) throw new DOMException('Cancelled', 'AbortError');
    if (timedOut) throw new Error('请求超时，请检查 Go 服务后重试。' + (write ? '修改结果未确认，请先刷新核对。' : ''));
    if (error instanceof TypeError) throw new Error('无法连接后端，请检查 Go 服务、连接地址与跨域配置。' + (write ? '修改结果未确认，请刷新核对。' : ''));
    throw error;
  } finally { clearTimeout(timer); signal?.removeEventListener('abort', abort); }
}

export async function listIssues(base: string, signal?: AbortSignal): Promise<Issue[]> {
  const data = await request(base, '/api/issues', {}, signal);
  if (!object(data) || !Array.isArray(data.items) || !data.items.every(isIssue) || new Set(data.items.map(item => item.id)).size !== data.items.length)
    throw new Error('后端问题列表格式不正确，请检查接口返回的数据。');
  return data.items;
}

export async function updateStatus(base: string, id: string, status: Status): Promise<Issue> {
  const data = await request(base, `/api/issues/${encodeURIComponent(id)}/status`, {
    method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ status }),
  });
  if (!isIssue(data) || data.id !== id || data.status !== status)
    throw new Error('状态更新响应不正确，结果未确认，请刷新列表核对。');
  return data;
}
