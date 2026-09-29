<script setup>
import { watch, onMounted, onUnmounted } from "vue";
import { RouterLink, useRoute } from "vue-router";
import Logo from "@/components/Logo.vue";
import IconClose from "@/components/icons/IconClose.vue";

const props = defineProps({
  open: { type: Boolean, default: false },
  items: { type: Array, required: true },
});
const emit = defineEmits(["close"]);

// 點選單換頁後自動關閉
const route = useRoute();
watch(
  () => route.fullPath,
  () => emit("close"),
);

// 開啟時鎖住背後頁面的捲動
watch(
  () => props.open,
  (isOpen) => {
    document.body.style.overflow = isOpen ? "hidden" : "";
  },
);

// 按 Esc 關閉
const onKeydown = (e) => {
  if (e.key === "Escape" && props.open) emit("close");
};
onMounted(() => window.addEventListener("keydown", onKeydown));
onUnmounted(() => {
  window.removeEventListener("keydown", onKeydown);
  document.body.style.overflow = "";
});
</script>

<template>
  <!-- 搬到 <body> 底下，避免被 sticky header 的層級限制住 -->
  <Teleport to="body">
    <Transition
      enter-active-class="transition-opacity duration-300"
      enter-from-class="opacity-0"
      leave-active-class="transition-opacity duration-300"
      leave-to-class="opacity-0"
    >
      <div
        v-if="open"
        class="fixed inset-0 z-50 bg-black/40 xl:hidden"
        aria-hidden="true"
        @click="emit('close')"
      ></div>
    </Transition>

    <Transition
      enter-active-class="transition-transform duration-300"
      enter-from-class="-translate-x-full"
      leave-active-class="transition-transform duration-300"
      leave-to-class="-translate-x-full"
    >
      <aside
        v-if="open"
        class="fixed inset-y-0 left-0 z-50 flex w-72 max-w-[85vw] flex-col border-r border-neutral-border bg-neutral-surface xl:hidden"
        aria-label="主選單"
      >
        <div class="flex h-16 items-center justify-between border-b border-neutral-border px-4">
          <Logo class="h-8" />
          <button
            type="button"
            class="cursor-pointer rounded p-2 text-neutral-text-secondary transition hover:bg-brand-primary/8 active:bg-brand-primary/16"
            aria-label="關閉選單"
            @click="emit('close')"
          >
            <IconClose class="h-6 w-6" />
          </button>
        </div>

        <ul class="flex-1 overflow-y-auto py-2">
          <li v-for="item in items" :key="item.to">
            <RouterLink
              :to="item.to"
              class="block border-l-4 border-transparent px-6 py-3 font-semibold text-neutral-text-primary transition-colors hover:bg-brand-background"
              active-class="!border-brand-primary bg-brand-primary/8 !text-brand-primary"
            >
              {{ item.label }}
            </RouterLink>
          </li>
        </ul>
      </aside>
    </Transition>
  </Teleport>
</template>
