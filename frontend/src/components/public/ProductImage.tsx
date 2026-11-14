import { Coffee } from 'lucide-react'
import { useState } from 'react'

type ProductImageProps = {
  src?: string | null
  alt: string
  className?: string
  iconSize?: number
}

export function ProductImage({
  src,
  alt,
  className = '',
  iconSize = 28,
}: ProductImageProps) {
  const [failed, setFailed] = useState(false)
  const showImage = Boolean(src) && !failed

  if (!showImage) {
    return (
      <div
        className={`grid place-items-center bg-coffee-100 text-coffee-500 ${className}`}
        role="img"
        aria-label={alt}
      >
        <Coffee size={iconSize} strokeWidth={1.5} />
      </div>
    )
  }

  return (
    <img
      src={src ?? undefined}
      alt={alt}
      loading="lazy"
      onError={() => setFailed(true)}
      className={`object-cover ${className}`}
    />
  )
}
