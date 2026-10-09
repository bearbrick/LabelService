# 前端（frontend）

网页设计器和管理后台：Vue 3 + TypeScript + Vite，画布用 Konva + vue-konva，浏览器端条码预览用 bwip-js。只通过 HTTP 和模板 JSON 跟后端交互。

```
frontend/
├─ package.json  package-lock.json   依赖（版本写死，不加 ^）
├─ vite.config.ts                    开发时把 /api 转给 http://localhost:5080
├─ tsconfig.json  index.html
└─ src/
   ├─ main.ts  App.vue  style.css    入口、三栏布局、样式
   ├─ components/DesignerCanvas.vue  Konva 画布
   ├─ model/
   │  ├─ template.ts                 模板 JSON 的 TypeScript 镜像（契约）
   │  └─ units.ts                    mm、pt、打印点换算
   └─ samples/box-label.json         示例模板（和 backend/samples/templates 保持一致）
```

## 常用命令

```powershell
cd frontend
npm ci            # 安装依赖
npm run dev       # 开发，http://localhost:5173
npm run build     # 类型检查（vue-tsc）+ 打包
```

需要 Node 20.19+ 或 22.12+。调接口时先启动后端服务（见 `backend/README.md`）。

## 要点

- `src/model/template.ts` 是后端 Contracts 的镜像，改动属于契约变更，见根目录 AGENTS.md。后端检查程序会核对它包含全部元素类型。
- `src/samples/box-label.json` 是后端示例模板的副本，只用于管理后台完成前的演示，后端检查程序会核对两份一致。
- 长度一律 mm，换算只用 `src/model/units.ts`；画布状态就是模板 JSON，不另造一份状态。
- 不引用 `frontend/` 以外的文件（后端检查程序会拦）。
