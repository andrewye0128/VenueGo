import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
// Nuxt UI 的 Vite 外掛已內建 Tailwind CSS，所以拿掉原本的 @tailwindcss/vite
import ui from "@nuxt/ui/vite";

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    ui({
      // Design System v1 沒有深色模式，關掉 Nuxt UI 依系統設定自動切換
      colorMode: false,
      // 把用到的 Iconify icon 打包進專案，不在執行時向網路抓
      icon: { clientBundle: { scan: true } },
      // 團隊規則：只有 Nuxt UI 的 U 開頭元件自動匯入，專案自己的元件（src/components）一律要寫 import
      // dirs: [] 代表不自動掃描任何專案資料夾；忘了 import 時，瀏覽器 console 會出現 Failed to resolve component 警告
      components: { dirs: [] },
      // 在 Nuxt UI 內建的顏色名稱之外，新增 action（Design System 的 Brand / Action）
      theme: {
        colors: ["primary", "secondary", "success", "info", "warning", "error", "action"],
      },
      // 把 Nuxt UI 元件調整成 VenueGo Design System 的樣子
      // 顏色本身在 main.css 用 --ui-* 變數對應到品牌色
      ui: {
        // 表單驗證時機：離開欄位（blur）或選項改變（change）時才檢查，打字途中不檢查
        // 寫在 defaultVariants 會成為全站所有 UForm 的預設值，組員不用每張表單都設定
        form: {
          defaultVariants: {
            validateOn: ["blur", "change"],
          },
        },
        // 輸入框、多行輸入框：外觀與 USelect 一致（高 40px、圓角 4px、滑鼠停留藍框）
        input: {
          slots: {
            base: "rounded disabled:opacity-100 disabled:bg-brand-background disabled:text-neutral-text-secondary",
          },
          variants: {
            size: {
              md: { base: "h-10 px-3 text-sm gap-2" },
            },
          },
          compoundVariants: [
            {
              color: "primary",
              variant: "outline",
              class:
                "hover:ring-brand-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary",
            },
          ],
        },
        textarea: {
          slots: {
            base: "rounded disabled:opacity-100 disabled:bg-brand-background disabled:text-neutral-text-secondary",
          },
          variants: {
            size: {
              md: { base: "px-3 py-2 text-sm" },
            },
          },
          compoundVariants: [
            {
              color: "primary",
              variant: "outline",
              class:
                "hover:ring-brand-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary",
            },
          ],
        },
        select: {
          slots: {
            base: "disabled:opacity-100 data-[state=open]:ring-brand-primary",
            placeholder: "text-neutral-text-secondary",
            leadingIcon: "text-brand-accent",
            trailingIcon:
              "text-neutral-text-secondary transition-transform group-data-[state=open]:rotate-180",
            content: "rounded shadow-none ring-neutral-border",
            // 選中項目被滑鼠停留時，Nuxt UI 內建的 highlighted 文字色權重較高，要用同樣權重的寫法蓋回藍色
            item: "items-center before:rounded data-highlighted:not-data-disabled:before:bg-brand-primary/8 data-[state=checked]:font-semibold data-[state=checked]:text-brand-primary data-highlighted:not-data-disabled:data-[state=checked]:text-brand-primary",
            itemLeadingIcon:
              "text-brand-accent group-data-highlighted:not-group-data-disabled:text-brand-accent",
            itemTrailingIcon: "text-brand-primary",
          },
          variants: {
            variant: {
              outline:
                "rounded hover:bg-neutral-surface hover:ring-brand-primary disabled:bg-brand-background disabled:text-neutral-text-secondary disabled:hover:ring-neutral-border",
            },
            size: {
              md: {
                base: "h-10 px-3 text-sm gap-2",
                item: "h-9 px-3 py-0 gap-2",
              },
            },
          },
          compoundVariants: [
            {
              color: "primary",
              variant: "outline",
              class:
                "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary",
            },
          ],
        },
        // UButton 對應設計規範的按鈕類型：action / primary = solid、secondary = outline、text = ghost
        button: {
          // 全站按鈕預設開啟 loadingAuto：在 UForm 裡的送出按鈕，送出處理中會自動轉圈、不能重複按
          // （@click 綁定的是會等待的 async 函式時，也會自動轉圈）
          defaultVariants: {
            loadingAuto: true,
          },
          slots: {
            base: "cursor-pointer justify-center rounded font-semibold transition disabled:opacity-100 aria-disabled:opacity-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary",
          },
          variants: {
            // 高度依 4px 格線：sm 32px / md 40px / lg 48px
            size: {
              sm: { base: "h-8 px-3 text-sm gap-2" },
              md: { base: "h-10 px-4 text-sm gap-2" },
              lg: { base: "h-12 px-6 text-base gap-2" },
            },
          },
          compoundVariants: [
            // 有底色：Hover 變亮 120%、Pressed 變暗 65%；Disabled 淺灰底白字
            // （class 要寫完整，Tailwind 是掃描原始碼文字找 class，用 ${} 組出來的會找不到）
            {
              color: "action",
              variant: "solid",
              class: "hover:bg-action active:bg-action text-white hover:brightness-120 active:brightness-65 disabled:bg-neutral-border aria-disabled:bg-neutral-border disabled:brightness-100 aria-disabled:brightness-100",
            },
            {
              color: "primary",
              variant: "solid",
              class: "hover:bg-primary active:bg-primary text-white hover:brightness-120 active:brightness-65 disabled:bg-neutral-border aria-disabled:bg-neutral-border disabled:brightness-100 aria-disabled:brightness-100",
            },
            // 白底藍框（secondary）
            // 用真正的 border 取代 Nuxt UI 預設的內側 ring：ring 不佔寬度，按鈕會比設計稿窄 2px
            {
              color: "primary",
              variant: "outline",
              class:
                "ring-0 border border-brand-primary bg-neutral-surface hover:bg-brand-primary/8 active:bg-brand-primary/16 disabled:bg-neutral-border aria-disabled:bg-neutral-border disabled:text-white aria-disabled:text-white disabled:border-neutral-border aria-disabled:border-neutral-border",
            },
            // 純文字（text）
            {
              color: "primary",
              variant: "ghost",
              class:
                "hover:bg-brand-primary/8 active:bg-brand-primary/16 disabled:text-neutral-border aria-disabled:text-neutral-border",
            },
            // 載入中：UButton 會自動加上 disabled（防止重複送出），這裡讓它維持原本顏色，游標顯示「處理中」
            {
              loading: true,
              color: "action",
              variant: "solid",
              class: "disabled:bg-action disabled:cursor-progress",
            },
            {
              loading: true,
              color: "primary",
              variant: "solid",
              class: "disabled:bg-primary disabled:cursor-progress",
            },
            {
              loading: true,
              color: "primary",
              variant: "outline",
              class:
                "disabled:bg-neutral-surface disabled:text-brand-primary disabled:border-brand-primary disabled:cursor-progress",
            },
            {
              loading: true,
              color: "primary",
              variant: "ghost",
              class: "disabled:text-brand-primary disabled:cursor-progress",
            },
            // 只有 icon 的正方形按鈕
            { size: "sm", square: true, class: "w-8 px-0" },
            { size: "md", square: true, class: "w-10 px-0" },
            { size: "lg", square: true, class: "w-12 px-0" },
          ],
        },
        formField: {
          slots: {
            label: "text-sm font-medium text-neutral-text-primary",
            error: "text-xs text-semantic-error",
          },
        },
      },
    }),
    vueDevTools(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  // 開發時把 /api 開頭的請求轉給 ASP.NET Core 後端
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7078',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
