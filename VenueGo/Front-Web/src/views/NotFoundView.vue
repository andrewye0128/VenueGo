<template>
  <!-- 找不到頁面（404）：網址不符合任何路由時顯示；UButton 由 Nuxt UI 自動匯入 -->
  <main class="mx-auto flex max-w-7xl flex-col items-center px-4 py-20 text-center md:px-6">
    <p class="text-6xl font-bold text-brand-primary tabular-nums">404</p>
    <h1 class="mt-4 text-2xl font-bold text-neutral-text-primary">找不到這個頁面</h1>
    <p class="mt-2 text-sm text-neutral-text-secondary">網址可能輸入錯誤，或這個頁面已經不存在。</p>
    <UButton color="action" to="/" class="mt-8">回到首頁</UButton>
  </main>
</template>
