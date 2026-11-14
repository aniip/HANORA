import type { Product } from '../../lib/catalog'
import { formatRupiah } from '../../lib/format'
import { Modal } from '../ui/Modal'
import { ProductImage } from './ProductImage'

type ProductDetailModalProps = {
  product: Product
  onClose: () => void
}

export function ProductDetailModal({
  product,
  onClose,
}: ProductDetailModalProps) {
  return (
    <Modal title={product.name} onClose={onClose} wide>
      <div className="grid gap-6 md:grid-cols-[1fr_1.1fr]">
        <ProductImage
          key={product.imageUrl ?? 'fallback'}
          src={product.imageUrl}
          alt={product.name}
          className="h-64 w-full rounded-2xl"
          iconSize={42}
        />

        <div>
          <p className="text-[10px] font-bold uppercase tracking-[0.2em] text-coffee-500">
            {product.categoryName}
          </p>
          <p className="mt-3 font-display text-3xl text-coffee-950">
            {formatRupiah(product.price)}
          </p>
          <p className="mt-4 text-sm leading-7 text-stone-600">
            {product.description ?? 'Deskripsi produk belum ditambahkan.'}
          </p>

          <div className="mt-6 rounded-2xl border border-coffee-100 bg-coffee-50 p-4 text-xs leading-6 text-coffee-800">
            Untuk memesan, scan QR yang ada di meja Anda, lalu tambahkan menu ke
            keranjang.
          </div>
        </div>
      </div>
    </Modal>
  )
}
