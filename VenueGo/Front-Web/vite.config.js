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
      // 【POC】把 USelect / UFormField 調整成 VenueGo Design System 的樣子
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
