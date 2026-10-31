# 虚拟现场巡检问题标注

## 当前进度

第一阶段：Go HTTP API、SQLite 持久化和手动接口测试页。等待用户手动验收后再进入第二阶段。

尚未完成：Unity 客户端、React 管理端、演示视频、完整 AR 问答。GitHub 远端仓库尚未成功创建；当前仅初始化本地 Git。

## 版本与依赖

- 本机工具链：Go 1.27.3 windows/amd64（从 https://go.dev/dl/ 下载并核对 SHA-256，位于忽略的 `.tools/go`）。
- SQLite 驱动：modernc.org/sqlite v1.61.0（纯 Go，不需要 C 编译器），传递依赖见 `server/go.mod`、`server/go.sum`。
- Unity 计划使用已安装的 2022.3.62f3c1；React、TypeScript 的版本在对应阶段固定。

## 启动第一阶段

在 PowerShell 中运行：

```powershell
cd 'E:\Project\Unity-3D巡检实操题'
powershell -NoProfile -ExecutionPolicy Bypass -File .\start-backend.ps1
```

脚本编译并在当前终端运行后端。首次下载依赖或编译可能需要等待。浏览器访问 http://localhost:8080 。按 Ctrl+C 停止服务，再运行同一命令重新启动。

`-ExecutionPolicy Bypass` 只对该进程有效，不修改系统设置。默认使用项目内 Go；其他电脑可安装 Go 1.26 或更高版本并加入 PATH，然后运行同一脚本。依赖解析后的最低版本以 `server/go.mod` 为准。

启动脚本在未指定 GOPROXY 时临时优先使用 `https://goproxy.cn` 模块镜像，退出时恢复环境变量，保留 Go 默认模块校验。也可在当前 PowerShell 中设置自己的 `$env:GOPROXY`，脚本会尊重该值。

8080 被占用时可加 `-Port 8081`，测试地址相应改为 http://localhost:8081 。不要关闭其他项目的进程来抢占端口。

数据库固定为项目根目录 `data/inspection.db`，启动日志显示绝对路径。重启不会删除或初始化已有记录。`data`、`.tools` 和 `bin` 不提交到 Git。

也可以直接从源码运行：

```powershell
cd server
go run ./cmd/server -addr 127.0.0.1:8080 -db ../data/inspection.db
```

## 手动验收

1. 打开测试页，点击“检查服务”，看到服务和数据库正常。
2. 保留默认标题、high 优先级和坐标 1.25 / 0.8 / 2.4，提交。看到成功提示和新记录，状态为 open，ID 非空。记录 ID。
3. 将该记录状态改为 in_progress，再改为 resolved。刷新页面，状态仍为 resolved。
4. 点击“空白标题”“错误优先级”“缺少 Z 坐标”“错误状态”，均应看到 HTTP 400；展开“最近一次接口响应”查看错误。刷新列表，记录数量不变。
5. 保持网页打开，在服务终端按 Ctrl+C。点击网页“刷新列表”或“检查服务”，看到连接错误，页面不白屏。此时直接刷新整个浏览器会显示浏览器连接失败，因为测试页也是后端提供的；验证页内错误应保留已打开页面。
6. 再次启动同一脚本，点击“刷新列表”，之前的 ID、标题、坐标和 resolved 状态均保留。
7. 可自行尝试空描述、中文标题、负数坐标、零坐标以及三种优先级。

这是后端辅助测试页，不是任务书中的 React 管理端。

## API

基础地址 `http://localhost:8080`。请求和响应使用 JSON，POST/PATCH 请求带 `Content-Type: application/json`。

| 方法 | 路径 | 成功响应 |
| --- | --- | --- |
| GET | /api/health | 200 `{"status":"ok"}`，同时检查数据库连接 |
| POST | /api/issues | 201 完整问题对象 |
| GET | /api/issues | 200 `{"items":[...]}`，新记录在前；空列表为 `[]` |
| PATCH | /api/issues/{id}/status | 200 更新后的完整对象 |

创建请求：

```json
{"title":"1 号机柜温度异常","description":"机柜右侧温度明显偏高","priority":"high","position":{"x":1.25,"y":0.8,"z":2.4}}
```

状态更新请求：

```json
{"status":"resolved"}
```

完整对象字段严格为 `id,title,description,priority,status,position,createdAt,updatedAt`；`position` 包含 `x,y,z`。ID 为后端生成的 128 位随机值的十六进制字符串。新问题默认 `open`，时间为后端生成的 UTC RFC3339Nano 字符串。状态更新保留 createdAt 并更新 updatedAt。

标题去除首尾空白后为 1–120 个字符；描述可省略，最长 2000 个字符；优先级仅允许 low/medium/high；状态仅允许 open/in_progress/resolved，允许在三者间切换；坐标要求完整、非 null、有限数值，允许零和负数。

客户端不能指定 id/status/时间等创建时的后端字段。拒绝未知字段、多份 JSON 和错误字段类型；最大请求体 64 KiB。

错误响应示例：

```json
{"error":{"code":"VALIDATION_ERROR","message":"标题不能为空且不能超过 120 个字符","field":"title"}}
```

400 参数/JSON 错误；403 来源不允许；404 问题不存在；413 请求过大；415 Content-Type 错误；500 数据库操作失败；503 健康检查失败。未匹配的路由或方法由标准库返回 404/405。

CORS 默认允许 `http://localhost:5173`、`http://127.0.0.1:5173` 及 8080 测试页的两个本机来源，支持 GET/POST/PATCH/OPTIONS 和 Content-Type。自定义端口用启动脚本或 `-origins` 配置。服务仅监听本机，当前无认证，不用于公网部署。

## 自动验证

```powershell
cd 'E:\Project\Unity-3D巡检实操题\server'
..\.tools\go\bin\go.exe test ./...
..\.tools\go\bin\go.exe vet ./...
```

测试覆盖创建及状态更新、数据库关闭后重新打开的持久化、唯一 ID、空列表、非法输入不写入、零坐标、非法状态、未知 ID、CORS、请求大小、媒体类型及数据库不可用。测试使用临时数据库，不影响手动验收数据。

2026-10-31：上述自动化测试、`go vet`、脚本语法检查通过；另用真实服务进程和独立临时数据库验证中文创建、状态修改、测试页返回、跨域预检、停止进程并重启后的数据保留，均通过。浏览器中的实际点击操作与用户手动验收待完成。

## 已知限制

- 未实现创建请求幂等性；超时后应先刷新确认记录，再决定是否再次提交。测试页不自动重试创建。
- 列表尚未分页，无删除功能；当前服务适用于小规模本地演示。
- Unity 实际射线、UI 误触、React 浏览器跨域联调需在后续阶段验证，不能用当前后端测试替代。
- 完整交付所需 AR 问答和两段 3–5 分钟视频待后续补齐；“真机”暂按实际桌面电脑运行理解，最终需确认。
