# 虚拟现场巡检问题标注

unity-inspection-demo — Virtual inspection demo built with Unity, React, TypeScript and Go.

## 当前进度

第一阶段：Go HTTP API、SQLite 持久化和手动接口测试页已实现；自动测试通过，用户未明确反馈手动验收结果。

第二阶段：Unity 设备间、移动/视角、射线命中验证已实现，自动检查通过。

第三阶段：用户授权继续实现 Unity 问题填写、取消、提交和 Go 联调。完成后等待用户手动验收，再进入 React 管理端阶段。未将“进入下一阶段”当作用户已反馈完整验收结果。

尚未完成：标记恢复、React 管理端、演示视频、完整 AR 问答。

GitHub 仓库：https://github.com/xiao-e-xiao-e/unity-inspection-demo （由用户创建，公开可访问）。

## 版本与依赖

- 本机工具链：Go 1.27.3 windows/amd64（从 https://go.dev/dl/ 下载并核对 SHA-256，位于忽略的 `.tools/go`）。
- SQLite 驱动：modernc.org/sqlite v1.61.0（纯 Go，不需要 C 编译器），传递依赖见 `server/go.mod`、`server/go.sum`。
- Unity 使用已安装的 2022.3.62f3c1，内置渲染管线和旧版 Input Manager，无第三方素材或额外输入包；React、TypeScript 的版本在对应阶段固定。

## 第三阶段：问题填写与上报（当前版本）

### 启动

1. 保持第一阶段 Go 服务运行。如果没有运行，在项目根目录执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\start-backend.ps1`。已有服务占用 8080 时，不要再重复启动。
2. 双击本机 `bin/unity-stage3/InspectionDemo.exe`；第二阶段程序可以关闭，它不会提供表单和上报功能。程序目录中的 Data、DLL 等配套文件不能拆开。
3. 点击左上角“检查后端”，应提示服务和数据库连接正常。默认地址 `http://127.0.0.1:8080`；可点“后端地址设置”修改，只对本次运行生效。也可通过 `-apiBaseUrl http://127.0.0.1:8081` 启动参数指定。
4. 浏览器打开 http://localhost:8080 ，用于核对 Unity 提交后的数据库记录。这仍是第一阶段测试页，React 管理端尚未实现。

从源码运行：用 Unity 2022.3.62f3c1 打开 `unity-client`，打开 `Assets/Inspection/Scenes/InspectionRoom.unity`，按 Play 并在 Game 视图中操作。后端不需要和 Unity 同时重启。

### 手动验收清单

| 测试 | 操作 | 预期 |
| --- | --- | --- |
| 正常上报 | 左键点机柜；填中文标题、可选描述，选择 low/medium/high 后提交 | 弹出带命中坐标的表单；提交中按钮禁用；成功后关闭表单，黄色临时球变为绿色，提示后端生成的 ID |
| 后端核对 | 网页点击“刷新列表” | 标题、描述、优先级、x/y/z 与 Unity 一致，状态 open，ID 和时间非空；描述及时间可从接口 `/api/issues` 查看 |
| 标题校验 | 空标题或仅空格时提交 | 明确提示标题必填；表单和临时球保留，不新增记录 |
| 单个草稿 | 打开表单后点击场景背景、连续点击或长按 | 不再创建其他临时球，不改变已经选定的坐标 |
| 取消 | 填写后点“取消并移除标记”或按 Esc | 表单关闭、未保存的球消失、数据库不新增；已经保存的绿色球不删除 |
| 输入与移动隔离 | 输入标题时按 W/A/S/D/R，或按住右键移动鼠标 | 仅编辑输入内容，不移动、转向或重置角色 |
| UI 防误触 | 点击输入框、优先级、按钮、页面说明区 | 不在场景中新增临时标记；关闭表单的那次点击不穿透到场景 |
| 重复提交 | 快速连续点击提交 | 提交中禁止再次提交和取消；网页仅新增一条问题 |
| 后端不可用 | 在后端终端 Ctrl+C 停止 Go，然后新建问题并提交 | 显示连接失败或超时，保留标题、描述、优先级和临时球；不能提示成功 |
| 恢复后重试 | 重新启动 Go，先核对网页列表未保存该条，再回 Unity 提交 | 使用原表单成功提交，无需重新点击设备和填写 |
| 重启持久化 | 成功提交后重启 Go，网页刷新 | 原 ID、字段和状态仍在 |

