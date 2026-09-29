// undefined = the device's default locale (FR-021). Tests pin a locale explicitly.
let currentLocale: string | undefined

export function getLocale(): string | undefined {
  return currentLocale
}

export function setLocale(locale: string | undefined): void {
  currentLocale = locale
}
