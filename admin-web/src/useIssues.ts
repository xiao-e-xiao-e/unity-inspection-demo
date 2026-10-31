import { useCallback, useEffect, useRef, useState } from 'react';
import { listIssues, updateStatus } from './api';
import type { Issue, Status } from './api';

export function useIssues(base: string) {
  const [issues, setIssues] = useState<Issue[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [actionError, setActionError] = useState('');
  const [notice, setNotice] = useState('');
  const [updatedAt, setUpdatedAt] = useState<Date | null>(null);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const requestId = useRef(0);
  const listController = useRef<AbortController | null>(null);
  const mutation = useRef(false);
  const mounted = useRef(true);

  const reload = useCallback(async () => {
    if (mutation.current) return;
    const id = ++requestId.current;
    listController.current?.abort();
    const controller = new AbortController(); listController.current = controller;
    setLoading(true); setLoadError(''); setActionError(''); setNotice('');
    try {
      const items = await listIssues(base, controller.signal);
      if (id !== requestId.current || !mounted.current) return;
      setIssues(items); setUpdatedAt(new Date());
    } catch (error) {
      if (controller.signal.aborted || id !== requestId.current || !mounted.current) return;
      setLoadError(error instanceof Error ? error.message : '读取问题失败，请重试。');
    } finally { if (id === requestId.current && mounted.current) setLoading(false); }
  }, [base]);

  useEffect(() => {
    mounted.current = true; setIssues([]); setUpdatedAt(null); void reload();
    return () => { mounted.current = false; listController.current?.abort(); requestId.current++; };
  }, [reload]);

  const changeStatus = async (issue: Issue, status: Status) => {
    if (mutation.current || loading || loadError || status === issue.status) return;
    mutation.current = true; setPendingId(issue.id); setActionError(''); setNotice('');
    try {
      const updated = await updateStatus(base, issue.id, status);
      if (!mounted.current) return;
      setIssues(items => items.map(item => item.id === updated.id ? updated : item));
      setNotice(`“${updated.title}”的状态已保存。`);
    } catch (error) { if (mounted.current) setActionError(error instanceof Error ? error.message : '修改失败，请刷新核对。'); }
    finally { mutation.current = false; if (mounted.current) setPendingId(null); }
  };
  return { issues, loading, loadError, actionError, notice, updatedAt, pendingId, reload, changeStatus };
}
