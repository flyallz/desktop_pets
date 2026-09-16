# v0.3 姿态素材

坐姿 `assets/cat.png` 仍是原照片的本地抠图。下面三份是内置图像工具参考原猫生成的衍生素材，不能视为这只猫真实拍摄的新照片。

| 文件 | 用途 |
|---|---|
| `assets/cat-stretch.png` | 伸懒腰时的身体和四肢姿态 |
| `assets/cat-rest.png` | 趴下时的身体、轮廓和睁眼表情 |
| `assets/cat-sleep.png` | 与趴下姿态对齐的闭眼参考；程序仅使用其眼睛区域 |

执行方式：使用内置 `image_gen`，未使用需要 API 密钥的 CLI。因本机文件读取环境错误，使用对话中显示的原猫透明缩略图作为身份参考。先生成睡觉姿态，再生成伸懒腰姿态，最后编辑睡觉图得到睁眼版本。

生成结果为带棋盘格的 RGB 图片，并非真正透明。随后沿用本项目的本地 IS-Net 抠图流程清理背景，保留主体连通区域并清空全透明像素的 RGB。三张最终图片统一为 953 × 1347 画布、底部基线 1291；醒来与睡觉共用同一裁剪和缩放位置。程序检查了嵌入素材的透明通道和隐藏背景像素。

动作包括局部伸展、呼吸，以及固定身体上的眼睛过渡。坐姿、伸展、趴下之间使用关键姿态切换，没有宣称是连续的起卧视频或三维骨骼动画。

## 实际使用的提示词

下列提示词对应三次成功的内置工具调用。引用顺序按每次调用中所述的图像顺序提供。

### 睡觉

```text
Use case: identity-preserve. Use the most recent inline transparent cat image shown in the conversation as the subject identity reference. It is a chubby silver-white long-haired cat with a round broad face, short small ears, faint gray forehead markings, gray-green eyes, pink-brown nose, large fluffy ruff, and a very bushy long tail. Produce ONE new full-body photorealistic sleeping pose of this SAME recognizable individual cat: naturally lying belly down with forepaws tucked forward, chin low near paws, eyes softly closed, fluffy tail curled beside the body. Match the reference cat's real proportions, face and silver-white fur, and retain the photographic appearance. Three-quarter frontal view at the cat's height, gentle neutral light. Entire cat visible with generous transparent margins. Genuine transparent RGBA background. No room, floor, props, text, watermark, cast shadow or extra animals. A single clean sprite for a Windows desktop companion, not a collage, not an illustration.
```

### 伸懒腰

```text
Use case: identity-preserve. Asset type: photorealistic transparent desktop-pet sprite. References: the original seated cat shown inline before the latest sleeping cat is the primary identity reference; the latest sleeping cat is a supporting reference for the same individual's fur and photographic styling. Create ONE full-body stretching pose of this SAME silver-white long-haired, round-faced, chubby adult cat. A natural feline forward stretch: rump raised slightly on bent hind legs at the left/back, front legs extended together toward the right/front, chest lowered between the forelegs, face relaxed and visible in three-quarter frontal view, eyes gently narrowed; thick fluffy tail relaxed curling alongside the left side, not vertical. Correct feline joints, four anatomically plausible paws, no exaggerated yoga posture. Keep the broad face, pink-brown nose, small ears, forehead gray markings, very thick fluffy fur and long bushy tail recognizable as this exact cat. Natural neutral lighting, photographic realism matching the references. All ears, paws and tail inside the canvas with 6 percent margin. Genuine transparent RGBA background, no checkerboard texture baked into the image, no room, floor, props, text, watermark, cast shadow or extra animals. Single pose asset, not a contact sheet.
```

### 醒来

```text
Use case: identity-preserve / precise-object-edit. Of the three most recent cat images, the first original seated cat is the identity reference, the second image showing the cat lying belly-down ASLEEP with tail curled on the left is the EDIT TARGET, and the third stretching cat is only an appearance reference. Make a matching AWAKE version of the second, lying-down image. Change ONLY the two eyes: gently open this cat's gray-green eyes into a calm half-awake expression. Preserve the exact sleeping image's full-body pose, chin height, four paws, face shape, ear locations, round plump body, tail curl, framing, every fur marking and photographic lighting. Do not raise the body or head and do not stretch. Same three-quarter frontal camera, same full cat silhouette and same empty margins. Exact same silver-white long-haired individual as the original photo, not a new animal. SINGLE clean full-body lying-awake sprite. Truly transparent background with alpha, no drawn checkerboard, room, floor, text, watermark or shadows.
```
