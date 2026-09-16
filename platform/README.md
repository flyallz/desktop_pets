# 桌宠工坊 · 跨平台内测

网站直接制作自己的桌宠，保存为可携带的宠物包；Windows、Mac Apple Silicon、Mac Intel 共用格式与动作逻辑。工作名称尚未作为商标或域名注册。

本目录是新的跨平台版本，原仓库中的 C# Windows v0.4.1 保持独立。**v0.4.1 不能导入本网站的宠物包，请使用本目录的客户端。**

## 当前可用

- 导入 JPG / PNG / WebP 照片，归一化画布并去掉原文件的 EXIF。
- 可选本机抠图；没有模型时可直接使用透明 PNG 或画笔擦除，支持 5 步撤销。
- 示例猫具备原地呼吸、8 帧行走、伸懒腰、睡觉；可自行轮换。
- 换成自己的照片后只保留原地陪伴。行走需添加至少 4 张姿态帧，不能用示例猫冒充。
- 自定义大小、活动节奏、休息提醒；可试用 25 分钟专注。
- 保存、重新导入 `.petpack.json`。文件包含照片、动作和设置，不含脚本或远程图片地址。
- Electron 桌面端有透明窗口、托盘、拖动、原生菜单、宠物包导入及本地保存。

未接入账号、云存储、AI 新姿态生成或支付。当前是本地制作流程内测，不代表商业平台已上线。

## 启动网站

需要 Node.js 22.12 或更新的 Node 22。首次在本目录运行：

```powershell
npm ci
npm run dev
```

打开 <http://127.0.0.1:4317>。在该终端按 Ctrl+C 停止。端口被占用时会报错，不会自动换到其他项目端口。

也可在 Windows 使用 `start-local.ps1` / `stop-local.ps1` 隐藏启动和停止本项目服务。首次仍需运行 `npm ci`。

网页草稿在当前页面内存中；关闭前请保存宠物包。没有服务器账号备份。用户上传的照片不写入仓库。

## 可选的本机自动抠图

服务只监听 127.0.0.1:4318，网页通过同源代理调用。单任务执行，输入上限 10 MB / 2000 万像素，60 秒处理超时。照片经内存管道传给 Python，不保留原图文件；这不是可以直接暴露到公网的上传 API。

Python 需要 Pillow、NumPy、ONNX Runtime，可使用仓库现有 `requirements-image.txt` 中相应版本。自行下载 [ISNet 官方项目发布的通用分割模型](https://github.com/danielgatis/rembg/releases/download/v0.0.0/isnet-general-use.onnx)，并设置：

```powershell
$env:PET_PYTHON = "C:\path\to\python.exe"
$env:PET_MODEL_PATH = "C:\path\to\isnet-general-use.onnx"
# 可选：已有依赖目录。普通 pip 安装不需要。
# $env:PET_PYTHON_DEPS = "C:\path\to\deps"
npm run api
```

然后保持服务运行，刷新网站。模型不会自动下载，执行前会校验已知模型摘要。云端自动生成新姿态是另一项尚未接入的能力。

## 桌面客户端

```powershell
npm run desktop
```

右键猫咪或托盘可以打开制作器、导入宠物包、选择动作、暂停或退出。隐藏后从托盘恢复。手动睡眠等到点击叫醒；自动小睡会自行结束。关闭客户端会停止提醒与计时。

文件在 Electron 的 `app.getPath('userData')` 下保存为 `pet.json`：Windows 通常是 `%APPDATA%\desktop-pets-platform\pet.json`，Mac 通常是 `~/Library/Application Support/desktop-pets-platform/pet.json`；以实际应用名称为准。备份该文件或制作器导出的宠物包即可。删除此文件可恢复示例。没有屏幕录制和自动开机启动。

## 打包 Windows 和两种 Mac

```powershell
npm run pack:win
# 以下须在 macOS 构建机运行：
npm run pack:mac
```

输出位于 `release/`。Mac 使用 Universal 二进制，包含 arm64 和 x64，最低 macOS 13。当前未签名、未公证，只供受控内测；不建议要求用户关闭系统安全机制。正式发放前需要 Developer ID 签名、公证及两种架构真机试用。[Electron 系统支持](https://www.electronjs.org/blog/electron-44-0)、[Apple 签名证书说明](https://developer.apple.com/help/account/certificates/create-developer-id-certificates)。

仓库的 [跨平台构建](https://github.com/flyallz/desktop_pets/actions/workflows/platform-build.yml) 分别使用 Windows、Apple Silicon Mac、Intel Mac；构建后提供测试下载。下载 Actions 附件需要 GitHub 登录；这些有保留期的附件不是长期面向消费者的下载地址。

## 验证与维护

```powershell
npm test
npm run build
# 本机有 Edge 且网站/抠图服务已启动时：
node tests/ui-check.mjs
# 原生窗口启动检查（会短暂显示测试猫咪后自行退出）：
npx electron . --smoke-test
```

自动检查不替代 macOS 真机上的拖动、透明鼠标穿透、托盘、多屏与安装体验检查。构建成功也不代表已签名或已公证。

- `shared/`：版本化宠物包校验、动作与计时逻辑。
- `src/`：网页制作器和共用预览。
- `electron/`：隔离的桌面系统接口；页面没有 Node 权限。
- `server/`：仅本机的抠图辅助服务。
- `public/demo/`：已授权公开的缩小版猫素材，PNG 内嵌来源记录。
- `tools/prepare_demo.py`：从根目录已授权素材重新制作网页示例。
- [后续产品路线](../docs/product-roadmap.md)：服务器用途、商业化与正式上线顺序。
