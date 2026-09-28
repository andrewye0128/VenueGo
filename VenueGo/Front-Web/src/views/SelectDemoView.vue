<script setup>
// 【POC】下拉選單比較範例 — Nuxt UI 版，只存在 poc/select-nuxtui 分支
// UFormField、USelect 由 Nuxt UI 自動匯入，不用 import
import { ref } from "vue";

// icon 用 Iconify 名稱（i-集合-名稱），需安裝 @iconify-json/mdi
const sportItems = [
  { label: "籃球", value: "basketball", icon: "i-mdi-basketball" },
  { label: "羽球", value: "badminton", icon: "i-mdi-badminton" },
  { label: "桌球", value: "table-tennis", icon: "i-mdi-table-tennis" },
  { label: "排球", value: "volleyball", icon: "i-mdi-volleyball" },
  { label: "游泳（維修中）", value: "swimming", icon: "i-mdi-swim", disabled: true },
];

// USelect 不會自動在選單框顯示選中項目的 icon，要自己查出來傳給 :icon
const iconOf = (value) => sportItems.find((item) => item.value === value)?.icon;

// 關閉整頁捲動鎖定（與 Reka UI 版相同的閃動問題），每次使用都要傳
const contentOptions = { bodyLock: false };

const sportEmpty = ref("");
const sportSelected = ref("basketball");
const sportDisabled = ref("");

const usageExample = `<UFormField label="運動類型">
  <USelect
    v-model="sport"
    :items="sportItems"
    :icon="iconOf(sport)"
    :content="{ bodyLock: false }"
    placeholder="請選擇運動類型"
    class="w-full"
  />
</UFormField>`;
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">下拉選單比較範例：Nuxt UI 版</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">
      使用 Nuxt UI 的 &lt;USelect&gt; 與 &lt;UFormField&gt;，外觀透過 vite.config.js 的主題設定與
      main.css 的 --ui-* 變數調整成 VenueGo Design System 的樣子。
    </p>

    <section class="mt-8 grid gap-8 md:grid-cols-2">
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">① 預設（還沒選）</h2>
        <UFormField label="運動類型">
          <USelect
            v-model="sportEmpty"
            :items="sportItems"
            :icon="iconOf(sportEmpty)"
            :content="contentOptions"
            placeholder="請選擇運動類型"
            class="w-full"
          />
        </UFormField>
        <p class="mt-3 text-xs text-neutral-text-secondary tabular-nums">
          目前選到的值：{{ sportEmpty || "（空）" }}
        </p>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">② 已選擇</h2>
        <UFormField label="運動類型">
          <USelect
            v-model="sportSelected"
            :items="sportItems"
            :icon="iconOf(sportSelected)"
            :content="contentOptions"
            placeholder="請選擇運動類型"
            class="w-full"
          />
        </UFormField>
        <p class="mt-3 text-xs text-neutral-text-secondary tabular-nums">
          目前選到的值：{{ sportSelected || "（空）" }}
        </p>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">③ 停用</h2>
        <UFormField label="運動類型">
          <USelect
            v-model="sportDisabled"
            :items="sportItems"
            :icon="iconOf(sportDisabled)"
            :content="contentOptions"
            placeholder="請選擇運動類型"
            class="w-full"
            disabled
          />
        </UFormField>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">組員的使用方式</h2>
        <pre
          class="overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ usageExample }}</code></pre>
        <p class="mt-3 text-xs text-neutral-text-secondary">
          錯誤狀態：在 UFormField 加上 error="…"（運動類型不會用到，範例頁不展示）。
        </p>
      </div>
    </section>
  </main>
</template>
