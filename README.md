<p align="center">
  <img src="assets/logo.png" alt="smart-label logo" width="200"/>
</p>

<p align="center">
  <strong>smart-label</strong>
</p>

一个基于 Windows Forms 的标签打印小工具，使用 Seagull BarTender SDK 打印 .btw 模板文件并支持：

- 动态数据源输入（可配置数量）
- 每个数据源可配置“显示名称”和映射到模板的字段名（如 `DS1` 或自定义字段）
- 在模板文件夹中自动加载 `.btw` 模板，通过下拉框选择要打印的模板
- 模板预览（异步导出到临时图片并自动清理）
- 回车在数据源输入间跳转，最后一项回车会触发打印并清空输入
- 配置保存/加载（保存时需要输入密码以避免误操作）

## Logo 说明

在仓库中可以放置一个项目 logo 文件以在 README 中显示。默认位置：

- `assets/logo.png`

如果你想更换 logo，请把图像文件放到项目的 `assets` 子目录（若不存在请创建），并命名为 `logo.png` 或调整上方 `<img>` 的路径。

## 要求

- Windows + Visual Studio（建议使用 Visual Studio 2019/2022）
- .NET Framework 4.7.2
- BarTender (Automation/Enterprise Automation) 已安装并正确注册
- 引用 `Seagull.BarTender.Print` 程序集（BarTender SDK）在项目中可用

注意：打印与预览功能依赖本地安装的 BarTender 引擎。

## 项目结构（简要）

- `smart-label`：WinForms 程序，主窗体 `Form1` 实现主要交互
- `Form1.cs`：逻辑实现（配置读写、模板列表、打印、预览、动态输入）

## 运行

1. 在 Visual Studio 中打开解决方案 `smart-label.sln`。
2. 确保项目引用了 BarTender SDK（`Seagull.BarTender.Print`），并且本机已安装 BarTender。
3. 编译并运行。

默认行为：程序启动时会在可执行文件目录下尝试读取 `config.ini`（存在则加载配置）。

## 模板文件夹

程序默认使用可执行目录下的 `templates` 子目录来放 `.btw` 模板文件；程序启动或加载配置会扫描该目录并把 `.btw` 文件列在下拉框中。

你也可以通过程序保存的 `config.ini` 中更改 `General -> TemplatesFolder` 值来指定其他目录。

## 配置与保存

- 配置保存在程序目录下的 `config.ini`。
- 保存配置时会提示输入保存密码以避免误操作（默认密码：`20251129`）。
- 保存项包括：数据源数量、每个数据源的显示名称/字段映射、模板文件夹路径和每个数据源当前的值。

## 使用说明

1. 在顶部设置“数据源数量”（初始为 0）。新增时会提示输入该行的显示名称与映射字段（可改为 `DS1`、`DS2` 或模板中实际字段名）。
2. 在下方为每个数据源输入对应值。回车会跳到下一项；最后一项回车会触发打印并在打印完成后清空所有输入并把焦点移回第一项。
3. 选择模板下拉框查看可用 `.btw` 模板，右侧显示预览（异步生成临时图片）。
4. 点击“打印所选模板”按钮触发打印（等同于在最后一项回车触发）。

## 模板预览实现说明

当前实现采用“异步导出到临时图片 + 加载到 PictureBox + 删除临时文件”的方式：

- 优点：兼容性好，可指定分辨率，避免阻塞 UI，临时文件自动清理。
- 注意：导出依赖 BarTender 导出图片的 API，不同版本 SDK 可能有不同的重载或行为。

如果需要改为“导出到内存流”或“使用 BarTender 自带预览窗口”，可以进一步修改实现。

## 常见问题

- 无法打印或预览：请确认 BarTender 已安装、许可正确并且程序引用了 `Seagull.BarTender.Print`。
- 预览失败：检查 BarTender 是否支持 `ExportImage`/`ExportImageToFile`，或查看临时目录权限。
- 需要在无头/服务环境打印：BarTender 引擎需要相应的运行权限和桌面堆配置，参考 BarTender SDK 文档关于 "non-interactive" 的说明。

## 开发者说明

- 语言：C# (.NET Framework 4.7.2)
- 主窗体：`smart-label/Form1.cs`，UI 定义为 `Form1.Designer.cs`。
- 推荐在开发机上安装 BarTender 并调试打印/预览流程。

如需我把 README 添加更多示例、API 调用说明或把预览改为内存渲染，请告诉我要优先实现的选项。