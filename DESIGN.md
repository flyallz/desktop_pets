---
name: "桌宠工坊 / Desktop Pets"
description: "以真实宠物照片为主角的本地制作展台"
colors:
  primary: "#244a46"
  primary-hover: "#193a36"
  ink: "#18282a"
  muted: "#586969"
  page: "#f4f6f6"
  white: "#fff"
  surface: "#edf0f1"
  line: "#dce2e2"
  control-line: "#ccd6d5"
  field-line: "#ced8d4"
  control-hover: "#e7edeb"
  focus: "#548c80"
typography:
  display:
    fontFamily: '"Pet Exhibition", "Noto Serif SC", serif'
    fontSize: "30px"
    fontWeight: 600
    lineHeight: 1.4
    letterSpacing: "-0.03em"
  brand:
    fontFamily: '"Pet Exhibition", "Noto Serif SC", serif'
    fontSize: "19px"
    fontWeight: 600
  title:
    fontFamily: '"PingFang SC", "Microsoft YaHei UI", "Microsoft YaHei", sans-serif'
    fontSize: "17px"
    fontWeight: 600
    lineHeight: 1.55
  body:
    fontFamily: '"PingFang SC", "Microsoft YaHei UI", "Microsoft YaHei", sans-serif'
    fontSize: "13px"
    fontWeight: 400
    lineHeight: 1.8
  label:
    fontFamily: '"PingFang SC", "Microsoft YaHei UI", "Microsoft YaHei", sans-serif'
    fontSize: "12px"
    fontWeight: 400
  helper:
    fontFamily: '"PingFang SC", "Microsoft YaHei UI", "Microsoft YaHei", sans-serif'
    fontSize: "12px"
    fontWeight: 400
    lineHeight: 1.8
rounded:
  tag: "4px"
  photo: "5px"
  field: "6px"
  control: "7px"
spacing:
  tight: "8px"
  compact: "12px"
  regular: "16px"
  group: "18px"
  section: "20px"
components:
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.white}"
    rounded: "{rounded.control}"
    padding: "9px 13px"
  button-primary-hover:
    backgroundColor: "{colors.primary-hover}"
  button-secondary:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "9px 13px"
  button-secondary-hover:
    backgroundColor: "{colors.control-hover}"
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.control}"
    padding: "9px 13px"
  link-action:
    backgroundColor: "transparent"
    textColor: "{colors.primary}"
    typography: "{typography.label}"
    padding: "0"
  field-text:
    backgroundColor: "{colors.white}"
    textColor: "{colors.ink}"
    rounded: "{rounded.field}"
    padding: "7px 10px"
    height: "39px"
  photo-upload:
    backgroundColor: "#f6f9f7"
    textColor: "{colors.ink}"
    rounded: "8px"
    height: "146px"
    width: "100%"
  action-photo:
    backgroundColor: "#f7f8f7"
    rounded: "{rounded.photo}"
    height: "89px"
  action-photo-selected:
    backgroundColor: "#edf3ee"
---

# Design System: 桌宠工坊

## Overview

**Creative North Star: "照片展台"**

照片展台把真实宠物留在画面中心。冷灰背景承接毛发和透明边缘，白色工具区与松绿色操作退在周围；标题像展签，动作照片像一排可试播的接触印样。

界面以中文短句、直接的控件和清楚的状态说明支持制作。品牌与主标题使用有笔画性格的宋体，操作文字使用系统无衬线体。结构保持平整，只有宠物和必要的处理状态运动。

**Key Characteristics:**

- 真实照片承担主体与结果证明。
- 冷灰、白色和单一松绿强调色。
- 宋体标题配合紧凑、清楚的中文操作文字。
- 平整工作面、细边线和轻微圆角。

本记录依据已完成的网页源代码与桌面、移动端、1024 像素宽度复核图。页面策略见 `.impeccable/surfaces/platform-src-app-jsx.md`，产品能力以 `PRODUCT.md` 为准。

## Colors

