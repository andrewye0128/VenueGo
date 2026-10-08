<script setup>
// 場地圖卡：照片、場地名稱、運動類型標籤、兩個快速連結
// UBadge、UButton 由 Nuxt UI 自動匯入
defineProps({
  // 資料格式同 GET /api/venues 的 data 單筆：
  // { venueId, venueName, sportTypeId, sportTypeName, photoPath }
  venue: { type: Object, required: true },
});
</script>

<template>
  <article
    class="flex h-full flex-col overflow-hidden rounded-lg border border-neutral-border bg-neutral-surface"
  >
    <!-- 場地照片：固定 4:3 裁切，後台上傳的照片尺寸不一時卡片仍一樣高 -->
    <img
      v-if="venue.photoPath"
      :src="venue.photoPath"
      :alt="venue.venueName"
      loading="lazy"
      class="aspect-4/3 w-full object-cover"
    />
    <div
      v-else
      class="flex aspect-4/3 w-full items-center justify-center bg-brand-background text-sm text-neutral-text-secondary"
    >
      尚無照片
    </div>

    <div class="flex flex-1 flex-col gap-2 p-4">
      <!-- 名稱過長時截斷成一行，滑鼠停留顯示完整名稱 -->
      <h3
        class="truncate text-base font-semibold text-neutral-text-primary"
        :title="venue.venueName"
      >
        {{ venue.venueName }}
      </h3>

      <UBadge color="primary" variant="subtle" class="self-start">
        {{ venue.sportTypeName }}
      </UBadge>

      <!-- mt-auto：按鈕固定在卡片底部 -->
      <div class="mt-auto grid grid-cols-2 gap-2 pt-2">
        <UButton
          color="primary"
          variant="outline"
          block
          :to="{ name: 'venues', params: { sportTypeId: venue.sportTypeId } }"
        >
          瀏覽介紹
        </UButton>
        <UButton color="action" block :to="{ path: '/booking', query: { venueId: venue.venueId } }">
          我要預約
        </UButton>
      </div>
    </div>
  </article>
</template>
