# 自己添加照片 · v0.4.4

这是 Windows 成品里的照片功能，直接在 EXE 中使用。自助网站与跨平台客户端仍是另一个目录里的独立内测版本。

## 给朋友的操作

1. 退出旧版，双击新版 EXE。
2. 右键猫咪 → 换造型 → 添加自己的照片。
3. 选择一张 JPG / PNG。普通照片会自动去背景；透明 PNG 可直接进入标记。
4. 点“放大”，在一只眼睛的两个眼角之间拖线，再画另一只。尾巴从根部拖到尖端；耳朵从根部拖到耳尖。不需要或看不到的部位跳过即可。
5. 眼睛的椭圆应盖住眼睛，尽量不要盖到鼻子；可调“眼睛张开高度”。尾巴可调粗细。
6. 点“看看它动起来”，满意后起名并保存。没标记也能保存，会轻轻呼吸。

背景有残留时选择“擦掉”并拖动，擦多了可用“恢复”或“撤销”。支持最近 5 笔撤销。“恢复”在刚导入时可找回抠掉的原图部分；保存后重新编辑时以已经保存的照片为底图。要重新找回原照中被抠掉的部分，请重新选择那张原照片。

自动抠图仍可能留下杂物或漏掉毛边，建议选择单只宠物、主体清晰的照片，保存前看一下轮廓。它不自动识别眼睛和尾巴，标记由主人完成。关闭编辑窗口会放弃未保存的修改。

## 保存与更新

照片、名字和动作标记会保留在当前 Windows 账户的 `%LOCALAPPDATA%\PhotoCat\photos`；每个 `.petphoto` 文件是一个造型。它包含透明 PNG 与标记，不再依赖原照片路径。把整个 `photos` 文件夹复制出来即可备份。

换新版 EXE 不会清空照片库。双击轮换包含自己的照片；右键“换造型”可直接选择、调整当前自选照片或删除它。删除不改动用户原照片，也不能删除内置造型。

自选照片原地呼吸、眨眼、轻动耳朵或摆尾，不会突然切成内置猫的走路、伸展和趴睡图。内置六种造型的活动继续保留。

## 离线处理

完整成品约 203 MB，含抠图工具和模型。首次处理时自动准备到 `%LOCALAPPDATA%\PhotoCat\tools`，之后不必重复准备。无需 Python、命令行、网页服务或 API Key，处理时没有照片上传。

聊天仍单独使用用户在“聊天设置”中填写的 DeepSeek API Key。照片不会附在聊天请求里。自选照片与个人设置不随源码或发布包上传。

## 开发构建

普通 C# 编译只需 Windows .NET Framework 4.8。要生成完整成品，先使用独立 Python 3.11 环境准备离线工具：

```powershell
python -m venv .build\cutout-env
.\.build\cutout-env\Scripts\python.exe -m pip install -r requirements-photo-tools.txt
.\.build\cutout-env\Scripts\python.exe -m pip install requests==2.32.3
.\.build\cutout-env\Scripts\python.exe tools\download_model.py
.\.build\cutout-env\Scripts\python.exe tools\build_photo_tools.py --model .build\models\isnet-general-use.onnx
.\build.ps1
```

上面的命令会安装模型下载脚本所需的 `requests`。也可使用已有且通过校验的模型路径，跳过下载。`build_photo_tools.py` 输出 `.build/photo-tools.zip` 与 SHA-256 文件，`build.ps1` 自动嵌入两者。模型与工具只进入完整 EXE，不提交到 Git 仓库。

来源和许可见 [照片工具说明](photo-tools-notices.md)。所有模型处理在独立、隐藏的 CPU 进程中执行；取消、超时或出错会终止该处理。默认输入最多 30 MB / 4000 万像素，读取后缩至长边不超过 1600 像素，保存到统一的 953 × 1347 透明画布。

## 验证范围

294 项本机检查包括原有功能以及照片导入、八种手机方向、可恢复编辑、真实离线去背景、取消和错误恢复、保存、重读、改名与删除。真实 WPF 窗口已经渲染检查。

自动点击工具本次无法启动，错误为 `windows sandbox failed: helper_unknown_error: setup refresh had errors`；未完成真实鼠标拖线和系统文件选择器点击验收。其他 Windows 电脑、混合缩放多屏和真实 DeepSeek 在线回复仍需相应环境与有效密钥实测。
