<!--
  TimeText.vue — 「3 天前」這種相對時間，點一下切換成完整時間
  用法：<TimeText :time="item.createdAt" />    （time 是後端的 TimeView：{ value, ago, full }）

  手機沒有「滑鼠停上去」這個動作，只放在 title 的完整時間永遠看不到，所以改成點一下切換。
  用 <button> 包起來：鍵盤可以 Tab 到、按 Enter／空白鍵切換，讀螢幕也知道它能按。

  點擊範圍：文字只有十幾 px 高。觸控裝置（pointer-coarse）上用「上下 padding + 等量負 margin」
  把可點範圍撐到約 44px，但版面位置不變，不會把卡片撐高。
-->
<script setup>
import { ref } from "vue";

defineProps({
  time: { type: Object, required: true },
});

const showFull = ref(false);
</script>

<template>
  <button
    type="button"
    class="cursor-pointer rounded text-inherit focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary pointer-coarse:-mx-1 pointer-coarse:-my-3 pointer-coarse:px-1 pointer-coarse:py-3"
    @click="showFull = !showFull"
  >
    <time :datetime="time.value" :title="time.full">{{ showFull ? time.full : time.ago }}</time>
  </button>
</template>
