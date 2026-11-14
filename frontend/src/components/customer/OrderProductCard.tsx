import { Plus } from 'lucide-react'
import type { Product } from '../../lib/catalog'
import { formatRupiah } from '../../lib/format'
import { ProductImage } from '../public/ProductImage'

type OrderProductCardProps = {
  product: Product
  quantity: number
  onAdd: (product: Product) => void
  onDetail: (product: Product) => void
}

export function OrderProductCard({
  product,
  quantity,
  onAdd,
  onDetail,
}: OrderProductCardProps) {
  return (
    <article className="overflow-hidden rounded-2xl border border-coffee-100 bg-white shadow-soft">
      <button
        type="button"
        onClick={() => onDetail(product)}
        className="block w-full text-left"
      >
        <ProductImage
          key={product.imageUrl ?? 'fallback'}
          src={product.imageUrl}
          alt={product.name}
          className="h-40 w-full"
        />
      </button>
      <div className="p-4">
        <p className="text-[10px] font-bold uppercase tracking-[0.16em] text-coffee-500">
          {product.categoryName}
        </p>
        <h3 className="mt-1 text-sm font-bold text-coffee-950">
          {product.name}
        </h3>
        {product.description && (
          <p className="mt-1 line-clamp-2 text-xs leading-5 text-stone-500">
            {product.description}
          </p>
        )}
        <div className="mt-3 flex items-center justify-between gap-2">
          <span className="text-sm font-bold text-coffee-800">
            {formatRupiah(product.price)}
          </span>
          <button
            type="button"
            onClick={() => onAdd(product)}
            className="inline-flex items-center gap-1 rounded-lg bg-coffee-900 px-3 py-2 text-[11px] font-semibold text-white hover:bg-coffee-800"
          >
            <Plus size={14} />
            Tambah
            {quantity > 0 && (
              <span className="ml-1 rounded-full bg-white/20 px-1.5 py-0.5 text-[10px]">
                {quantity}
              </span>
            )}
          </button>
        </div>
      </div>
    </article>
  )
}
