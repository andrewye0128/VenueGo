// 場地預約頁的選項
// 圖示名稱要寫完整，Nuxt UI 打包時才掃描得到

// 時段快速篩選：start 含、end 不含（"HH:mm"）；「全部」沒有範圍
export const TIME_PERIODS = [
  { key: "all", label: "全部時段", icon: "i-mdi-clock-outline" },
  {
    key: "morning",
    label: "上午",
    hint: "～12:00",
    icon: "i-mdi-weather-sunset-up",
    start: "00:00",
    end: "12:00",
  },
  {
    key: "afternoon",
    label: "下午",
    hint: "12:00–18:00",
    icon: "i-mdi-white-balance-sunny",
    start: "12:00",
    end: "18:00",
  },
  {
    key: "evening",
    label: "晚上",
    hint: "18:00～",
    icon: "i-mdi-weather-night",
    start: "18:00",
    end: "24:00",
  },
];

export const DEFAULT_TIME_PERIOD = "all";

// 這個時段（"18:00"）是否在篩選範圍內
export function isInPeriod(slotTime, periodKey) {
  const period = TIME_PERIODS.find((p) => p.key === periodKey);
  if (!period?.start) return true;
  return slotTime >= period.start && slotTime < period.end; // "HH:mm" 可以直接比字串
}

// 日期列一次顯示幾天
export const DATE_BAR_DAYS = 7;
