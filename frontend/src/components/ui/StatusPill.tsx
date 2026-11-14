import type { ReactNode } from 'react'

export type StatusTone = 'green' | 'amber' | 'red' | 'gray' | 'brown'

const tones: Record<StatusTone, string> = {
  green: 'bg-emerald-50 text-emerald-700',
  amber: 'bg-amber-50 text-amber-700',
  red: 'bg-rose-50 text-rose-700',
  gray: 'bg-stone-100 text-stone-600',
  brown: 'bg-coffee-100 text-coffee-800',
}

type StatusPillProps = {
  tone?: StatusTone
  children: ReactNode
}

export function StatusPill({ tone = 'gray', children }: StatusPillProps) {
  return (
    <span
      className={`inline-flex items-center rounded-full px-2.5 py-1 text-[11px] font-semibold ${tones[tone]}`}
    >
      {children}
    </span>
  )
}
