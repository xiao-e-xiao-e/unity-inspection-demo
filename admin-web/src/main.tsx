import { Component, StrictMode } from 'react';
import type { ErrorInfo, ReactNode } from 'react';
import { createRoot } from 'react-dom/client';
import App from './App';
import './styles.css';

class ErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error: Error, info: ErrorInfo) { console.error('管理页面渲染失败', error, info.componentStack); }
  render() { return this.state.failed ? <main className="fatal"><h1>页面暂时无法显示</h1><p>请刷新页面重试；如果仍有问题，请提供控制台错误信息。</p><button onClick={() => window.location.reload()}>重新加载</button></main> : this.props.children; }
}
createRoot(document.getElementById('root')!).render(<StrictMode><ErrorBoundary><App /></ErrorBoundary></StrictMode>);