网络中断/超时时，服务器可能已经保存但响应未到达。客户端提示“结果未确认”，不自动重试；必须先核对列表再决定重试。首版尚无请求幂等键。

当前绿色球仅代表本次运行已确认保存。重启 Unity 后不会恢复旧标记，但 Go 数据仍在；从后端恢复标记属于后续加分功能。位置是射线实际 `hit.point`，不是球体沿法线偏移后的视觉坐标。

### 第三阶段实现与验证

`IssueWorkflow` 管理浏览/填写/提交状态，校验输入并防止重复提交。`IssueApiClient` 用 UTF-8 JSON 和 UnityWebRequest 访问 Go，8 秒超时，不跟随重定向，不自动重试。成功必须是 HTTP 201 且返回完整问题对象；错误响应和内容异常均不能把草稿变成已保存状态。`InspectionHud` 继续使用 IMGUI，表单弹出后屏蔽全部场景输入。

Unity 编译及 Windows 构建入口：`Inspection.Editor.StageThreeBuilder.BuildAll`。升级菜单 `Inspection > Upgrade Scene to Stage 3` 会保存当前固定场景的第三阶段组件引用。不要用第二阶段 Rebuild 菜单覆盖当前场景。

自动联调脚本：`pwsh -NoProfile -File .\scripts\test-stage3.ps1`。需要本机 Go 工具链、编译好的 `bin/inspection-server.exe` 与 `bin/unity-stage3/InspectionDemo.exe`。脚本使用 18083/18084/18085 端口和 `.tools` 内独立数据库，端口已占用则停止测试，不关闭用户进程。可见 Unity 窗口会自动操作并退出，日志和截图保存在输出的 Artifacts 目录。

覆盖内容：空标题/过长描述/错误地址不发送请求、取消清理、重复提交抑制、真实 Go 保存中文和换行、连接失败保留草稿、恢复后重试、HTTP 400/500、错误成功响应、超时，以及真实后端进程重启后记录仍在。自动测试不会替代用户实际键盘鼠标输入验收。

2026-10-31：第三阶段场景升级、Windows 构建、上述真实联调脚本均通过；已检查可见窗口表单截图。源码与第三阶段程序已就绪，等待用户手动测试。

参考：[UnityWebRequest](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.UnityWebRequest.html)、[JsonUtility](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/JsonUtility.html)。

## 第二阶段记录（旧测试程序）

以下保留第二阶段验收记录，适用于 `bin/unity-stage2` 中的旧程序。当前 Unity 源码场景已升级第三阶段，编辑器运行时应按上面的第三阶段清单测试。

本阶段无需启动 Go。黄色球体只是射线命中预览，不表示问题已保存，不发送网络请求。

### 方法 A：Windows 测试程序

本机已构建时，双击 `bin/unity-stage2/InspectionDemo.exe`。整个 `unity-stage2` 目录必须保持完整。程序产物不提交 Git；其他电脑可以按方法 B 打开源码，或者自行通过 Unity 的 File > Build Settings > Windows x86_64 构建。

### 方法 B：Unity 编辑器

```powershell
cd 'E:\Project\Unity-3D巡检实操题'
powershell -NoProfile -ExecutionPolicy Bypass -File .\open-unity.ps1
```

