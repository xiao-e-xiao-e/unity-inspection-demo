# 实现说明与评审核验

本项目完成一条本地巡检流程：Unity 记录位置并上报，Go 写入 SQLite，React 查看并修改状态。场景使用基础对象，验证重点是坐标、数据流和异常处理。

2026-11-01，提交者按步骤操作并反馈功能测试全部通过，见 [验收记录](acceptance.md)。下文对应现有代码，不将尚未完成的视频、跨机器验证和本人代码审核写成完成证据。

## 1. 射线与实际命中位置

[SurfaceProbe.cs](../unity-client/Assets/Inspection/Scripts/SurfaceProbe.cs) 的 Update 排除 UI、已打开表单和右键观察状态后，通过 ScreenPointToRay 生成射线。TryPick 使用 Physics.Raycast，最远 30 个 Unity 单位，只检测 InspectionSurface 层并忽略 Trigger；未命中不创建草稿。

两个坐标用途不同：

```csharp
HitPosition = hit.point;
marker.transform.position = hit.point + hit.normal * 0.08f;
```

HitPosition 是实际表面命中点，也是提交的 position。标记球直径 0.16，球心外移 0.08 是为了贴面显示，避免半个球陷入表面。**球心不是上报坐标**；当前没有额外创建位于 hit.point 的父对象，不能声称球体 Transform 就是原始命中点。

球的 Collider 被禁用并销毁，且放在 Ignore Raycast 层，避免后续点击打到标记自己。机柜装饰片也不参与射线，位置核验应对照设备主碰撞体。

核验：点击地面，Y 接近 0；当前固定场景机柜正面的 Z 接近 1；改变点击位置，坐标随之变化。上报后在 React 详情核对同一 position。[InspectionSceneBuilder.Validate](../unity-client/Assets/Inspection/Editor/InspectionSceneBuilder.cs) 还检查地面/机柜的精确命中点和无命中情况。

## 2. 三端闭环与持久化

| 环节 | 实现位置与行为 | 核验结果 |
| --- | --- | --- |
| Unity 创建 | [IssueWorkflow.Submit](../unity-client/Assets/Inspection/Scripts/IssueWorkflow.cs) 读取表单与原始 position；[IssueApiClient.Create](../unity-client/Assets/Inspection/Scripts/IssueApiClient.cs) POST /api/issues | 客户端不生成 ID，不指定初始状态 |
| Go 校验 | [httpapi.create](../server/internal/httpapi/api.go) 校验标题、枚举和完整坐标 | 非法输入返回明确错误，不写入 |
| SQLite 写入 | [Store.Create](../server/internal/storage/sqlite.go) 生成随机 ID、open 和 UTC 时间，用参数化 SQL 保存 | 写入失败不返回 201；创建成功后才确认保存 |
| React 处理 | [api.ts](../admin-web/src/api.ts) GET 列表、PATCH 状态；[useIssues.ts](../admin-web/src/useIssues.ts) 收到有效响应后更新显示 | 同一 ID 可切换 open/in_progress/resolved |
| 重启恢复 | 脚本指向 data/inspection.db，建表使用 IF NOT EXISTS | 原 ID、坐标和处理状态不因 Go 进程退出而丢失 |

两端默认连接同一个 8080 后端，Go 配置 CORS 供 5173 页面访问，没有各自维护的演示数据。提交者已验证“上报 → 网页查看 → 改状态 → 重启 Go 后保留”。React 单元测试使用模拟响应，不拿它替代真实闭环。

## 3. 操作、UI 和网络失败

| 情况 | 当前处理及理由 |
| --- | --- |
| 移动与转向 | [FirstPersonMotor](../unity-client/Assets/Inspection/Scripts/FirstPersonMotor.cs) 用 CharacterController 处理碰撞；右键观察，失焦时释放鼠标 |
| UI 防误触 | [InspectionHud](../unity-client/Assets/Inspection/Scripts/InspectionHud.cs) 用 BlocksSceneInput、ContainsScreenPoint、suppressedFrame 屏蔽输入；当前是 IMGUI，通过面板矩形和状态判断，没有使用 EventSystem.IsPointerOverGameObject |
| 一个草稿 | 已打开表单时拒绝新的 ShowHit；取消调用 Clear，避免填写期间累积待保存标记 |
| 重复点击 | 发请求前置 IsSubmitting，期间禁止再次提交和取消，阻止同一请求尚未结束时重复写入 |
| Unity 网络失败 | 8 秒超时，保留输入与草稿；仅确认 HTTP 201 和关键返回字段后变绿，不提前显示成功 |
| React 失败 | 显示错误；加载失败时保留并标注旧数据、禁用编辑；首次失败也保留页面；状态更新失败不提前改成已解决 |

