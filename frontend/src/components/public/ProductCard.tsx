import { ArrowRight } from 'lucide-react'
import type { Product } from '../../lib/catalog'
import { formatRupiah } from '../../lib/format'
import { ProductImage } from './ProductImage'

type ProductCardProps = {
  product: Product
  onDetail: (product: Product) => void
}

export function ProductCard({ product, onDetail }: ProductCardProps) {
  return (
    <button
      type="button"
      onClick={() => onDetail(product)}
      className="group overflow-hidden rounded-2xl border border-coffee-100 bg-white text-left shadow-soft transition hover:-translate-y-0.5 hover:border-coffee-200"
    >
      <div className="overflow-hidden">
        <ProductImage
          key={product.imageUrl ?? 'fallback'}
          src={product.imageUrl}
          alt={product.name}
          className="h-48 w-full transition duration-500 group-hover:scale-105"
        />
      </div>
      <div className="p-4">
        <div className="flex items-start justify-between gap-3">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-coffee-500">
              {product.categoryName}
            </p>
            <h3 className="mt-1 text-sm font-bold text-coffee-950">
              {product.name}
            </h3>
          </div>
          <p className="whitespace-nowrap text-xs font-bold text-coffee-800">
            {formatRupiah(product.price)}
          </p>
        </div>
        {product.description && (
          <p className="mt-2 line-clamp-2 text-xs leading-5 text-stone-500">
            {product.description}
          </p>
        )}
        <span className="mt-3 inline-flex items-center gap-1 text-[11px] font-bold text-coffee-700">
          Lihat detail
          <ArrowRight size={13} />
        </span>
      </div>
    </button>
  )
}
