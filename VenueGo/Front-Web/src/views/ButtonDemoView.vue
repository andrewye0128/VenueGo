<script setup>
// BaseButton（自己寫的）與 UButton（Nuxt UI）比較頁：同一列左右兩邊應長得一樣
// UButton 由 Nuxt UI 自動匯入，不用 import
import BaseButton from "@/components/base/BaseButton.vue";
import IconCalendarPlus from "@/components/icons/IconCalendarPlus.vue";

const baseCode = `<BaseButton variant="action" to="/booking">立即預約</BaseButton>
<BaseButton variant="primary">下一步</BaseButton>
<BaseButton variant="secondary" to="/login">登入</BaseButton>
<BaseButton variant="text">查看全部</BaseButton>
<BaseButton variant="action" icon-only aria-label="立即預約">
  <IconCalendarPlus class="h-6 w-6" />
</BaseButton>`;

const nuxtCode = `<UButton color="action" to="/booking">立即預約</UButton>
<UButton color="primary">下一步</UButton>
<UButton color="primary" variant="outline" to="/login">登入</UButton>
<UButton color="primary" variant="ghost">查看全部</UButton>
<UButton color="action" icon="i-lucide-calendar-plus"
  square aria-label="立即預約" />
<UButton color="action" loading>送出中</UButton>`;
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">按鈕比較：BaseButton vs UButton</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">
      同一列左右兩邊應長得一樣。可以用滑鼠停留、按下去，比較兩邊的 Hover、Pressed 效果。
    </p>

    <section class="mt-8 overflow-x-auto rounded border border-neutral-border bg-neutral-surface">
      <table class="w-full text-sm">
        <thead class="border-b border-neutral-border bg-brand-background text-left">
          <tr>
            <th class="w-40 px-6 py-3 font-semibold text-neutral-text-primary">類型</th>
            <th class="px-6 py-3 font-semibold text-neutral-text-primary">
              BaseButton（自己寫的）
            </th>
            <th class="px-6 py-3 font-semibold text-neutral-text-primary">UButton（Nuxt UI）</th>
          </tr>
        </thead>
        <tbody class="divide-y divide-neutral-border">
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">action</td>
            <td class="px-6 py-4">
              <BaseButton variant="action" to="/booking">立即預約</BaseButton>
            </td>
            <td class="px-6 py-4"><UButton color="action" to="/booking">立即預約</UButton></td>
          </tr>
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">primary</td>
            <td class="px-6 py-4"><BaseButton variant="primary">下一步</BaseButton></td>
            <td class="px-6 py-4"><UButton color="primary">下一步</UButton></td>
          </tr>
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">secondary</td>
            <td class="px-6 py-4"><BaseButton variant="secondary" to="/login">登入</BaseButton></td>
            <td class="px-6 py-4">
              <UButton color="primary" variant="outline" to="/login">登入</UButton>
            </td>
          </tr>
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">text</td>
            <td class="px-6 py-4"><BaseButton variant="text">查看全部</BaseButton></td>
            <td class="px-6 py-4"><UButton color="primary" variant="ghost">查看全部</UButton></td>
          </tr>
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">只有圖示</td>
            <td class="px-6 py-4">
              <BaseButton variant="action" icon-only aria-label="立即預約">
                <IconCalendarPlus class="h-6 w-6" />
              </BaseButton>
            </td>
            <td class="px-6 py-4">
              <UButton color="action" icon="i-lucide-calendar-plus" square aria-label="立即預約" />
            </td>
          </tr>
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">尺寸 sm / md / lg</td>
            <td class="px-6 py-4">
              <div class="flex flex-wrap items-center gap-3">
                <BaseButton variant="action" size="sm">立即預約</BaseButton>
                <BaseButton variant="action" size="md">立即預約</BaseButton>
                <BaseButton variant="action" size="lg">立即預約</BaseButton>
              </div>
            </td>
            <td class="px-6 py-4">
              <div class="flex flex-wrap items-center gap-3">
                <UButton color="action" size="sm">立即預約</UButton>
                <UButton color="action" size="md">立即預約</UButton>
                <UButton color="action" size="lg">立即預約</UButton>
              </div>
            </td>
          </tr>
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">停用</td>
            <td class="px-6 py-4">
              <div class="flex flex-wrap items-center gap-3">
                <BaseButton variant="action" disabled>立即預約</BaseButton>
                <BaseButton variant="primary" disabled>下一步</BaseButton>
                <BaseButton variant="secondary" disabled>登入</BaseButton>
                <BaseButton variant="text" disabled>查看全部</BaseButton>
              </div>
            </td>
            <td class="px-6 py-4">
              <div class="flex flex-wrap items-center gap-3">
                <UButton color="action" disabled>立即預約</UButton>
                <UButton color="primary" disabled>下一步</UButton>
                <UButton color="primary" variant="outline" disabled>登入</UButton>
                <UButton color="primary" variant="ghost" disabled>查看全部</UButton>
              </div>
            </td>
          </tr>
          <tr>
            <td class="px-6 py-4 text-neutral-text-secondary">載入中</td>
            <td class="px-6 py-4 text-neutral-text-secondary">（沒有這個功能）</td>
            <td class="px-6 py-4"><UButton color="action" loading>送出中</UButton></td>
          </tr>
        </tbody>
      </table>
    </section>

    <section class="mt-8 grid gap-8 md:grid-cols-2">
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">BaseButton 寫法</h2>
        <pre
          class="overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ baseCode }}</code></pre>
      </div>
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">UButton 寫法</h2>
        <pre
          class="overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ nuxtCode }}</code></pre>
        <p class="mt-3 text-xs text-neutral-text-secondary">
          只有圖示的按鈕：UButton 用 Iconify 名稱（這裡用 Lucide 的圖示），所以圖案會跟
          BaseButton（Heroicons）略有不同。
        </p>
      </div>
    </section>
  </main>
</template>
