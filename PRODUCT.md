# Desktop Pets 产品约定

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

用户已授权由实现方选择技术：网站与桌面客户端共享网页动画和宠物包，分别打包 Windows、Mac Apple Silicon 和 Mac Intel。网页先直接实现可操作页面，先验证制作流程。现有 C# Windows 样品继续保留。具体前端与构建工具以 platform/README.md 的实装为准。

## Users

首批面向中国大陆个人养宠用户，希望把真实宠物带到工作电脑上。用户不需要懂代码或配置 AI 密钥。已有朋友的银白长毛猫样例。

## Product Purpose

让用户能创建、预览、调整、导出自己的桌宠，之后可在 Windows 和两种 Mac 架构上复用。产品需要具备可持续运营和商业化的发展路径。

## Positioning

保留自家宠物的辨识度，提供可配置的桌面陪伴；以真实照片、真实制作结果和实际可用的导入流程证明产品价值。

## Operating Context

先验证制作与桌面使用流程，再开通商业化服务。已知可用服务器配置为 4 核 4GB；暂未检查或部署。用户目前没有域名、经营主体和 Apple 开发者账号。

## Capabilities and Constraints

已有 Windows v0.4.1 样品、坐姿抠图、生成的伸懒腰/趴卧/睡眠姿态及 8 帧行走素材，已获用户公开授权。仅这些已授权素材可用作公开演示。原始房间照片不公开。未来用户上传的照片默认私有，公开展示必须另获授权。

当前样品动作坐标针对单只猫写死，尚不支持任意照片自动获得完整步态。网站不得用演示猫的动作冒充上传宠物的生成结果。AI 多姿态生成、支付、正式 Mac 签名均为待接入能力，不虚构成功状态、销量、评价、下载数或定价。

两种 Mac 架构均在目标范围；通用安装包并不代表支持所有旧 macOS。正式发布前需明确最低系统版本、完成架构构建与真机试用。

## Brand Commitments

工作名称为 Desktop Pets / 桌宠工坊；正式品牌和域名尚未确定。产品主角是真实宠物，界面用中文，操作说明面向非技术用户。

## Evidence on Hand

assets/cat.png、assets/cat-stretch.png、assets/cat-rest.png、assets/cat-sleep.png、assets/cat-walk-0.png 至 cat-walk-7.png；现有 src/ 与 docs/verification.txt；GitHub 仓库 flyallz/desktop_pets。没有已验证的商业收入、用户规模或留存数据。

## Product Principles

- 用户先看到自己的制作结果，再决定是否继续或付费。
- 桌面运行和基础陪伴优先本地完成，避免持续产生生成费用。
- 用户照片与配置可导出、可删除，默认不进入公开作品库。
- 沿用同一份宠物格式与核心行为，减少多平台分叉。
- 收费方案、生成成本和效果达标率需要小规模实测，不能以假设冒充经营事实。
