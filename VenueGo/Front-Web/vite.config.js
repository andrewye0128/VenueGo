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
      // 在 Nuxt UI 內建的顏色名稱之外，新增 action（Design System 的 Brand / Action）
      theme: {
        colors: ["primary", "secondary", "success", "info", "warning", "error", "action"],
      },
      // 把 Nuxt UI 元件調整成 VenueGo Design System 的樣子
      // 顏色本身在 main.css 用 --ui-* 變數對應到品牌色
      ui: {
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
        // UButton 對應 BaseButton：action / primary = solid、secondary = outline、text = ghost
        button: {
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
            {
              color: "primary",
              variant: "outline",
              class:
                "ring-brand-primary bg-neutral-surface hover:bg-brand-primary/8 active:bg-brand-primary/16 disabled:bg-neutral-border aria-disabled:bg-neutral-border disabled:text-white aria-disabled:text-white disabled:ring-neutral-border aria-disabled:ring-neutral-border",
            },
            // 純文字（text）
            {
              color: "primary",
              variant: "ghost",
              class:
                "hover:bg-brand-primary/8 active:bg-brand-primary/16 disabled:text-neutral-border aria-disabled:text-neutral-border",
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
