export type ButtonVariant = 'primary' | 'outline' | 'ghost' | 'gold' | 'inverse'

const base =
  'inline-flex items-center justify-center gap-2 rounded-xl px-5 py-3 text-sm font-semibold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-gold focus-visible:ring-offset-2'

const variants: Record<ButtonVariant, string> = {
  primary: 'bg-coffee-900 text-white hover:bg-coffee-800',
  outline:
    'border border-coffee-200 bg-transparent text-coffee-800 hover:bg-coffee-100',
  ghost: 'text-coffee-700 hover:bg-coffee-100',
  gold: 'bg-gold text-coffee-950 hover:bg-[#f0d2ad]',
  inverse:
    'border border-white/30 bg-transparent text-white hover:bg-white/10',
}

export function buttonClasses(
  variant: ButtonVariant = 'primary',
  className = '',
): string {
  return `${base} ${variants[variant]} ${className}`.trim()
}
