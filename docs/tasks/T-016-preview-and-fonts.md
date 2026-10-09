# T-016 服务端预览与字体下载

状态：待认领
负责人：—
里程碑：M3
依赖：T-001（字体注册表）
设计书：§4 保证所见即所得、§7 字体、§8 接口列表

## 目标

设计器能用服务端渲染引擎出真实 PNG 预览，并加载和服务端同一份字体，保证所见即所得。

## 范围

可以新建、修改的文件：

- 新建 `backend/src/LabelService.Server/Endpoints/PreviewEndpoints.cs`：`POST /api/v1/preview`（未保存的模板 JSON + 样例数据 → PNG，需要登录）
- `GET /fonts/{name}`：返回字体文件，带长缓存头
- `frontend/src/`：“服务端预览”按钮，`@font-face` 加载服务端字体

## 共享文件

- `backend/src/LabelService.Server/Endpoints/PublicApiEndpoints.cs`：去掉预览的 501 占位

## 验收标准

- [ ] 预览请求的模板不经过存储，直接校验后渲染；模板 JSON 非法时返回明确的错误位置
- [ ] 预览不计入开放接口指标（用单独的 endpoint 维度）
- [ ] 同一模板，设计器画布与服务端 PNG 的元素位置偏差 ≤ 0.2 mm（写一个对比脚本或检查）
- [ ] `verify.ps1` 通过（见 AGENTS.md）

## 不做

- 登录本身（见 T-014，这里可以先用开发期开关）

## 交接记录

<!-- 按时间倒序追加：日期 · 谁 · 做了什么 / 卡在哪里 / 留给下一位的话 -->
