<script setup>
// 場地卡片：依運動分組，每張卡片顯示縮圖、名稱、可容納人數、剩幾格、最早幾點有空
// 點一張卡片 → 頁面在下方顯示這個場地的時段
//
// v-model：選中的 venueId（沒選是 null）
// groups：[{ sportTypeId, sportName, icon, venues: [{ venueId, venueName, capacity, photoPath, availableCount, earliest }] }]
//   availableCount、earliest 由頁面依目前的「時段篩選」算好傳進來
// showHeadings：只有一種運動時不用顯示運動標題
// UIcon 由 Nuxt UI 自動匯入
import { reactive } from "vue";

const model = defineModel({ type: Number, default: null });

defineProps({
  groups: { type: Array, required: true },
  showHeadings: { type: Boolean, default: true },
});

// 照片讀取失敗的場地：改顯示運動圖示（例如 PhotoPath 有值，但檔案不存在、回傳 404）
const brokenPhotos = reactive(new Set());
</script>

<template>
  <div class="flex flex-col gap-5">
    <section v-for="group in groups" :key="group.sportTypeId">
      <h3
        v-if="showHeadings"
        class="mb-2 flex items-center gap-1.5 text-sm font-semibold text-neutral-text-primary"
      >
        <UIcon :name="group.icon" class="size-5 text-brand-accent" />
        {{ group.sportName }}
      </h3>

      <!-- 手機一排 1 張、平板 2 張、寬的時候 3 張 -->
      <ul class="grid grid-cols-1 gap-2 sm:grid-cols-2 xl:grid-cols-3">
        <li v-for="venue in group.venues" :key="venue.venueId">
          <button
            type="button"
            :aria-pressed="model === venue.venueId"
            class="flex w-full cursor-pointer items-center gap-3 rounded border p-2 text-start transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary"
            :class="
              model === venue.venueId
                ? 'border-brand-primary bg-brand-primary/8 ring-1 ring-brand-primary'
                : 'border-neutral-border bg-neutral-surface hover:border-brand-primary'
            "
            @click="model = venue.venueId"
          >
            <!-- 縮圖 72×72；沒有照片或讀取失敗時顯示運動圖示
                 loading="lazy"：捲動到附近才下載，「全部運動」時不會一次下載十幾張 -->
            <span
              class="flex size-18 shrink-0 items-center justify-center overflow-hidden rounded bg-brand-background"
            >
              <img
                v-if="venue.photoPath && !brokenPhotos.has(venue.venueId)"
                :src="venue.photoPath"
                alt=""
                loading="lazy"
                class="size-full object-cover"
                @error="brokenPhotos.add(venue.venueId)"
              />
              <UIcon v-else :name="group.icon" class="size-8 text-neutral-text-secondary/60" />
            </span>

            <span class="min-w-0 flex-1">
              <span class="block truncate font-semibold text-neutral-text-primary">
                {{ venue.venueName }}
              </span>
              <!-- 資料庫的 Capacity 允許 NULL：沒有設定時不顯示，避免出現「可容納  人」 -->
              <span v-if="venue.capacity != null" class="block text-xs text-neutral-text-secondary">
                可容納 {{ venue.capacity }} 人
              </span>
              <span
                class="mt-1 block text-sm"
                :class="venue.availableCount > 0 ? 'text-brand-primary' : 'text-semantic-error'"
              >
                <template v-if="venue.availableCount > 0">
                  剩 {{ venue.availableCount }} 格・最早 {{ venue.earliest }}
                </template>
                <template v-else>已額滿</template>
              </span>
            </span>

            <UIcon
              v-if="model === venue.venueId"
              name="i-mdi-check-circle"
              class="size-5 shrink-0 text-brand-primary"
            />
          </button>
        </li>
      </ul>
    </section>
  </div>
</template>
