import type { ReactNode } from 'react'

type SectionTitleProps = {
  eyebrow?: string
  title: string
  description?: string
  action?: ReactNode
}

export function SectionTitle({
  eyebrow,
  title,
  description,
  action,
}: SectionTitleProps) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        {eyebrow && (
          <p className="text-[10px] font-bold uppercase tracking-[0.22em] text-coffee-500">
            {eyebrow}
          </p>
        )}
        <h1 className="mt-2 font-display text-3xl text-coffee-950">{title}</h1>
        {description && (
          <p className="mt-2 max-w-2xl text-sm leading-6 text-stone-500">
            {description}
          </p>
        )}
      </div>
      {action}
    </div>
  )
}
