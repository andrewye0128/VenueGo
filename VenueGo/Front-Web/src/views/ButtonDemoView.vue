<script setup>
// UButton 使用說明頁（給組員參考）
// UButton 由 Nuxt UI 自動匯入，不用 import
import IconCalendarPlus from "@/components/icons/IconCalendarPlus.vue";

// 對照表：Design System 5.1.1 的按鈕類型 → UButton 寫法
const typeRows = [
  {
    type: "action",
    code: 'color="action"',
    props: { color: "action" },
    label: "立即預約",
    usage: "立即預約、查詢空場、立即付款等推動主要任務的操作",
  },
  {
    type: "primary",
    code: 'color="primary"',
    props: { color: "primary" },
    label: "下一步",
    usage: "確認、下一步、儲存",
  },
  {
    type: "secondary",
    code: 'color="primary" variant="outline"',
    props: { color: "primary", variant: "outline" },
    label: "登入",
    usage: "返回、修改、重新選擇",
  },
  {
    type: "text",
    code: 'color="primary" variant="ghost"',
    props: { color: "primary", variant: "ghost" },
    label: "查看全部",
    usage: "查看全部、了解更多等輕量操作",
  },
];

const sizeCode = `<UButton color="action" size="sm">立即預約</UButton>  <!-- 32px -->
<UButton color="action">立即預約</UButton>             <!-- 40px（預設 md）-->
<UButton color="action" size="lg">立即預約</UButton>  <!-- 48px -->`;

const iconCode = `<!-- 用 Iconify 名稱 -->
<UButton color="action" icon="i-lucide-calendar-plus" square aria-label="立即預約" />

<!-- 或放入專案自己的圖示元件 -->
<UButton color="action" square aria-label="立即預約">
  <IconCalendarPlus class="h-6 w-6" />
</UButton>

<!-- 圖示 + 文字 -->
<UButton color="action" icon="i-lucide-calendar-plus">立即預約</UButton>`;

const linkCode = `<!-- 有 to 就是換頁連結 -->
<UButton color="action" to="/booking">立即預約</UButton>`;

const disabledCode = `<UButton color="action" disabled>立即預約</UButton>`;

const loadingCode = `<!-- 載入中：維持原本顏色 + 轉圈圖示，且不能再按（防止重複送出） -->
<UButton color="action" loading>送出中</UButton>`;
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">UButton 使用說明</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">
      按鈕統一使用 Nuxt UI 的 &lt;UButton&gt;，外觀已依 VenueGo Design System 設定好，請不要自己加
      class 修改顏色、大小、圓角。
    </p>

    <!-- ① 對照表 -->
    <section class="mt-8">
      <h2 class="mb-4 text-lg font-bold text-neutral-text-primary">① 按鈕類型對照表</h2>
      <div class="overflow-x-auto rounded border border-neutral-border bg-neutral-surface">
        <table class="w-full min-w-180 text-sm">
          <thead class="border-b border-neutral-border bg-brand-background text-left">
            <tr>
              <th class="px-6 py-3 font-semibold text-neutral-text-primary">設計規範</th>
              <th class="px-6 py-3 font-semibold text-neutral-text-primary">UButton 寫法</th>
              <th class="px-6 py-3 font-semibold text-neutral-text-primary">長這樣</th>
              <th class="px-6 py-3 font-semibold text-neutral-text-primary">用途</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-neutral-border">
            <tr v-for="row in typeRows" :key="row.type">
              <td class="px-6 py-4 font-semibold text-neutral-text-primary">{{ row.type }}</td>
              <td class="px-6 py-4">
                <code
                  class="rounded bg-brand-background px-2 py-1 text-xs text-neutral-text-primary"
                >
                  {{ row.code }}
                </code>
              </td>
              <td class="px-6 py-4">
                <UButton v-bind="row.props">{{ row.label }}</UButton>
              </td>
              <td class="px-6 py-4 text-neutral-text-secondary">{{ row.usage }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <!-- grid-cols-1：手機一欄且寬度不超過螢幕，避免程式碼範例把整頁撐寬 -->
    <section class="mt-8 grid grid-cols-1 gap-8 md:grid-cols-2">
      <!-- ② 尺寸 -->
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">② 尺寸：sm / md / lg</h2>
        <div class="flex flex-wrap items-center gap-3">
          <UButton color="action" size="sm">立即預約</UButton>
          <UButton color="action">立即預約</UButton>
          <UButton color="action" size="lg">立即預約</UButton>
        </div>
        <pre
          class="mt-4 overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ sizeCode }}</code></pre>
      </div>

      <!-- ③ 圖示 -->
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">③ 圖示</h2>
        <div class="flex flex-wrap items-center gap-3">
          <UButton color="action" icon="i-lucide-calendar-plus" square aria-label="立即預約" />
          <UButton color="action" square aria-label="立即預約">
            <IconCalendarPlus class="h-6 w-6" />
          </UButton>
          <UButton color="action" icon="i-lucide-calendar-plus">立即預約</UButton>
        </div>
        <pre
          class="mt-4 overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ iconCode }}</code></pre>
        <p class="mt-3 text-xs text-neutral-text-secondary">
          只有圖示的按鈕一定要加 aria-label，螢幕閱讀器才知道這個按鈕是做什麼的。
        </p>
      </div>

      <!-- ④ 換頁連結 -->
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">④ 換頁連結（to）</h2>
        <UButton color="action" to="/booking">立即預約</UButton>
        <pre
          class="mt-4 overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ linkCode }}</code></pre>
      </div>

      <!-- ⑤ 停用 -->
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">⑤ 停用（disabled）</h2>
        <div class="flex flex-wrap items-center gap-3">
          <UButton color="action" disabled>立即預約</UButton>
          <UButton color="primary" disabled>下一步</UButton>
          <UButton color="primary" variant="outline" disabled>登入</UButton>
          <UButton color="primary" variant="ghost" disabled>查看全部</UButton>
        </div>
        <pre
          class="mt-4 overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ disabledCode }}</code></pre>
      </div>

      <!-- ⑥ 載入中 -->
      <div class="rounded border border-neutral-border bg-neutral-surface p-6 md:col-span-2">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">⑥ 載入中（loading）</h2>
        <div class="flex flex-wrap items-center gap-3">
          <UButton color="action" loading>送出中</UButton>
          <UButton color="primary" loading>儲存中</UButton>
          <UButton color="primary" variant="outline" loading>處理中</UButton>
          <UButton color="primary" variant="ghost" loading>載入中</UButton>
        </div>
        <pre
          class="mt-4 overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ loadingCode }}</code></pre>
      </div>
    </section>
  </main>
</template>