提交者断开后端后的截图显示 `HTTP 502：请求失败。结果未确认，请先核对网页列表。`，标题和黄色草稿仍在。随后恢复 Go、核对列表，再用原表单重试成功。502 的具体产生环节未进一步定位，不能直接认定是 Go 或某个代理返回；这份证据说明失败提示和草稿保留有效。

重复点击保护不等于请求幂等。服务端可能已经写入，但响应丢失，所以创建失败后不自动重试，先刷新核对。当前没有幂等键，这个风险没有被包装成“已解决”。

## 4. 与真实 AR 的区别

这里的位置来自预设场景与 Collider，原点由工程定义。真实 AR 需要建立跟踪坐标、识别平面并处理跟踪状态，不是换成摄像头背景就完成迁移。

AR Foundation 能力、跨会话恢复问题和异常提示方案见 [README 的 AR 问答](../README.md#ar-基础理解)，附有官方资料。项目没有接入 AR Foundation、真实平面检测或持久化锚点，也没有进行 AR 真机验证。保存虚拟世界 x/y/z 不等于能跨启动定位同一个现实位置。

## 5. 实际排错记录

这些修正由 AI 辅助执行，提交者完成后续功能手动测试；不写成提交者独立排障经历。

| 现象 | 定位和修正 | 验证与出处 |
| --- | --- | --- |
| 重开场景后，机柜校验可能找到无碰撞体的文字标签 | 标签与机柜同名，GameObject.Find 可能选错；标签增加 Label: 前缀，保存后重新打开场景校验 | 场景校验和构建通过；AI_LOG P-008；当前生成器 Label 方法可查看 |
| 隐藏 Unity 窗口得到黑色截图 | 延迟仍无效，改用可见窗口，并增加非暗像素比例校验，不能只判断文件存在 | 当时实际截图已查看；[InspectionSmoke](../unity-client/Assets/Inspection/Scripts/InspectionSmoke.cs) 保留检查；AI_LOG P-008/P-009 |
| 前端构建 ENOENT | 发现无用途的 CSS data URL import，删除该导入 | 重新生产构建通过；AI_LOG P-010 |

复查命令见 README。保留失败、定位、修正和验证过程，不用“系统稳定可靠”等没有范围的评价替代结果。

## 6. AI 使用与本人核验

AI 辅助编写代码、自动测试、构建脚本和文档。提交者要求逐项实施和测试，亲自运行三端，验证上报、状态保存、重启持久化、取消、UI 隔离、失败重试、重复提交及输入校验，最后明确反馈“全部通过，无异常”。记录见 [AI_LOG](../AI_LOG.md) P-013 至 P-024。

功能测试不能单独证明已经理解代码。提交前仍需本人阅读并能现场解释：

1. 把上报坐标换成 marker.transform.position，会出现什么误差？
2. 表单关闭后为什么还要检查 suppressedFrame？
3. 为什么提交超时不能证明数据库没有保存？
4. Go 重启后从哪里读取数据？页面显示过是否算持久化？
5. 为什么不能直接用这些 position 实现真实 AR 的跨启动定位？

本人审核记录见 [验收文档](acceptance.md#本人代码检查与解释)。至少记录一次本人实际检查、修改或放弃 AI 输出的过程；目前确认的是行为测试，不代填本人代码理解结论，也不虚构人工改代码经历。

## 7. 尚未完成与限制

| 内容 | 状态及影响 |
| --- | --- |
| 两段 3–5 分钟视频与分享链接 | 待录制、回放与核验；脚本已经准备，脚本不算视频 |
| 独立克隆首次启动、本人代码审核 | 待完成；现有通过结果来自本机已有环境 |
| 统一入口后的完整人工复验 | 已过 Unity 编译/构建及 Go/React 自动回归；用户全流程反馈来自清理前版本 |
| Unity 恢复标记、同步网页状态 | 未实现；绿色只表示本次运行确认上报，不表示已解决 |
| 创建幂等、并发冲突处理 | 未实现；失败先核对，多管理端修改时后写覆盖先写 |
| 鉴权、分页、删除、公网部署 | 未实现，适用于小规模本地演示 |

完整交付包括源码、运行说明、AI_LOG、验收记录和实际视频。评审可依次看代码位置、做一次操作、核对同一条记录、检查失败行为，不需要依赖自我评价。
