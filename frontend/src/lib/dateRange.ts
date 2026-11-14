function toIso(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function todayIso(): string {
  return toIso(new Date())
}

export function firstOfMonthIso(): string {
  const now = new Date()
  return toIso(new Date(now.getFullYear(), now.getMonth(), 1))
}

export function addDaysIso(iso: string, days: number): string {
  const [year, month, day] = iso.split('-').map(Number)
  return toIso(new Date(year, month - 1, day + days))
}

export function formatIsoDate(iso: string): string {
  const [year, month, day] = iso.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  if (Number.isNaN(date.getTime())) {
    return iso
  }
  return date.toLocaleDateString('id-ID', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })
}

export function formatPeriodLabel(
  period: string,
  groupBy: 'day' | 'month',
): string {
  if (groupBy === 'month') {
    const [year, month] = period.split('-').map(Number)
    return new Date(year, month - 1, 1).toLocaleDateString('id-ID', {
      month: 'short',
      year: 'numeric',
    })
  }

  return formatIsoDate(period)
}
