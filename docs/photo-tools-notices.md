# 离线照片工具来源

桌宠 v0.4.4 的完整 EXE 内置 Python 和 CPU 抠图工具。工具不访问网络；原照片通过进程内存管道传入，只返回带透明通道的 PNG。首次使用时解压到当前账户的 `PhotoCat/tools`，没有安装系统服务。

- 前景模型：Xuebin Qin 等人的 [IS-Net / DIS](https://github.com/xuebinqin/DIS)，原项目 [Apache 2.0 许可](https://github.com/xuebinqin/DIS/blob/main/LICENSE.md)。使用的是 [rembg 发布的 ONNX 文件](https://github.com/danielgatis/rembg/releases/download/v0.0.0/isnet-general-use.onnx)，模型字节未修改，MD5 `fc16ebd8b0c10d971d3513d564d01e29`。
- 输入标准化与输出蒙版参考 [rembg 的 IS-Net 实现](https://github.com/danielgatis/rembg/blob/main/rembg/sessions/dis_general_use.py)。本项目增加了本地进程通信、输入限制及通用透明边缘清理，没有复用针对示例猫毛色的清理规则。
- 执行依赖：Python 3.11、NumPy、Pillow、ONNX Runtime CPU。版本见 `requirements-photo-tools.txt`。
- 打包：PyInstaller 独立目录模式，随后连同模型嵌入 EXE。使用方式参考 [PyInstaller 官方文档](https://pyinstaller.org/en/stable/usage.html)。

工具缓存中的 `licenses/` 随附 Python、NumPy 及其内置库、Pillow、ONNX Runtime、protobuf、flatbuffers、PyInstaller 与 IS-Net 的许可和依赖声明，构建时从所用版本原文件复制。这个工具只负责去掉背景；它不识别猫咪眼睛或尾巴的位置，也不生成新的走路姿态。