也可在 Unity Hub 中添加 `unity-client` 目录，用 **2022.3.62f3c1** 打开。首次导入完成后，在 Project 面板双击 `Assets/Inspection/Scenes/InspectionRoom.unity`，按顶部 Play，再点击 Game 画面使其获得焦点。不要在 Scene 视图中测试角色输入。结束时再次点击 Play。

| 步骤 | 操作 | 预期 |
| --- | --- | --- |
| 1 | 查看起始画面 | 两台带编号机柜、后方水泵和管线、地面与墙体；左上角中文操作说明 |
| 2 | WASD 移动，按住右键移动鼠标 | 可以走动并转动视角；松开右键恢复鼠标指针 |
| 3 | 朝墙或机柜走 | 被碰撞体挡住，不穿墙、不穿机柜，不落到地板下 |
| 4 | 松开右键，左键点击两台机柜不同位置及地面 | 底部显示不同对象名称和命中世界坐标；黄色球贴近所点表面 |
| 5 | 地面点击 | Y 应接近 0；机柜正面点击时 Z 应接近 1.000（命中主碰撞体） |
| 6 | 按住左键，再在不同位置单击 | 一次按下仅触发一次；新的点击移动同一个预览点，不累积球体；点击计数按有效点击增加 |
| 7 | 点击左上角按钮、文字区域和底部面板 | 不增加有效点击次数、不向背后场景放置预览；清除按钮删除已有预览 |
| 8 | 按 Esc | 清除预览并释放鼠标；不是退出程序 |
| 9 | 走远后按 R 或“回到起点” | 回到初始位置与朝向 |
| 10 | Alt+Tab 切出再返回 | 鼠标不被永久锁住；可以重新按住右键观察 |

关闭 Windows 程序用标题栏关闭按钮或 Alt+F4。退出后预览不恢复，属于本阶段预期行为。

代码职责：`FirstPersonMotor` 管理 CharacterController、重力和鼠标；`SurfaceProbe` 使用单次点击和限定 LayerMask 做 Physics.Raycast，保存原始 hit.point；`InspectionHud` 提供阶段测试 HUD，显式屏蔽 UI 区域的场景点击；`InspectionTarget` 提供稳定编号及名称。

预览球仅视觉上沿法线偏移，显示坐标没有偏移；预览无碰撞体并在 Ignore Raycast 层，不会挡住后续射线。当前 HUD 为 IMGUI 测试界面，尚未添加问题输入表单。静态设备装饰片不参与射线，命中设备主碰撞体。

编辑器菜单 `Inspection > Validate Stage 2 Scene` 校验固定测试场景的机柜/地面命中点、无命中射线、墙体碰撞。`Inspection > Rebuild Stage 2 Scene` 会重新生成当前阶段场景，覆盖该场景文件；手动调整场景后不要随意使用此菜单。

2026-10-31 第二阶段自动检查：场景保存后重新打开验证通过；Windows Development Build 成功；实际程序内的 UI 区域判断、十次选择仅一个预览、清除、原始命中坐标、移动与墙体碰撞、回到起点检查通过。可见窗口截图已检查，中文 HUD 和场景正常。真实键盘鼠标操作仍等待用户验收。

可复现运行时检查（会自动移动角色并退出，不适用于手动体验）：从项目根目录运行 `bin\unity-stage2\InspectionDemo.exe -stage2Smoke -smokeScreenshot stage2-check.png -logFile stage2-check.log`。这是 Development Build 中通过显式参数启用的测试，普通双击运行不会执行。需要可见窗口；隐藏窗口的图像捕获可能是黑屏。

实现参考：[CharacterController.Move](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/CharacterController.Move.html)、[Physics.Raycast](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Physics.Raycast.html)、[Unity 命令行](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html)。

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
- Unity 实际鼠标/键盘体验与 UI 点击需要第二阶段手动验收；React 浏览器跨域联调需在对应阶段验证，不能用后端测试替代。
- 完整交付所需 AR 问答和两段 3–5 分钟视频待后续补齐；“真机”暂按实际桌面电脑运行理解，最终需确认。
