export const TAX_RATE = 10

export function calculateOrderTotals(subtotal: number) {
  const taxAmount = Math.round((subtotal * TAX_RATE) / 100)
  return {
    subtotal,
    taxRate: TAX_RATE,
    taxAmount,
    total: subtotal + taxAmount,
  }
}
