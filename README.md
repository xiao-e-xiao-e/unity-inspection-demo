# 虚拟现场巡检问题标注

Unity 虚拟现场 → 点击标记并上报 → Go + SQLite 保存 → React 查看与修改状态。

仓库：[unity-inspection-demo](https://github.com/xiao-e-xiao-e/unity-inspection-demo)。不需要手机、付费素材或公网部署。

## 交付状态

必做功能源码已实现：基础设备间、移动与视角、射线命中标记、问题填写与取消、提交反馈、UI 防误触、React 列表与状态修改、Go 校验/持久化/CORS。管理端另有搜索、筛选和详情。

上报位置保存原始 hit.point；球心为贴面显示沿法线外移 0.08，不把视觉偏移写入问题坐标。各项设计、代码位置、排错案例及验证范围见下方实现说明。

用户已于 2026-11-01 确认功能测试全部通过、无异常；详见验收记录。当前整理为一套统一系统。**独立克隆启动核验、本人代码审核、两段视频及分享链接仍待完成。**

- [最终验收清单与验证证据](docs/acceptance.md)
- [实现说明与评审核验](docs/implementation-review.md)
- [两段视频录制脚本](docs/demo-recording.md)
- [AI 使用记录](AI_LOG.md)

## 版本与源码

| 部分 | 版本 | 目录 |
| --- | --- | --- |
| Unity / C# | 2022.3.62f3c1 LTS，内置渲染管线、旧 Input Manager、IMGUI | unity-client |
| React / TypeScript | React 19.3.1、TypeScript 7.0.2、Vite 8.3.5 | admin-web |
| Node / npm | 本机 24.21.0 / 11.19.0，建议 Node 24 LTS | package-lock.json 固定依赖 |
| Go / SQLite | 本机 Go 1.27.3；模块最低 Go 1.26；modernc.org/sqlite v1.61.0 | server |

Unity 包含 Assets 及 .meta、Packages、ProjectSettings。Library、Temp、Logs、node_modules、dist、bin、data、.tools 不入库。未安装 AR Foundation，本题 AR 部分仅作基础理解。

## 启动

克隆仓库后，在项目根目录打开 PowerShell。本机目录为 `E:\Project\Unity-3D巡检实操题`；其他电脑替换为自己的克隆目录。首次安装依赖需要联网。

**Go 后端**：安装 Go 1.26 或更高版本并加入 PATH；本机也可使用已有 `.tools/go`。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\start-backend.ps1
```

保留终端运行；Ctrl+C 停止，再执行原命令重启。默认监听 `127.0.0.1:8080`，数据库固定为根目录 `data/inspection.db`，重启不清空。已有服务时不要重复启动。可加 `-Port 8081`，同时更改两端地址。脚本临时使用 Go 模块镜像，保留校验并尊重已有 GOPROXY。也可在 server 目录执行 `go run ./cmd/server -addr 127.0.0.1:8080 -db ../data/inspection.db`。

**React 管理端**：安装 Node.js，新开终端，在根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\start-admin.ps1
```

首次自动执行 npm ci；打开 http://localhost:5173 ，保持终端运行。也可在 admin-web 执行 `npm.cmd ci`、`npm.cmd run dev`。5173 占用时明确退出，不自动换端口。后端只提供 API，唯一管理页面是 5173。

默认后端为 `http://127.0.0.1:8080`，右上角“连接设置”可修改，仅保存于当前标签页会话；启动/构建前也可设置 VITE_API_BASE_URL。页面不自动轮询，Unity 上报后点“刷新问题”。

**Unity 客户端**：Unity Hub 添加 unity-client，用上述版本打开。加载 `Assets/Inspection/Scenes/InspectionRoom.unity`，按 Play，在 Game 视图操作。WASD 移动，按住右键转向，松开右键后左键点击放置，R 回到起点，Esc 取消草稿。

本机可运行 `bin/unity/InspectionDemo.exe`，配套目录不能拆开。构建产物不在 Git；新电脑可直接使用 Editor，或在 File > Build Settings 加入该场景后构建 Windows x86_64。自动构建入口是 `Inspection.Editor.InspectionBuilder.BuildAll`。在项目根目录执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build-unity.ps1` 构建，执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\start-unity.ps1` 启动。

Unity 使用同一后端地址；左上角设置或 `-apiBaseUrl http://127.0.0.1:8080` 参数可更改。黄色球为草稿，确认保存后变绿，取消则移除。

## API

基础地址 `http://127.0.0.1:8080`，POST/PATCH 使用 application/json。

| 方法 | 路径 | 成功响应 |
| --- | --- | --- |
| GET | /api/health | 200 `{"status":"ok"}`，同时检查数据库 |
| POST | /api/issues | 201 完整问题对象 |
| GET | /api/issues | 200 `{"items":[...]}`，新记录在前 |
| PATCH | /api/issues/{id}/status | 200 完整更新对象 |

创建示例：

```json
{"title":"1 号机柜温度异常","description":"机柜右侧温度明显偏高","priority":"high","position":{"x":1.25,"y":0.8,"z":2.4}}
```

后端补充唯一 id、默认 status=open、createdAt、updatedAt（UTC RFC3339Nano）。三端字段统一为 `id,title,description,priority,status,position,createdAt,updatedAt`。更新请求 `{"status":"resolved"}`；open/in_progress/resolved 可互相切换，分别显示待处理/处理中/已解决。

标题去空白后 1–120 字符，描述可省略且最多 2000 字符；priority 仅 low/medium/high；坐标须完整且有限。拒绝未知字段、错误类型与超过 64 KiB 的请求。错误为 `{"error":{"code":"VALIDATION_ERROR","message":"…","field":"title"}}`。

400 参数错误，403 来源不允许，404 ID 不存在，413 过大，415 媒体类型错误，500 数据库错误，503 健康检查失败。未匹配路由/方法可能返回标准库非 JSON 404/405，客户端仍显示错误。

CORS 允许 localhost/127.0.0.1 的 5173 与 8080 来源及 GET/POST/PATCH/OPTIONS。三端指向同一 Go 地址即使用同一数据。服务仅供本机演示，无认证，不用于公网。

## AR 基础理解

**1. 平面检测与点击放置需要什么？** 以 AR Foundation 5.x 为例，使用 ARSession、XR Origin 和对应平台 XR 插件。ARPlaneManager 检测平面；ARRaycastManager 将屏幕点击投射到平面，可限定 TrackableType.PlaneWithinPolygon，在返回的命中 pose 放置标记；用 ARAnchorManager 管理锚点。UI 点击仍须排除。本项目 Physics.Raycast 只检测预设 Collider，不会自动识别现实表面。参考：[管理器](https://docs.unity.cn/Packages/com.unity.xr.arfoundation@5.1/manual/architecture/managers.html)、[AR 射线](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.1/manual/features/raycasts.html)。

**2. 为什么仅保存 x/y/z 不能保证重启恢复真实位置？** 坐标依赖会话原点与跟踪结果，重启后原点可能改变；三个数字不包含与现实环境的绑定。跨会话恢复需要平台支持的持久化锚点/地图及重定位，或利用已知图像、设备标识重新校准，并保存关联 ID 和相对姿态。普通运行时锚点不等于跨启动持久化。参考：[会话坐标](https://docs.unity.cn/Packages/com.unity.xr.arfoundation@4.2/manual/index.html)。

**3. 初始化失败、不支持或 Tracking 异常如何提示？** 检查可用性并监听 ARSession.stateChanged，结合跟踪状态显示原因。初始化显示进度；不支持时明确提示并允许退出或进入普通 3D；权限/服务问题给出恢复指引。跟踪不稳时暂停放置，提示缓慢移动、改善光照或对准有纹理区域；恢复后开放操作，保留输入，不假报成功。这是拟采用的方案，未编写真实 AR 功能。参考：[会话可用性与状态](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@5.1/manual/features/session.html)。

## 验证与已知限制

在 server 执行 `go test ./... -count=1`、`go vet ./...`；在 admin-web 执行 `npm.cmd test`、`npm.cmd run build`。2026-11-01 均通过；前端 10 项测试使用模拟 HTTP。用户已完成实际鼠标键盘功能测试。统一后的客户端通过 Unity 编译与构建检查；详细证据及范围见验收记录。

可选完整自动联调：先构建后端和 Unity，再运行 `pwsh -NoProfile -File .\scripts\test-integration.ps1`。使用 18083/18084/18085 端口及 `.tools` 内独立数据库，端口占用时退出；会打开测试客户端并在结束时关闭自己的进程，不影响日常数据库。仅在显式 `-inspectionSmoke` 参数下执行测试，普通启动不触发。此次整理只验证脚本语法、编译和现有 Go/React 测试，未重跑该写入联调。

- 创建无幂等键，超时后先刷新核对再决定重试，避免重复上报。
- Unity 重启不恢复标记，绿色球不随管理端状态变化，属于未实现的加分项。
- 列表未分页，无删除；多管理端同时修改时后写覆盖先写，适用于小规模演示。
- 两段 3–5 分钟视频未录制。“真机”按实际桌面运行理解，若评审要求外部摄像头拍摄需录制前确认。
- AI_LOG 的自查纠错不是用户人工审核；本人需完成验收并能解释核心代码。
