<script setup>
// 運動類型下拉選單 SportTypeSelect 的使用說明頁（給組員參考）
import { ref } from "vue";
import SportTypeSelect from "@/components/SportTypeSelect.vue";
import { ALL_SPORTS } from "@/constants/sportTypes";

const sportEmpty = ref("");
const sportSelected = ref("basketball");
const sportDisabled = ref("");
const sportWithAll = ref(ALL_SPORTS);
const sportPartial = ref("");

// 原始碼裡不能出現完整的 script 結束標籤（連註解也不行），否則 Vue 會以為 script 區塊在這裡結束
// 所以拆成兩段字串再接起來
const scriptEndTag = "</" + "script>";

const usageExample = `<script setup>
import { ref } from "vue";
import SportTypeSelect from "@/components/SportTypeSelect.vue";

const sport = ref("");
${scriptEndTag}

<template>
  <SportTypeSelect v-model="sport" />
</template>`;
</script>

<template>
  <main class="mx-auto max-w-7xl px-4 py-10 md:px-6">
    <h1 class="text-2xl font-bold text-neutral-text-primary">運動類型下拉選單範例</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">
      使用 &lt;SportTypeSelect&gt; 元件（底層為 Nuxt UI），選項、圖示、Label 都已內建。
    </p>

    <!-- grid-cols-1：手機一欄且寬度不超過螢幕，避免程式碼範例把整頁撐寬 -->
    <section class="mt-8 grid grid-cols-1 gap-8 md:grid-cols-2">
      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">① 預設（還沒選）</h2>
        <SportTypeSelect v-model="sportEmpty" />
        <p class="mt-3 text-xs text-neutral-text-secondary">
          寫法：&lt;SportTypeSelect v-model="sport" /&gt;
        </p>
        <p class="mt-1 text-xs text-neutral-text-secondary tabular-nums">
          目前選到的值：{{ sportEmpty || "（空）" }}
        </p>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">② 已選擇</h2>
        <SportTypeSelect v-model="sportSelected" />
        <p class="mt-3 text-xs text-neutral-text-secondary">
          寫法：同 ①，v-model 的初始值設為 "basketball"
        </p>
        <p class="mt-1 text-xs text-neutral-text-secondary tabular-nums">
          目前選到的值：{{ sportSelected || "（空）" }}
        </p>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">③ 停用</h2>
        <SportTypeSelect v-model="sportDisabled" disabled />
        <p class="mt-3 text-xs text-neutral-text-secondary">
          寫法：&lt;SportTypeSelect v-model="sport" disabled /&gt;
        </p>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">④ 全部運動（搜尋、篩選用）</h2>
        <SportTypeSelect v-model="sportWithAll" include-all />
        <p class="mt-3 text-xs text-neutral-text-secondary">
          寫法：&lt;SportTypeSelect v-model="sport" include-all /&gt;，初始值設為 ALL_SPORTS
        </p>
        <p class="mt-1 text-xs text-neutral-text-secondary tabular-nums">
          目前選到的值：{{ sportWithAll || "（空）" }}
        </p>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">⑤ 部分運動停用</h2>
        <SportTypeSelect v-model="sportPartial" :disabled-values="['swimming']" />
        <p class="mt-3 text-xs text-neutral-text-secondary">
          寫法：&lt;SportTypeSelect v-model="sport" :disabled-values="['swimming']" /&gt;
        </p>
        <p class="mt-1 text-xs text-neutral-text-secondary tabular-nums">
          目前選到的值：{{ sportPartial || "（空）" }}
        </p>
      </div>

      <div class="rounded border border-neutral-border bg-neutral-surface p-6">
        <h2 class="mb-4 font-semibold text-neutral-text-primary">組員的使用方式</h2>
        <pre
          class="overflow-x-auto rounded bg-brand-background p-4 text-xs text-neutral-text-primary"
        ><code>{{ usageExample }}</code></pre>
        <p class="mt-3 text-xs text-neutral-text-secondary">
          其他選填參數：label、placeholder、error（顯示錯誤訊息）。
        </p>
      </div>
    </section>
  </main>
</template>
