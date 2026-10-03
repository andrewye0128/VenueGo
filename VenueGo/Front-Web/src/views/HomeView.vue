<script setup>
import BannerCarousel from "@/components/banner/BannerCarousel.vue";
import BookingSearchCard from "@/components/booking/BookingSearchCard.vue";
</script>

<template>
  <main>
    <!-- 一頁要有一個 h1；畫面上不顯示（sr-only），給螢幕閱讀器和搜尋引擎 -->
    <h1 class="sr-only">VenueGo 運動中心</h1>

    <BannerCarousel />

    <!-- 電腦版：-mt-16 往上移，壓在輪播照片的下緣；relative z-10 讓卡片疊在照片上面 -->
    <div class="relative z-10 mx-auto max-w-6xl px-4 pb-16 md:px-6 xl:-mt-16">
      <BookingSearchCard />
    </div>
  </main>
</template>
