# 字体

服务端渲染文本用的字体有两个来源（ADR 0011），服务启动时全部加载（`LabelService.Rendering/Fonts/FontRegistry.cs`）：

- **部署目录**：程序目录的 `fonts/`（配置 `LabelService:Fonts:Directory` 可以指向别的目录）。开发构建时从这里（`backend/assets/fonts`）复制过去；**发布包（`dotnet publish`、Docker 镜像）不带字体**。
- **上传目录**：数据目录的 `fonts/`。正式环境由使用单位在管理后台“素材与字体”页上传自己有授权的字体，上传、删除后立即生效，不用重启。

**检查程序和基准都需要这里有思源黑体**，没有字体时文本检查会失败。

## 开发时放哪些文件

只用思源黑体、思源宋体（SIL Open Font License 1.1）。**不要放微软雅黑、宋体等系统字体**，授权不允许（AGENTS.md R7）。

两种发行版任选一种，模板里的 `SourceHanSansSC`、`SourceHanSerifSC` 都能匹配上：

| 发行版 | 文件 | 说明 |
| --- | --- | --- |
| Adobe：Source Han Sans / Serif | `SourceHanSansSC-Regular.otf`、`SourceHanSansSC-Bold.otf`、`SourceHanSerifSC-Regular.otf`、`SourceHanSerifSC-Bold.otf` | 每个字重一个文件。在 [source-han-sans](https://github.com/adobe-fonts/source-han-sans/releases)、[source-han-serif](https://github.com/adobe-fonts/source-han-serif/releases) 的 Release 里，选“SubsetOTF / SC”包 |
| Google：Noto Sans SC / Noto Serif SC | `NotoSansSC-VF.ttf`、`NotoSerifSC-VF.ttf` | 和思源是同一款字体，可变字体，一个文件包含全部字重。在 [noto-cjk](https://github.com/notofonts/noto-cjk/releases) 的 Release 里 |

正式环境上传的也建议先传思源黑体（默认字体）：模板没指定字体、或指定的字体找不到时都用它。

## 匹配规则

- 模板字体名和字体文件里的家族名比较时忽略大小写、空格、连字符、下划线。
- `SourceHanSansSC` 也认 Source Han Sans SC / CN、Noto Sans SC、Noto Sans CJK SC、思源黑体；`SourceHanSerifSC` 同理。
- 上传的其他字体按字体文件里的家族名使用（设计器的字体下拉框列出服务端已有的全部字体）。
- 可变字体按常规 400、粗体 700 生成实例；只有常规字重时，粗体用合成粗体。没有斜体字形时用合成斜体。
- 模板引用了不存在的字体时降级到思源黑体，响应头 `X-Label-Warnings` 里给 `FONT_FALLBACK:原字体名`。

## 现在的状态

字体文件几十 MB，没有提交到仓库（`.gitignore` 忽略了 `*.otf`、`*.ttf`、`*.ttc`、`*.otc`）。开发时各自按上表把字体放进这个目录；正式环境在管理后台上传。