以冷灰与白色承接真实毛色，松绿给出操作和选择的方向。前置 token 是颜色值的唯一规范层。

### Primary

- **松绿**：保存、移动端上传、当前步骤、开关与品牌标记。
- **深松绿**：主按钮悬停，增加反馈而不移动控件。

### Neutral

- **深墨色**：正文、标题与控件文字。
- **灰绿文字**：帮助说明和次要状态，维持清楚的层级。
- **浅冷灰页面 / 雾灰展台 / 白色工具面**：形成平整的三层空间，照片是视觉主体。
- **分隔线 / 控件线 / 字段线**：区分区域和可操作边界。
- **淡灰绿悬停 / 焦点绿**：鼠标与键盘反馈；焦点轮廓不可由颜色变化单独替代。

**The Working Green Rule.** 松绿同时标识可执行动作、当前选择和品牌；不把它铺满照片展台。

## Typography

**Display Font:** 自托管 Noto Serif SC，项目内使用名称 `Pet Exhibition`，后备为 Noto Serif SC 与 serif。
**Body Font:** PingFang SC、Microsoft YaHei UI、Microsoft YaHei、sans-serif。

**Character:** 宋体仅用于品牌和页面主标题，像照片展签；系统无衬线体承担步骤、表单和说明。没有独立等宽字体角色，也没有装饰性字距标签。

### Hierarchy

- **Display**：主标题使用前置 display token；在紧凑桌面收至（27px），移动端收至（23px）。
- **Brand**：桌面使用前置 brand token，移动端为（16px）。
- **Title**：工具区标题使用 title；小组标题为（14px、600）。桌面使用说明标题为（20px），移动端为（18px）。
- **Body / Helper**：正文与短说明按内容角色选择；帮助段落上限为（70ch），无需增加文字密度。
- **Label**：步骤、字段和动作名称紧邻各自对象。当前页面还有小型状态与照片脚注，它们的尺寸属于本页记录，不构成全站最小字号规则。

字体文件位于 `platform/src/assets/noto-serif-sc-title.ttf`，许可位于 `platform/public/fonts/OFL.txt`。这是当前标题使用的字体子集；扩充品牌或标题文案时先核对字形覆盖并保留许可。

**The Photo First Rule.** 照片和动作帧必须保留原有轮廓与比例；文字解释状态，不能用装饰图形替代实际结果。

## Layout

系统采用“工具在侧、对象居中、状态贴近对象”的工作面。页面容器上限为（1680px），默认左右留白（48px）；宽屏为（70px），紧凑桌面为（28px），移动端主体为（16px）。工具内容保持紧凑，展台保留宽松留白。

当前制作页在桌面使用左侧固定工具栏与可伸缩展台，动作接触印样在展台下方。工具栏按宽屏、默认、紧凑桌面分别为（320px / 290px / 260px）。这些是制作页的尺寸记录，不是每个未来页面都必须复制的布局。

在（760px）及以下，内容改为单列：步骤、展台、工具内容、保存提示。独立上传按钮留在展台上方，用户无需向下寻找上传入口。帮助链接使用独立行的右侧位置，避免挤压主标题。动作图仍保留一行四项；使用说明改为纵向顺序。

紧凑桌面断点为（1100px），宽屏断点为（1500px）。常用间距来自前置 spacing token；区域分隔依靠留白与细线，不需要额外的悬浮容器。

透明桌面伴侣窗口有独立样式边界：根背景透明，展台高度始终跟随窗口，不继承移动网页的固定高度、衬底与圆角。

## Elevation & Depth

默认表面平整。页面、工具与展台依靠色阶及细边线分层，不使用工作区投影。短暂提示与宠物气泡可以悬浮，其阴影只说明覆盖关系。

### Shadow Vocabulary

- **宠物气泡**：`0 7px 22px #18282a13`，表达来自宠物的短暂消息。
- **状态提示**：`0 8px 30px #18282a24`，用于底部处理、完成与错误反馈。

