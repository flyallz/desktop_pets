# 猫咪桌宠 · Desktop Pets

给我的好闺闺的桌宠。

新增 **桌宠工坊跨平台内测**：在 [platform/](platform/README.md) 中用照片制作、预览并保存自己的宠物包。网站与 Windows / Mac M 系列 / Mac Intel 客户端共用动作和文件格式。[v0.5 内测下载](https://github.com/flyallz/desktop_pets/releases/tag/v0.5.0-beta.2) · [跨平台检查](https://github.com/flyallz/desktop_pets/actions/workflows/platform-build.yml) · [后续产品路线](docs/product-roadmap.md)。

下文是可直接发给朋友的 **Windows 成品 v0.4.3**，基于原来的 v0.4 系列继续改进。网站用于不同用户制作自己的桌宠；这个 EXE 已内置朋友这只猫，无需制作或导入宠物包。

Windows 桌面猫咪陪伴程序：默认会自己在桌面走动、坐着陪伴、伸懒腰和打盹。保留原照坐姿，坐着时会眨眼、轻动耳朵、摆动尾尖；睡觉时有缓慢呼吸，轻点会醒来并伸个懒腰。

当前版本：**v0.4.3 · 照片造型也会动**。六种原照片造型都有各自的局部动作，保留 v0.4 系列的行走、伸懒腰、睡觉和自动活动，以及 DeepSeek 聊天和可关闭的主动问候。

换造型直接使用主人提供的不同照片，经本地抠图后切换，包含原版坐姿、红毛衣、歪头、侧坐、露肚皮睡觉和背影。原有行走、伸展和趴睡动作素材沿用 v0.4 的照片衍生素材。

![六种原照片造型](docs/photo-looks.png)

![五种新增造型的实际动作预览](docs/photo-motion.gif)

![猫咪桌宠预览](docs/pet-preview.png)

## 下载运行

从 [v0.4.3 下载页](https://github.com/flyallz/desktop_pets/releases/tag/v0.4.3) 下载 `DesktopPets-0.4.3-win-x64.exe`，双击即可运行。图片已经装在 EXE 里，朋友无需安装制作工具。更新前先通过猫咪或托盘的右键菜单退出旧版。

## 从源码编译

目标环境：Windows 10 / 11，64 位，已安装 .NET Framework 4.8。编译使用 Windows 自带的 .NET Framework C# 编译器，无需安装 Python、Node.js 或额外编译包。

在 PowerShell 中进入仓库目录，运行：

```powershell
.\build.ps1
```

生成文件位于 `发布包\猫咪桌宠.exe`，双击即可运行。图片和图标已嵌入 EXE，可以单独复制给朋友。

## 使用

- 左键按住猫咪拖动；轻点猫咪会回应，能看到睁开眼睛的造型会慢眨眼。
- **双击猫咪换照片造型**；也可右键“换造型”直接选择。会记住上次选择，原有动作结束后回到选中的照片。每张照片分别校准了小动作：红毛衣、歪头、侧坐会眨眼，所有造型都有呼吸和轻动耳朵，可见的尾部会轻摆。露肚皮原照已闭眼、背影看不到眼睛，保留原样。关闭“自动切换动作”后，原地的小动作仍会继续；“暂停全部动作”才会完全停住。
- **右键“和猫咪聊天”**打开聊天窗口，Enter 发送，Shift + Enter 换行；可以停止正在等待的回复。
- **右键“聊天设置”**填写自己的 DeepSeek API Key。可开启每 15、30、60 或 120 分钟主动问候，默认关闭；右键“打个招呼”可立即请求问候。
- 默认开启“自动切换动作”：启动约 6 秒后先走一小段，随后在原地陪伴、行走、伸懒腰和打盹之间轮换；每轮都会包含这些动作，顺序会变化。
- 行走使用 8 帧迈步动作，一次完整行走约 5～8 秒，起步和收步逐渐变速；到当前屏幕工作区边缘会停顿约 0.3 秒再转向，随后逐渐起步，拖动或打开右键菜单时停下。结束拖动后至少留出约 8 秒，再自动选择下一动作。
- 右键“走一走”可立即行走；取消勾选“自动切换动作”可停止后续自动活动，并立即停止当前行走。手动动作仍可使用。
- 右键“伸个懒腰”：完成约 3 秒的伸展后回到坐姿。
- 右键“睡一会儿”：趴着睡觉，直到轻点它或选择“叫醒它”；醒来会睁眼、伸懒腰，再坐好。
- 自动动作之间会原地停留约 7～13 秒；自动小睡约 32 秒后醒来，手动选择的睡觉会等你叫醒。
- 右键可选择大小、暂停全部动作、隐藏、回到右下角或退出。暂停会停在当前姿态；恢复后继续。手动选择行走、伸懒腰、睡觉或叫醒会恢复动作。
- 隐藏后，在任务栏右侧托盘找到猫咪图标，双击或选择“显示猫咪”恢复。
- 未配置聊天时离线运行；配置后，仅在发起聊天、手动问候或开启的主动问候到期时请求 DeepSeek。隐藏、睡觉、暂停或正在聊天时不主动问候。
- 密钥使用 Windows 当前账户加密，保存在 `%LOCALAPPDATA%\PhotoCat\settings.json`。EXE 和仓库均不包含你的密钥。聊天历史只在内存中保留，不写入磁盘；调用费用由所填写密钥的 DeepSeek 账户承担。
- 不设置开机自启，不读取屏幕或文件。重启后记住照片造型与聊天设置，位置和大小仍恢复默认值。

姿态内部用局部动画保留毛发和轮廓，睡着与醒来的图使用相同位置，只过渡眼睛区域。坐姿、伸展和趴下之间目前采用关键姿态切换，还不是逐帧连续的起卧动画。行走已有 8 帧循环，转向会先短暂停顿，再切换左右镜像，尚无连续转身画面；生成帧仍有少量毛发和轮廓差异。

## 文件说明

| 文件 | 用途 |
|---|---|
| `src/CatPet.cs` | 独立 Windows 程序及自身验证入口 |
| `src/PetCompanion.cs`、`src/ChatWindows.cs` | 照片选择、独立聊天窗口与主动问候 |
| `src/DeepSeekChat.cs`、`src/PetPreferences.cs` | DeepSeek 异步请求、有限对话上下文与密钥加密 |
| `src/CompanionChecks.cs` | 新增功能的本地模拟接口与 WPF 窗口验证 |
| `assets/looks/` | 五种新增原照片透明造型，原图不随仓库发布 |
| `docs/photo-looks.md` | 照片来源、处理与显示边界 |
| `src/PhotoMotion.cs` | 原照局部动画、行走帧、自动轮换和动作命中区域 |
| `src/IdlePhotoMotion.cs` | 每张照片的眼睛、耳朵、呼吸和可见尾部坐标及局部动作 |
| `src/PhotoMotionChecks.cs` | 新照片动作的连续渲染、轮廓、眼睛方向与鼠标命中检查 |
| `src/app.manifest` | Windows 权限和显示缩放声明 |
| `assets/cat.png` | 原照抠图的坐姿 |
| `assets/cat-stretch.png` | 伸懒腰姿态 |
| `assets/cat-rest.png`、`assets/cat-sleep.png` | 对齐的醒来与睡觉素材 |
| `assets/cat-walk-0.png` 至 `cat-walk-7.png` | 对齐的 8 帧行走素材 |
| `assets/cat.ico` | 程序图标 |
| `build.ps1` | 将代码和素材编译为一个 EXE |
| `tools/` | 可选的本地照片抠图和行走图集处理脚本 |
| `requirements-image.txt` | 已验证的图片处理依赖版本 |
| `docs/verification.txt` | 本机样品验证记录 |
| `docs/pose-assets.md` | 新姿态来源、处理方式与生成提示词 |

仓库不包含原始房间照片、下载模型、开发环境或临时构建结果。使用现有素材编译时不需要下载模型。

## 验证

编译后运行程序自身的检查模式：

```powershell
$qaPath = Join-Path $PWD 'qa'
$petExe = Join-Path $PWD '发布包\猫咪桌宠.exe'
$check = Start-Process -FilePath $petExe -ArgumentList @('--self-test', ('"' + $qaPath + '"')) -PassThru -Wait
$check.ExitCode
Get-Content .\qa\verification.txt
```

退出码 `0` 表示检查通过。当前本机记录为 250 项通过，详情见 `docs/verification.txt`。检查覆盖透明图片、窗口、鼠标穿透、尺寸、点击反馈、暂停、托盘和隐藏恢复；还检查动作曲面不会翻折、脚掌像素保持稳定、摆尾后的命中区域，以及真实计时器驱动的慢眨眼。

检查会输出 `qa/pet-preview.png`、`qa/motion/` 下的原有动作图，以及 `qa/postures/` 下的新姿态对照图和 180 张演示帧。新检查覆盖睡眠保持、点击叫醒、自动小睡、动作中途切换、姿态变换后的透明鼠标区域和新素材的真实透明通道。

v0.4 增加 `qa/walking/` 中的双向行走帧和演示帧。检查覆盖八帧顺序、镜像后的鼠标命中区域、启动后无人操作的自动轮换、关闭与重开自动模式、边缘转向、步幅与移动距离，以及拖动、菜单、暂停、隐藏时的停止行为。

v0.4.1 另外检查起步与收步的变速、完整步态循环、不同计时间隔下的总距离一致性，以及边缘停顿后向内转向并逐渐起步。

v0.4.2 另检查照片切换、透明命中、动作后恢复所选照片、设置重读和密钥加密；使用本地模拟 HTTP 检查聊天的成功、无密钥、余额/权限/限流错误、断网、超时和取消，并操作真实 WPF 聊天与设置窗口。`qa/companion/` 包含照片和窗口的实际渲染。**未提供真实 API Key，因此没有声称真实 DeepSeek 在线回复已验证。**

v0.4.3 另外检查五张照片的独立眨眼方向、耳朵、呼吸、尾部、固定鼻尖及脚掌位置，以及动作后的鼠标命中区域。`qa/companion/motion-*/` 包含五组各 90 张连续 WPF 实际渲染帧；450 帧均检查未出现曲面翻折或轮廓缺失。预览为了展示动作安排了较密集的触发时间，正常运行仍沿用原有的自然间隔。离屏渲染耗时不代表桌面运行帧率。

DeepSeek 使用官方 HTTPS 接口和 `deepseek-flash` 非思考模式，单次等待最长 45 秒；参见 [DeepSeek 官方接口文档](https://api-docs.deepseek.com/api/create-chat-completion/)。

6 分钟的模拟行为序列用于检查流程，不代表 6 分钟实机试用或不同电脑的帧率表现。

这些检查不代替真实鼠标拖动、托盘点击、混合缩放多屏和其他电脑上的实机试用。

## 更换猫咪图片

这版所有姿态共用 953 × 1347 透明画布，动作坐标专门对应当前猫咪。换猫或改变图片裁剪后，需要在 `src/PhotoMotion.cs` 与 `src/IdlePhotoMotion.cs` 中重新校准局部动作、睡觉眼睛区域和落脚位置，不能只换图片就得到正确动作。PNG 应有真实透明背景、完整轮廓及少量边距。更新图片、图标与坐标后，再运行 `build.ps1`。

若需复现本次本地抠图流程，可使用 Python 3.11：

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements-image.txt
.\.venv\Scripts\python.exe tools\download_model.py
.\.venv\Scripts\python.exe tools\make_cutout.py "C:\path\to\your-cat.jpg"
.\build.ps1
```

首次下载的 IS-Net 模型约 179 MB，保存在 `.build/models/`，脚本会验证官方校验值。抠图在本机完成；结果写入 `assets/`，对照预览写入 `qa/`。脚本会覆盖这两个目录中同名的生成素材，更换前请保留自己的版本。

`make_cutout.py` 中的木地板残影清理针对当前白猫坐在木地板上的示例；更换为其他毛色、宠物或背景时，应重新检查边缘，必要时调整这部分处理。它尚不是通用的照片定制服务。

模型和预处理参考：[rembg IS-Net 实现](https://github.com/danielgatis/rembg/blob/main/rembg/sessions/dis_general_use.py)。

行走图集使用内置图像工具生成，随后用 `python tools/prepare_walk.py "C:\path\to\walking-atlas.png"` 在本地抠出 8 帧；该脚本针对本次绿色背景、4 × 2 排列的图集。处理会覆盖 `assets/cat-walk-0.png` 至 `cat-walk-7.png`，并输出对齐检查图。具体来源和提示词见 `docs/pose-assets.md`。

## 后续产品方向

网页可负责上传照片、确认形象、选择动作和配置功能；桌面程序负责悬浮、互动和本地提醒。下一步以猫主人的试用反馈为依据，再决定是否制作自助定制网站。