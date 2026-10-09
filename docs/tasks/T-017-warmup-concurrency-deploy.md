# T-017 预热、并发控制与部署

状态：待认领
负责人：—
里程碑：M4
依赖：T-008（预热要遍历已发布模板）
设计书：§9 怎么做到、其他非功能需求；ADR 0003

## 目标

服务启动后第一个请求也不慢；高峰时请求排队而不是一起变慢；能用 Docker 或 Windows 服务单实例部署，数据有每日备份。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Hosting/`：启动预热（加载字体、图标、素材，用样例值把每个已发布模板各渲染一次）、就绪检查、渲染并发限制
- 新建 `backend/deploy/`：Dockerfile（Linux，字体随镜像打包，数据目录挂卷）、Windows 服务安装说明
- 每日备份：后台服务用 `VACUUM INTO` 给 `label.db`、`label-log.db` 生成快照，保留份数可配置
- `backend/src/LabelService.Server/LabelService.Server.csproj`：发布配置（ReadyToRun 需要指定运行时标识）

## 共享文件

- `backend/src/LabelService.Server/Program.cs`
- `backend/src/LabelService.Contracts/Protocol/ErrorCodes.cs`：排队超时的 503 如需新错误码，按契约变更流程处理（SDK 已把 503 统一映射为 SERVICE_UNAVAILABLE 并重试）

## 验收标准

- [ ] 预热完成前 `/health/ready` 不通过；30 秒内完成预热
- [ ] 启动后第一个请求 total 与热请求相差不超过 2 倍（当前骨架：61 ms 对 10 ms）
- [ ] 渲染并发数 = CPU 核数，多出的排队；排队超过 2 秒返回 503；排队时间进 `label_queue_wait_ms`
- [ ] Docker 镜像在没有任何系统中文字体的 Linux 上渲染中文正常
- [ ] 备份快照能直接替换库文件恢复（写进部署说明并实际演练一次）
- [ ] 部署说明写明：数据目录不能放网络共享盘，同一数据目录只能有一个服务进程
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- Kubernetes 编排、多实例与高可用（ADR 0003：前期单实例）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
