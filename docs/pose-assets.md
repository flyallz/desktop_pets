# v0.3 / v0.4 姿态素材

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


## v0.4 行走素材

新增 `assets/cat-walk-0.png` 至 `assets/cat-walk-7.png`，按顺序播放，循环时长约 1.12 秒。它们由内置 `image_gen` 参考原猫坐姿和既有衍生姿态生成，属于 AI 衍生素材。执行仍使用对话内的参考图，未使用需要 API 密钥的 CLI。

工具输出一张 1774 × 887 的 RGB 绿色背景图集，4 列 × 2 行。`tools/prepare_walk.py` 在本地去除绿色背景、保留猫咪主体并清理边缘；8 帧共用裁剪框 `(9, 118, 425, 355)`，统一缩放到 841 像素宽，放在 953 × 1347 透明画布的 `(56, 812)` 位置。所有帧共用坐标，避免逐张缩放引起忽大忽小。完全透明像素的 RGB 均清零。

程序向左行走时镜像同一套帧，并同步变换鼠标命中区域。步幅按猫咪显示尺寸缩放，保留 Windows 窗口位置取整后的余数，使距离不随每次取整累积偏差。默认启动约 6 秒后开始第一段行走；动作之间停留约 7～13 秒，自动打盹约 32 秒。手动睡觉不受自动轮换打断。

程序实际渲染的两方向帧、顺序、边缘转向、默认无人操作轮换和暂停等由自身检查验证；这不等于真实猫的运动捕捉。生成帧仍可能有少量毛发和四肢轮廓差异，坐下、起身和转向仍是关键姿态切换。

### 行走图集的实际提示词

```text
Use case: identity-preserve. Asset type: ONE production animation sprite-sheet atlas for a photorealistic Windows desktop pet. The reference shows two views of the SAME silver-white long-haired cat. Preserve that individual's round broad face, small upright ears, dark gray forehead markings, gray-green eyes, pink-brown nose, very thick neck ruff, chubby long-haired body, short sturdy legs and large fluffy tail. Create ONE image with exactly EIGHT equal rectangular cells arranged in a strict 4-column by 2-row grid, read left to right top to bottom. Each cell shows the same cat in a consecutive frame of a natural slow quadrupedal WALK CYCLE to the RIGHT. Near-side profile, face turned only slightly toward the viewer enough to recognize this cat. All four legs take alternating steps with distinct contact, passing, lift and opposite-contact phases; do not repeat the same static stance, do not hop or trot. Frame 8 should connect smoothly back to frame 1. Very important: the head, torso, fur markings, camera, cat scale and lighting stay identical across all 8 cells; only leg joint positions, slight shoulder weight transfer and a tiny relaxed tail sway change. Torso center, head center and common floor baseline at identical relative coordinates in every cell. Keep every complete cat, ears and tail inside its own cell with ample margin and clear separation from all adjacent cells. Body is horizontal, walking normally, not seated, not stretched, not sleeping. Tail is relaxed curving back toward the left without covering the paws. Photography with soft neutral light and fine fur detail, not a drawing, plush toy or 3D render. High-resolution wide atlas. Plain uniform bright green (#00FF00) background across all cells for precise local extraction; no checkerboards, floor, shadows, borders, gridlines, labels, numbers, text, watermarks or props.
```