**The Flat Workbench Rule.** 制作区域依靠底色与细边线分隔；阴影只用于短暂浮在内容上方的反馈。

## Shapes

主要控件是轻微圆角矩形。字段、预览和编辑区共用柔和但明确的边缘；照片缩略框使用更小的圆角，整体工作区使用（14px）外圆角。该外框是当前大工作面的尺度，不能逐层套在每个子区域。

圆形仅承担已有语义：步骤序号、背景色样、状态点、播放标记与开关滑块。上传区的虚线边框表达可投放照片。图标为简洁的描边 SVG，默认笔画（1.65），不用文字字符模拟图标。

## Components

### Buttons

直接、稳定，尺寸服从操作密度。

- **Primary**：松绿底白字，默认最小高度（40px）；桌面用于保存，移动端也用于首屏上传。
- **Secondary / Quiet / Link**：白底细边按钮、透明安静按钮、绿色文字操作分别用于工具、低强调操作与流程衔接。
- **Hover / Focus**：背景与文字以（0.18s）过渡；键盘焦点使用（3px）轮廓与（3px）外偏移。点击不伴随位移。
- **Disabled**：降低透明度至（0.48），保留按钮位置并由邻近说明交代不可用原因。

### Inputs / Fields

白底、细边、小圆角；标签在字段上方。文本与选择字段共享前置 field-text token。滑块使用松绿强调色，开关以位置与颜色同时表示开关状态；开关位移时间为（0.16s）。

### Navigation

制作步骤是小型顺序导航，圆形序号与中文名称并用。当前步骤用松绿填充序号、加深标签；桌面上下排列，移动端并排排列。导航不依赖装饰性悬停运动。

### Chips

“示例猫咪 / 我的桌宠”与内测标签是小型状态说明。它们贴近对象或品牌，不承担新的内容层级，也不应变成每个区块的眉题。

### Cards / Containers

工作面采用白色背景、细边线和内部留白。照片上传框是明确的投放目标。动作图块保留照片及两行简短说明：选中时改变边线和底色，并出现播放标记；缺少素材时显示图片占位符与“待添加图片”，不显示伪造的动作预览。

### Real-pet Stage

真实图像在同一展台上试播，控制区域保持静止。拖动改变预览位置，点击触发短暂回应，回中按钮恢复位置。背景色样改变预览衬底，不改写宠物素材。

编辑时背景修整界面替换原展台，占据同一空间，保留返回、画笔大小、撤销与保存操作。处理状态必须显示为处理状态，不能直接跳到成功图示。减少动态效果的系统偏好会停用 CSS 动效，并让宠物初次载入时处于暂停状态。

## Do's and Don'ts

### Do:

- Do 让真实宠物图像拥有足够留白，并以完整轮廓展示。
- Do 用文字、边线与选中标记共同说明状态，保留键盘焦点轮廓。
- Do 为操作提供中文名称；图标使用描边 SVG，单独出现时提供可访问名称。
- Do 保持控件位置稳定；减少动态效果的系统偏好应同时影响界面与宠物初始播放状态。

### Don't:

- Don't 把说明标签放大成新的装饰性眉题，或让每个工具组都拥有一层浮动卡片。
- Don't 用阴影、光晕或大面积强调色抢走宠物照片的注意力。
- Don't 把当前页面的小字号脚注、演示猫姿态或标题字体子集当作新页面的通用能力。


### Pet action and appearance controls

点击宠物出现非模态小菜单，沿用白底、松绿选择状态和短中文操作名称。菜单只在操作时覆盖展台，选定动作或造型后关闭；拖动保持移动语义。菜单最大宽度适应窄屏与 400 像素桌面窗口，超出高度时在菜单内滚动。

配饰位置调整是暂时的展台工具，四个滑块以两列排列，底部提供取消和完成。调整只影响当前姿势；原照片保持原样。恢复原样、保存失败与仅有单一姿势均有明确路径。键盘可从猫咪进入菜单并用 Esc 返回。
