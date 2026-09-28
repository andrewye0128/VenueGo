<script setup>
import { computed } from "vue";
import { RouterLink } from "vue-router";

const props = defineProps({
  // 樣式：action = 立即預約類 CTA、primary = 確認/下一步、secondary = 返回/登入、text = 查看全部
  variant: {
    type: String,
    default: "primary",
    validator: (v) => ["action", "primary", "secondary", "text"].includes(v),
  },
  size: {
    type: String,
    default: "md",
    validator: (v) => ["sm", "md", "lg"].includes(v),
  },
  // 只放 icon 的正方形按鈕，記得另外加 aria-label
  iconOnly: { type: Boolean, default: false },
  // 有給 to 就渲染成 RouterLink（換頁），沒給就是 <button>
  to: { type: [String, Object], default: null },
  // HTML button 的 type（button / submit / reset）
  type: { type: String, default: "button" },
  disabled: { type: Boolean, default: false },
});

// 有底色的按鈕：Hover 變亮、Pressed 變暗
const FILLED_STATES = "hover:brightness-120 active:brightness-65";
// 白底 / 無底的按鈕：Hover、Pressed 疊一層淡淡的品牌藍
const LIGHT_STATES = "hover:bg-brand-primary/8 active:bg-brand-primary/16";

const variantClasses = {
  action: `bg-brand-action text-white ${FILLED_STATES}`,
  primary: `bg-brand-primary text-white ${FILLED_STATES}`,
  secondary: `border border-brand-primary bg-neutral-surface text-brand-primary ${LIGHT_STATES}`,
  text: `bg-transparent text-brand-primary ${LIGHT_STATES}`,
};

// Disabled 不使用品牌色，也不套任何 hover / active 效果
const disabledClasses = {
  filled: "cursor-not-allowed bg-neutral-border text-white",
  text: "cursor-not-allowed bg-transparent text-neutral-border",
};

// 高度依 4px 格線：sm 32px / md 40px / lg 48px
const sizeClasses = {
  sm: { box: "h-8 px-3 text-sm", icon: "h-8 w-8" },
  md: { box: "h-10 px-4 text-sm", icon: "h-10 w-10" },
  lg: { box: "h-12 px-6 text-base", icon: "h-12 w-12" },
};

const classes = computed(() => [
  "inline-flex items-center justify-center gap-2 rounded font-semibold whitespace-nowrap",
  "transition duration-150",
  "focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-primary",
  sizeClasses[props.size][props.iconOnly ? "icon" : "box"],
  props.disabled
    ? disabledClasses[props.variant === "text" ? "text" : "filled"]
    : ["cursor-pointer", variantClasses[props.variant]],
]);
</script>

<template>
  <!-- 連結無法真正停用，所以 disabled 時一律改成 <button disabled> -->
  <RouterLink v-if="to && !disabled" :to="to" :class="classes">
    <slot />
  </RouterLink>
  <button v-else :type="type" :disabled="disabled" :class="classes">
    <slot />
  </button>
</template>
