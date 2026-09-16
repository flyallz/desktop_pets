# v0.4.2 原照片造型

这次换造型使用主人提供的不同原照片，不是配饰叠加或重新生成猫的姿态。

| 菜单名称 | 素材 |
|---|---|
| 原版坐姿 | `assets/cat.png`，保留原版局部动作 |
| 红毛衣 | `assets/looks/sweater.png` |
| 歪头看你 | `assets/looks/tilt.png` |
| 乖乖侧坐 | `assets/looks/sofa.png` |
| 露肚皮睡觉 | `assets/looks/belly.png` |
| 毛茸茸的背影 | `assets/looks/back.png` |

五个新造型均从主人原图中本地提取透明背景，保留原始照片颜色，仅裁剪和缩放到 953 × 1347 画布。红毛衣图额外修正背景玩偶、后腿毛衣和前爪的分割；相关坐标写在 `tools/make_photo_looks.py`。原图、分割模型及包含房间背景的检查图只在被忽略的本地构建目录中保存。`photo-looks-provenance.json` 记录每张使用照片的文件名、SHA-256 与裁剪信息。

歪头、露肚皮和背影照片本来就有部分身体或尾巴超出画面，造型保留原有裁切。另两张候选的主体分割残缺较明显，未装入成品。

双击或右键选择照片会结束当前动作并显示新造型，自动活动开关保持原样。原有行走、伸展与睡觉动画继续使用 v0.4 的动作图片；动作结束后回到选中的静止照片。新照片没有套用原版脸部坐标，避免眨眼或耳朵变形落在错误位置。此版没有为每一张衣服照片制作对应的整套行走动画。

开发者可用现有素材直接编译，无需重新抠图。如果持有原照片及同版本 IS-Net 运行库，可运行：

```powershell
python tools/make_photo_looks.py --photos "C:\path\to\photos" --runtime ".build"
.\build.ps1
```

工具为这组照片校准，并非网站的通用抠图接口。输出 `assets/looks/`，私人对照图写入 `.build/photo-looks/`。
