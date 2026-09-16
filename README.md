# 猫咪桌宠 · Desktop Pets

给我的好闺闺的桌宠。

用真实猫照制作的 Windows 桌面陪伴样品：透明悬浮、自然眨眼、耳尖轻动、间歇摆尾、胸口呼吸，以及拖动和托盘操作。摸它时会慢慢闭眼，再睁开眼回应。图片由本地抠图得到，保留原照片中的脸型、毛色、身体和尾巴。

![猫咪桌宠预览](docs/pet-preview.png)

## 从源码编译

目标环境：Windows 10 / 11，64 位，已安装 .NET Framework 4.8。编译使用 Windows 自带的 .NET Framework C# 编译器，无需安装 Python、Node.js 或额外编译包。

在 PowerShell 中进入仓库目录，运行：

```powershell
.\build.ps1
```

生成文件位于 `发布包\猫咪桌宠.exe`，双击即可运行。图片和图标已嵌入 EXE，可以单独复制给朋友。

## 使用

- 左键按住猫咪拖动；轻点猫咪，会慢眨眼并显示短暂回应。
- 右键猫咪可选择大小、暂停全部动作、隐藏、回到右下角或退出。暂停会停在当前姿态；恢复后继续。
- 隐藏后，在任务栏右侧托盘找到猫咪图标，双击或选择“显示猫咪”恢复。
- 运行时不联网，不设置开机自启，不记录屏幕内容。重启后恢复默认位置和大小。

目前是同一张真实坐姿照片的局部动画：

- 随机间隔眨眼，偶尔轻动一只耳朵。
- 尾尖小幅摆动后停下来，脚掌和尾根保持稳定。
- 胸口微幅起伏；点击时给出较慢的闭眼回应。
- 隐藏时停止动作计时，重新显示后继续。

这版没有增加转身或行走；这些动作需要额外姿态素材。动作来自照片表面的局部变形，仍属于照片动画样品。

## 文件说明

| 文件 | 用途 |
|---|---|
| `src/CatPet.cs` | 独立 Windows 程序及自身验证入口 |
| `src/PhotoMotion.cs` | 原照局部动画、动作间隔和动作命中区域 |
| `src/app.manifest` | Windows 权限和显示缩放声明 |
| `assets/cat.png` | 透明猫咪图片 |
| `assets/cat.ico` | 程序图标 |
| `build.ps1` | 将代码和素材编译为一个 EXE |
| `tools/` | 可选的本地照片抠图脚本 |
| `requirements-image.txt` | 已验证的图片处理依赖版本 |
| `docs/verification.txt` | 本机样品验证记录 |

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

退出码 `0` 表示检查通过。当前本机记录为 35 项通过。检查覆盖透明图片、窗口、鼠标穿透、尺寸、点击反馈、暂停、托盘和隐藏恢复；还检查动作曲面不会翻折、脚掌像素保持稳定、摆尾后的命中区域，以及真实计时器驱动的慢眨眼。

检查会输出 `qa/pet-preview.png`、`qa/motion/` 下的 8 张动作对照图和 90 张连续动作帧。模拟的 30 秒动作序列用于检查连续性，不代表 30 秒实机试用或不同电脑的帧率表现。

这些检查不代替真实鼠标拖动、托盘点击、混合缩放多屏和其他电脑上的实机试用。

## 更换猫咪图片

这版动作坐标专门对应当前猫咪的 953 × 1347 透明图片。换猫或改变图片裁剪后，需要在 `src/PhotoMotion.cs` 中重新校准眼睛、耳朵、胸口和尾尖位置，不能只换图片就得到正确动作。PNG 应有真实透明背景、完整轮廓及少量边距。更新图片、图标与坐标后，再运行 `build.ps1`。

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

## 后续产品方向

网页可负责上传照片、确认形象、选择动作和配置功能；桌面程序负责悬浮、互动和本地提醒。下一步以猫主人的试用反馈为依据，再决定是否制作自助定制网站。