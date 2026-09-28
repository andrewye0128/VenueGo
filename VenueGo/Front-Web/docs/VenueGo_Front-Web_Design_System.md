# VenueGo 前台 Figma Design System v1

> **Version:** v1.0  
> **Project:** VenueGo  
> **Scope:** Frontend / Figma / Vue.js / Tailwind CSS  
> **Purpose:** 建立一致、可維護、可重複使用的前台設計規範，並作為 Figma 與前端開發的共同依據。

---

# 目錄

1. [文件目的](#1-文件目的)
2. [Design System 架構](#2-design-system-架構)
3. [Design Principles 設計原則](#3-design-principles-設計原則)
4. [Foundations 基礎規範](#4-foundations-基礎規範)
   - [Color 色彩系統](#41-color-色彩系統)
   - [Typography 字體系統](#42-typography-字體系統)
   - [Spacing 間距系統](#43-spacing-間距系統)
   - [Radius 圓角系統](#44-radius-圓角系統)
   - [Border 邊框系統](#45-border-邊框系統)
   - [Shadow 陰影規範](#46-shadow-陰影規範)
5. [Components 元件規範](#5-components-元件規範)
   - [Button](#51-button)
   - [StatusBadge](#52-statusbadge)
   - [SlotChip](#53-slotchip)
   - [AlertBanner](#54-alertbanner)
   - [Form Controls](#55-form-controls)
6. [Patterns 功能模式](#6-patterns-功能模式)
7. [Layout 版面規範](#7-layout-版面規範)
8. [Figma Variables 與命名規範](#8-figma-variables-與命名規範)
9. [Figma 檔案結構](#9-figma-檔案結構)
10. [Figma 與 Vue / Tailwind 的對應](#10-figma-與-vue--tailwind-的對應)
11. [團隊協作規則](#11-團隊協作規則)
12. [Design System v1 範圍](#12-design-system-v1-範圍)
13. [後續版本規劃](#13-後續版本規劃)
14. [總結](#14-總結)

---

# 1. 文件目的

VenueGo 前台即將使用：

- **Figma**：設計與 Design System
- **Vue.js**：前端框架
- **Tailwind CSS**：樣式系統
- **C# Web API**：後端 API

在正式進入前台開發前，先建立 Design System，讓團隊在設計與程式開發上使用同一套規則。

## 建立 Design System 的目的

- 統一 UI 視覺風格
- 統一顏色、字體、間距、圓角與邊框
- 統一互動元件的狀態
- 降低重複設計
- 方便 Figma 元件重複使用
- 方便 Vue Component 化
- 讓設計稿與實際程式畫面更一致
- 提升團隊協作效率
- 降低後續維護成本

---

# 2. Design System 架構

VenueGo Design System v1 分成四個主要層級：

## 2.1 Foundations

最基礎的視覺規範。

包含：

- Color
- Typography
- Spacing
- Radius
- Border
- Shadow

## 2.2 Components

可重複使用的 UI 元件。

包含：

- Button
- StatusBadge
- SlotChip
- AlertBanner
- Input
- Select
- DatePicker
- Checkbox
- Radio
- Textarea
- SearchInput

## 2.3 Patterns

由多個 Components 組成的功能區塊。

例如：

- Search Bar
- Venue Card
- Time Slot Grid
- Booking Summary
- Reservation Stepper
- Price Summary
- Payment Method
- Empty State

## 2.4 Layout

整體頁面結構與響應式設計。

例如：

- Header
- Footer
- Container
- Section
- Desktop
- Tablet
- Mobile

---

# 3. Design Principles 設計原則

VenueGo 的前台 UI 應遵守以下原則。

## 3.1 Consistent 一致

相同功能應使用相同元件。

例如：

- 所有主要預約按鈕使用同一套 Button
- 所有時段狀態使用 SlotChip
- 所有系統訊息使用 AlertBanner
- 所有狀態標籤使用 StatusBadge

---

## 3.2 Clear 清楚

使用者應能快速理解：

- 我現在在哪裡？
- 我可以做什麼？
- 下一步是什麼？
- 目前操作是否成功？
- 是否還有未完成事項？

---

## 3.3 Functional 功能優先

VenueGo 是運動場館預約平台，因此 UI 的主要任務是：

> 找到場地 → 選擇日期 → 選擇時段 → 確認預約 → 完成預約

設計應優先支援任務完成，而非單純追求裝飾。

---

## 3.4 Accessible 可辨識

狀態不能只依賴顏色。

建議搭配：

- 文字
- Icon
- 邊框
- 標籤
- 背景色

例如：

- 成功：綠色 + 成功文字 + Check Icon
- 錯誤：紅色 + 錯誤文字 + Error Icon

---

## 3.5 Reusable 可重複使用

優先使用：

- Variable
- Component
- Variant
- Auto Layout

避免每個頁面重新繪製相同 UI。

---

# 4. Foundations 基礎規範

---

# 4.1 Color 色彩系統

VenueGo 色彩分成三大類：

1. Brand
2. Neutral
3. Semantic

---

## 4.1.1 Brand Colors

### Brand / Primary

- 色碼：`#1D5FAF`
- 中文：品牌色

用途：

- 品牌識別
- Header
- 主要按鈕
- 選取狀態
- 重要導覽
- 主要互動元素

```text
Token:
Color / Brand / Primary

Value:
#1D5FAF
```

---

### Brand / Action

- 色碼：`#C24E17`
- 中文：行動色

用途：

- 立即預約
- 查詢空場
- 立即付款
- 重要 CTA

```text
Token:
Color / Brand / Action

Value:
#C24E17
```

> Action 色只使用在真正推動使用者完成主要任務的操作。

---

### Brand / Background

- 色碼：`#F3F5F8`
- 中文：背景色

用途：

- Page Background
- Section Background
- 輕量資訊區塊

```text
Token:
Color / Brand / Background

Value:
#F3F5F8
```

---

### Brand / Dark

- 色碼：`#0D233E`（由設計稿圖片量測，待 Figma 確認正式色碼）
- 中文：深色背景色
- Tailwind：`brand-dark`

用途：

- Footer 背景
- 深色區塊背景

深色背景上的文字與分隔線，使用 Neutral / Surface（`#FFFFFF`）加透明度：

- 標題：`#FFFFFF` 100%
- 內文、連結：`#FFFFFF` 75%
- 次要文字（版權）：`#FFFFFF` 60%
- 分隔線：`#FFFFFF` 10%

```text
Token:
Color / Brand / Dark

Value:
#0D233E
```

---

### Brand / Accent

- 色碼：`#F03010`（由設計稿圖片量測，待 Figma 確認正式色碼）
- 中文：強調色
- Tailwind：`brand-accent`

用途：

- 運動類型圖示
- 需要吸引目光的小型圖示

```text
Token:
Color / Brand / Accent

Value:
#F03010
```

> Accent 色只用於小面積的圖示，不用於按鈕或大面積背景，避免與 Brand / Action（`#C24E17`）混淆。

---

## 4.1.2 Neutral Colors

### Neutral / Surface

- 色碼：`#FFFFFF`

用途：

- Card
- Form
- Modal
- Content Area

```text
Color / Neutral / Surface
#FFFFFF
```

---

### Neutral / Border

- 色碼：`#D3DAE3`

用途：

- Input Border
- Divider
- Card Border
- Slot Border

```text
Color / Neutral / Border
#D3DAE3
```

---

### Neutral / Text / Primary

- 色碼：`#1A2230`

用途：

- 標題
- 主要內文
- 重要資訊

```text
Color / Neutral / Text / Primary
#1A2230
```

---

### Neutral / Text / Secondary

- 色碼：`#4E5A6A`

用途：

- 說明文字
- 日期
- Metadata
- 次要資訊

```text
Color / Neutral / Text / Secondary
#4E5A6A
```

---

## 4.1.3 Semantic Colors

### Semantic / Success

- 色碼：`#1D6B3C`

用途：

- 已付款
- 預約完成
- 操作成功

```text
Color / Semantic / Success
#1D6B3C
```

---

### Semantic / Warning

- 色碼：`#8A5300`

用途：

- 待付款
- 付款期限
- 場地提醒
- 注意訊息

```text
Color / Semantic / Warning
#8A5300
```

---

### Semantic / Error

- 色碼：`#B42318`

用途：

- 已取消
- 付款失敗
- 系統錯誤
- 驗證失敗

```text
Color / Semantic / Error
#B42318
```

---

### Semantic / Peak

- 色碼：`#8C4A00`

用途：

- 尖峰時段
- 尖峰價格

```text
Color / Semantic / Peak
#8C4A00
```

---

## 4.1.4 Color Naming Rule

### Do

```text
Brand / Primary
Brand / Action
Neutral / Border
Neutral / Text / Primary
Semantic / Success
Semantic / Warning
Semantic / Error
```

### Don't

```text
Blue
Blue02
DarkBlue
Green
Red
Gray01
Orange01
```

### 原則

應以「用途」命名，而不是單純以顏色外觀命名。

---

# 4.2 Typography 字體系統

## 4.2.1 Font Family

主要字型：

```text
Noto Sans TC
```

適用於：

- 標題
- 內文
- Button
- Label
- Form
- Badge
- Alert

---

## 4.2.2 Typography Scale

### Heading / H1

```text
Font Size: 24px
Font Weight: 700
Line Height: 32px
```

用途：

- 頁面主標題

---

### Heading / H2

```text
Font Size: 20px
Font Weight: 700
Line Height: 28px
```

用途：

- Section Title

---

### Heading / H3

```text
Font Size: 18px
Font Weight: 600
Line Height: 26px
```

用途：

- Card Title
- 小節標題

---

### Body / Large

```text
Font Size: 16px
Font Weight: 400
Line Height: 24px
```

---

### Body / Medium

```text
Font Size: 14px
Font Weight: 400
Line Height: 22px
```

一般內文預設使用。

---

### Body / Small

```text
Font Size: 12px
Font Weight: 400
Line Height: 18px
```

用途：

- 輔助說明
- Metadata
- 小型資訊

---

### Label / Medium

```text
Font Size: 14px
Font Weight: 500
Line Height: 20px
```

用途：

- Input Label
- Form Label
- Filter Label

---

### Label / Small

```text
Font Size: 12px
Font Weight: 500
Line Height: 18px
```

用途：

- Badge
- 小型輔助資訊

---

### Button / Medium

```text
Font Size: 14px
Font Weight: 600
Line Height: 20px
```

---

## 4.2.3 Numeric

時間、日期、金額建議使用：

```text
Tabular Numbers
```

例如：

```text
08:00
10:00
19:00
2026/09/25
NT$ 500
NT$ 3,800
```

目的：

- 數字寬度一致
- 避免時間、金額跳動
- 表格對齊更整齊

---

# 4.3 Spacing 間距系統

VenueGo 採用：

```text
4px Grid System
```

## Spacing Tokens

| Token        | Value |
| ------------ | ----: |
| Spacing / 1  |   4px |
| Spacing / 2  |   8px |
| Spacing / 3  |  12px |
| Spacing / 4  |  16px |
| Spacing / 5  |  20px |
| Spacing / 6  |  24px |
| Spacing / 8  |  32px |
| Spacing / 10 |  40px |
| Spacing / 12 |  48px |

---

## Usage

### Component Padding

建議：

```text
8px
12px
16px
```

### Component Gap

建議：

```text
8px
12px
16px
```

### Section Gap

建議：

```text
24px
32px
```

### Page Section

建議：

```text
40px
48px
```

---

# 4.4 Radius 圓角系統

VenueGo 使用較俐落的 UI 風格。

| Token       | Value | 用途                      |
| ----------- | ----: | ------------------------- |
| Radius / XS |   4px | Button / Input / SlotChip |
| Radius / SM |   6px | Badge / Small Card        |
| Radius / MD |   8px | Card / Alert / Modal      |

---

# 4.5 Border 邊框系統

預設 Border：

```text
1px solid #D3DAE3
```

Token：

```text
Border / Default
```

用途：

- Card
- Input
- Select
- SlotChip
- Alert
- Divider

---

# 4.6 Shadow 陰影規範

VenueGo v1 原則：

```text
Shadow / None
```

不使用：

- Drop Shadow
- Floating Shadow
- Heavy Box Shadow

改用以下方式建立層次：

- Border
- Background
- Spacing
- Typography Hierarchy

---

# 5. Components 元件規範

---

# 5.1 Button

Button 用於觸發操作。

## Component Name

```text
Button
```

## Properties

```text
Type
State
Size
Icon
```

---

## 5.1.1 Type

### Action

用途：

- 立即預約
- 查詢空場
- 立即付款

Style：

```text
Background:
Brand / Action

Text:
#FFFFFF

Radius:
4px
```

---

### Primary

用途：

- 確認
- 下一步
- 儲存

Style：

```text
Background:
Brand / Primary

Text:
#FFFFFF
```

---

### Secondary

用途：

- 返回
- 修改
- 重新選擇

Style：

```text
Background:
#FFFFFF

Border:
Brand / Primary

Text:
Brand / Primary
```

---

### Text

用途：

- 查看全部
- 了解更多
- 輕量操作

Style：

```text
Background:
Transparent

Text:
Brand / Primary
```

---

## 5.1.2 State

```text
Default
Hover
Pressed
Disabled
```

Disabled：

- 淺灰底
- 灰色文字
- 不可操作
- 不使用品牌色

---

## 5.1.3 Size

```text
Small
Medium
Large
```

---

## 5.1.4 Button Naming

不要建立：

```text
Blue Button
Orange Button
Gray Button
Disabled Button
```

應建立單一：

```text
Button
```

再透過 Variant 控制：

```text
Type = Action
State = Default
Size = Medium
```

---

# 5.2 StatusBadge

StatusBadge 用於顯示簡短狀態。

## Component Name

```text
StatusBadge
```

## Property

```text
Status
```

## Variants

### Info

例如：

```text
公告
```

---

### Peak

例如：

```text
尖峰
```

---

### Success

例如：

```text
已付款
```

---

### Warning

例如：

```text
待付款
```

---

### Error

例如：

```text
已取消
```

---

## 使用原則

- 文案簡短
- 單一 Badge 表示單一狀態
- 顏色對應語意
- 不在不同頁面任意改色

---

# 5.3 SlotChip

SlotChip 是 VenueGo 預約流程的核心元件。

## Component Name

```text
SlotChip
```

## Properties

```text
State
Peak
```

---

## 5.3.1 State

### Available

範例：

```text
10:00
```

Style：

```text
Background:
White

Border:
Neutral / Border

Text:
Neutral / Text / Primary
```

---

### Selected

範例：

```text
✓ 20:00
```

Style：

```text
Background:
Brand / Primary

Text:
White
```

---

### Reserved

範例：

```text
已預約
```

意義：

- 已被占用
- 不可點擊

---

### Closed

範例：

```text
不開放
```

意義：

- 場館未開放
- 不可點擊

---

### Past

範例：

```text
08:00
```

意義：

- 已過去
- 不可點擊
- 使用低對比樣式

---

## 5.3.2 Peak

尖峰不是獨立 State，而是附加 Property。

```text
Peak = True / False
```

例如：

```text
State = Available
Peak = True
```

畫面：

```text
19:00  尖峰
```

---

## SlotChip Principle

不同狀態不應只靠顏色判斷。

應搭配：

- 文字
- 邊框
- Icon / Check
- 背景
- Disabled 視覺

---

# 5.4 AlertBanner

AlertBanner 用於：

- 系統提醒
- 成功通知
- 注意訊息
- 錯誤訊息

## Component Name

```text
AlertBanner
```

## Property

```text
Type
```

## Structure

```text
AlertBanner
├─ Icon
└─ Content
   ├─ Title
   └─ Description
```

---

## 5.4.1 Info

範例：

```text
提醒
明天 19:00 有一筆預約，請攜帶 QR Code 報到。
```

---

## 5.4.2 Success

範例：

```text
成功
預約已建立。
```

---

## 5.4.3 Warning

範例：

```text
注意
請於付款期限前完成付款。
```

---

## 5.4.4 Error

範例：

```text
錯誤
此時段剛被預約，請重新選擇。
```

---

# 5.5 Form Controls

VenueGo v1 建議建立：

```text
Input
Select
DatePicker
Textarea
Checkbox
Radio
SearchInput
```

---

## Form State

所有表單元件至少支援：

```text
Default
Hover
Focus
Filled
Error
Disabled
```

---

## 5.5.1 Input

結構：

```text
Label
Input Field
Helper / Error Text
```

範例：

```text
會員姓名

請輸入會員姓名

此欄位為必填
```

---

## 5.5.2 Select

結構：

```text
Label
Selected Value
Chevron
```

範例：

```text
運動類型

籃球 ▼
```

---

## 5.5.3 DatePicker

用途：

- 場地預約日期
- 查詢日期
- 會員資料日期

---

## 5.5.4 Checkbox

用途：

- 同意服務條款
- 接收通知
- 多選設定

---

## 5.5.5 Radio

用途：

- 付款方式
- 單一選項
- 使用類型

---

## 5.5.6 Textarea

用途：

- 備註
- 取消原因
- 活動說明

---

## 5.5.7 SearchInput

用途：

- 搜尋場地
- 搜尋活動
- 搜尋訂單

---

# 6. Patterns 功能模式

Pattern 是由多個 Components 組成的常見功能區塊。

VenueGo 建議建立：

- Search Bar
- Venue Card
- Time Slot Grid
- Booking Summary
- Reservation Stepper
- Price Summary
- Payment Method
- Empty State

---

## 6.1 Search Bar

建議包含：

- SearchInput
- DatePicker
- Sport Type
- Action Button

用途：

- 首頁快速查詢
- 場地搜尋
- 預約頁篩選

---

## 6.2 Venue Card

建議包含：

- 場地圖片
- 場地名稱
- 運動類型
- 地點
- 評分
- 價格
- 收藏
- CTA

---

## 6.3 Time Slot Grid

所有時段應重複使用：

```text
SlotChip
```

不要另外畫一套時段 Button。

---

## 6.4 Booking Summary

建議包含：

```text
場館
運動類型
日期
時間
價格
付款方式
預約總額
```

---

## 6.5 Reservation Stepper

建議流程：

```text
1 選擇運動
2 選擇場地
3 選擇日期
4 選擇時段
5 確認資訊
6 付款
7 完成
```

---

# 7. Layout 版面規範

## 7.1 頁面結構

```text
Header

Main
├─ Hero / Page Header
├─ Section
├─ Section
└─ Section

Footer
```

---

## 7.2 Responsive

建議至少考慮：

```text
Desktop
Tablet
Mobile
```

常見斷點：

```text
Desktop ≥ 1280px
Tablet ≈ 768px - 1279px
Mobile < 768px
```

> 實際 Tailwind breakpoint 可依專案規格調整。

---

## 7.3 Desktop

建議：

- Header 橫向排列
- Search Bar 同列
- Card Grid 多欄
- Footer 多欄資訊

---

## 7.4 Mobile

建議：

- Search Bar 改縱向
- Card 改單欄
- Header 收合為 Mobile Navigation
- Button 優先 Full Width
- SlotChip 使用可換行 Grid

---

# 8. Figma Variables 與命名規範

---

## 8.1 Colors

```text
Color / Brand / Primary
Color / Brand / Action
Color / Brand / Background
Color / Brand / Dark
Color / Brand / Accent

Color / Neutral / Surface
Color / Neutral / Border
Color / Neutral / Text / Primary
Color / Neutral / Text / Secondary

Color / Semantic / Success
Color / Semantic / Warning
Color / Semantic / Error
Color / Semantic / Peak
```

---

## 8.2 Typography

```text
Typography / Heading / H1
Typography / Heading / H2
Typography / Heading / H3

Typography / Body / Large
Typography / Body / Medium
Typography / Body / Small

Typography / Label / Medium
Typography / Label / Small

Typography / Button / Medium
```

---

## 8.3 Radius

```text
Radius / XS
Radius / SM
Radius / MD
```

---

## 8.4 Spacing

```text
Spacing / 1
Spacing / 2
Spacing / 3
Spacing / 4
Spacing / 5
Spacing / 6
Spacing / 8
Spacing / 10
Spacing / 12
```

---

## 8.5 Components

```text
Button
StatusBadge
SlotChip
AlertBanner
Input
Select
DatePicker
Textarea
Checkbox
Radio
SearchInput
```

---

# 9. Figma 檔案結構

建議 Figma Pages：

```text
00 — Cover

01 — Design System

02 — Home

03 — Reservation

04 — Payment

05 — Member Center

06 — Information

99 — Playground
```

---

## 9.1 01 — Design System

放：

- Foundations
- Components
- Patterns
- Layout
- Variables
- Naming Rules

---

## 9.2 99 — Playground

用於：

- 新元件測試
- 顏色測試
- 版型測試
- 尚未確認的 UI
- 不成熟的草稿

避免直接污染正式 Design System。

---

# 10. Figma 與 Vue / Tailwind 的對應

Design System 不只服務 Figma，也要能對應程式。

---

## 10.1 Color Token 對應

Figma：

```text
Brand / Action
```

Tailwind：

```text
brand-action
```

---

Figma：

```text
Semantic / Success
```

Tailwind：

```text
semantic-success
```

---

## 10.2 Component 對應

Figma：

```text
Button
```

Vue：

```text
BaseButton.vue
```

---

Figma：

```text
StatusBadge
```

Vue：

```text
StatusBadge.vue
```

---

Figma：

```text
SlotChip
```

Vue：

```text
SlotChip.vue
```

---

Figma：

```text
AlertBanner
```

Vue：

```text
AlertBanner.vue
```

---

Figma：

```text
Input
```

Vue：

```text
BaseInput.vue
```

---

Figma：

```text
Select
```

Vue：

```text
BaseSelect.vue
```

---

## 10.3 建議 Vue Components

```text
components/
├─ base/
│  ├─ BaseButton.vue
│  ├─ BaseInput.vue
│  ├─ BaseSelect.vue
│  ├─ BaseTextarea.vue
│  └─ BaseModal.vue
│
├─ booking/
│  ├─ SlotChip.vue
│  ├─ BookingSummary.vue
│  ├─ ReservationStepper.vue
│  └─ TimeSlotGrid.vue
│
├─ feedback/
│  ├─ StatusBadge.vue
│  └─ AlertBanner.vue
│
└─ venue/
   └─ VenueCard.vue
```

---

# 11. 團隊協作規則

---

## 11.1 設計端

新頁面優先使用現有：

- Variables
- Components
- Patterns

若不足，再討論是否新增 Design System 元件。

---

## 11.2 前端端

不要自行新增未定義的，要自行新增的話：

- 顏色
- Font Size
- Radius
- Spacing
- Button Style

如果真的需要：

1. 先確認現有 Design System 是否已有類似 Token
2. 與團隊討論
3. 更新 Figma Design System
4. 再更新前端 Token

---

## 11.3 Component Rule

相同功能：

```text
優先 Reuse
```

而不是：

```text
重新做一份
```

---

## 11.4 Design System Change

若變更 Design System：

```text
Figma
↓
Design System Review
↓
Component / Token 更新
↓
Vue Component / Tailwind 更新
↓
頁面同步
```

---

# 12. Design System v1 範圍

## Foundations

- Color
- Typography
- Spacing
- Radius
- Border
- Shadow

## Components

- Button
- StatusBadge
- SlotChip
- AlertBanner
- Input
- Select
- DatePicker
- Checkbox
- Radio
- Textarea
- SearchInput

## Patterns

- Search Bar
- Venue Card
- Time Slot Grid
- Booking Summary
- Reservation Stepper

## Layout

- Header
- Footer
- Container
- Responsive Page Structure

---

# 13. 後續版本規劃

## v1.0

目前先完成：

- 前台核心 Foundations
- 預約核心 Components
- 基本 Layout

---

## v1.1

建議增加：

- Modal
- Tabs
- Breadcrumb
- Pagination
- Toast
- Empty State
- Loading State
- Skeleton
- Tooltip
- Dropdown

---

## v2.0

進一步補充：

- Responsive Rules
- Accessibility
- Motion
- Interaction
- Dark Mode（如需要）
- 更完整 Component Library
- Design Token 與 Tailwind 自動同步規則

---

# 14. 總結

VenueGo Design System 的目的不是單純讓畫面更漂亮，而是建立一套：

## 統一

所有人使用相同設計語言。

## 可重複使用

避免相同 UI 重複製作。

## 可維護

Design System 更新後，能同步元件與畫面。

## 可開發

Figma 規格能直接對應 Vue Component 與 Tailwind Token。

---

# 最終流程

```text
Wireframe
↓
Design Token
↓
Figma Component
↓
UI Pattern
↓
High-Fidelity UI
↓
Vue Component
↓
Tailwind CSS
↓
C# Web API
↓
VenueGo 前台
```

---

# Key Takeaway

> VenueGo 建立 Design System 的目的，是在正式進入 Vue 前台開發之前，先統一設計語言、元件規格與互動狀態，讓 UI 設計、資料需求與前端開發都有共同依據，降低團隊各自實作不同版本的情況。

---

# 建議維護方式

文件版本：

```text
v1.0
```

若有變更，建議記錄：

```text
v1.1
- 新增 Modal
- 新增 Toast
- 調整 Button Hover
- 新增 Mobile Rules
```

## 變更紀錄

```text
v1.1（草案，色碼待 Figma 確認）
- 新增 Color / Brand / Dark（#0D233E）：Footer 等深色區塊背景
- 新增 Color / Brand / Accent（#F03010）：運動類型等小型圖示
```

---

**VenueGo Frontend Figma Design System v1**  
**Figma → Vue.js → Tailwind CSS**
