<script setup>
// 租借條款視窗（填寫資料頁）
// 文字從後台 Views/ReservationCreate/_TermsModal.cshtml 複製；拿掉「管理員代客」的說明
// ⚠️ 條款改版時：後台、這裡的文字都要改，後端 appsettings.json 的 ReservationRules.TermsVersion 也要改，
//    版本號會記錄在每一筆預約（Reservations.TermsVersion）
// v-model:open：是否打開；version：條款版本（試算 API 回傳的 termsVersion）
// UModal、UButton 由 Nuxt UI 自動匯入
const open = defineModel("open", { type: Boolean, default: false });

defineProps({
  version: { type: String, required: true },
});

const SECTIONS = [
  {
    title: "一、預約與付款",
    items: [
      "場地以每小時為一個時段，單筆預約的時段須連續。",
      "預約成立後請於付款期限前完成付款，逾期未付款的預約將自動失效。",
      "場地費用依運動類型與尖峰、離峰時段計算，以建立預約時顯示的金額為準。",
    ],
  },
  {
    title: "二、取消與退費",
    items: [
      "會員因故取消預約，得依取消時間與使用時間的間隔計算退費比例。",
      "因場地維護、設備故障或天災等場館因素取消者，全額退費，不扣任何費用。",
      "未依預約時間到場使用者，視為未報到，不予退費。",
    ],
  },
  {
    title: "三、使用規範",
    items: [
      "使用人數不得超過場地公告的可容納人數。",
      "請於預約時段內使用場地，逾時使用須另行計費。",
      "場地內設備請妥善使用，因故意或過失致損壞者應負賠償責任。",
      "請自備運動鞋，禁止穿著硬底鞋或釘鞋進入室內場地。",
    ],
  },
];
</script>

<template>
  <UModal
    v-model:open="open"
    :title="`場地租借條款（${version}）`"
    :close="{ 'aria-label': '關閉' }"
    scrollable
  >
    <template #body>
      <div class="flex flex-col gap-5 text-sm text-neutral-text-primary">
        <section v-for="section in SECTIONS" :key="section.title">
          <h3 class="mb-2 font-semibold">{{ section.title }}</h3>
          <ol class="list-decimal space-y-1 ps-5 text-neutral-text-secondary">
            <li v-for="item in section.items" :key="item">{{ item }}</li>
          </ol>
        </section>
        <p class="rounded bg-brand-background p-3 text-xs text-neutral-text-secondary">
          以上為示意條款，正式條款內容請依場館公告為準。版本 {{ version }}
          將記錄於本筆預約，日後條款改版不影響已成立的預約。
        </p>
      </div>
    </template>
    <template #footer>
      <div class="flex w-full justify-end">
        <UButton color="primary" variant="outline" @click="open = false">關閉</UButton>
      </div>
    </template>
  </UModal>
</template>
