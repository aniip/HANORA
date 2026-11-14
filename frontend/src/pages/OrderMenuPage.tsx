import {
  ArrowRight,
  Coffee,
  Minus,
  Plus,
  Search,
  ShoppingCart,
  Trash2,
} from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { OrderProductCard } from '../components/customer/OrderProductCard'
import { ProductDetailModal } from '../components/public/ProductDetailModal'
import { ProductImage } from '../components/public/ProductImage'
import { buttonClasses } from '../components/ui/buttonStyles'
import { EmptyState } from '../components/ui/EmptyState'
import { Modal } from '../components/ui/Modal'
import { getPublicCategories, getPublicProducts } from '../lib/catalog'
import type { Product } from '../lib/catalog'
import { formatRupiah } from '../lib/format'
import { useApi } from '../lib/useApi'
import { useCartStore } from '../stores/cartStore'
import { useTableStore } from '../stores/tableStore'

export function OrderMenuPage() {
  const navigate = useNavigate()
  const table = useTableStore((state) => state.table)

  const [category, setCategory] = useState('all')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null)
  const [cartOpen, setCartOpen] = useState(false)

  const categories = useApi(getPublicCategories, [])
  const products = useApi(
    () =>
      getPublicProducts({
        category: category === 'all' ? undefined : category,
        search: search || undefined,
        pageSize: 100,
      }),
    [category, search],
  )

  const items = useCartStore((state) => state.items)
  const add = useCartStore((state) => state.add)
  const increment = useCartStore((state) => state.increment)
  const decrement = useCartStore((state) => state.decrement)
  const remove = useCartStore((state) => state.remove)

  const itemCount = items.reduce((total, item) => total + item.quantity, 0)
  const subtotal = items.reduce(
    (total, item) => total + item.price * item.quantity,
    0,
  )

  const quantityOf = (productId: string) =>
    items.find((item) => item.productId === productId)?.quantity ?? 0

  const handleSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSearch(searchInput.trim())
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-7 md:px-6 md:py-9">
      <div className="rounded-3xl bg-coffee-900 p-6 text-white md:p-8">
        <p className="text-[10px] font-bold uppercase tracking-[0.26em] text-gold">
          Pesan dari meja
        </p>
        <div className="mt-3 flex flex-wrap items-end justify-between gap-3">
          <div>
            <h1 className="font-display text-4xl">
              {table?.name ?? 'Meja Anda'}
            </h1>
            <p className="mt-2 text-sm text-white/65">
              Pilih menu, lalu lanjutkan ke checkout. Pesanan langsung masuk ke
              kasir.
            </p>
          </div>
          <span className="rounded-xl border border-white/20 bg-white/10 px-4 py-2 text-xs font-semibold">
            {itemCount} item di keranjang
          </span>
        </div>
      </div>

      <div className="my-6 flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => setCategory('all')}
            className={`rounded-full px-4 py-2 text-xs font-semibold ${
              category === 'all'
                ? 'bg-coffee-900 text-white'
                : 'bg-white text-coffee-700 ring-1 ring-coffee-100'
            }`}
          >
            Semua
          </button>
          {(categories.data ?? []).map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => setCategory(item.slug)}
              className={`rounded-full px-4 py-2 text-xs font-semibold ${
                category === item.slug
                  ? 'bg-coffee-900 text-white'
                  : 'bg-white text-coffee-700 ring-1 ring-coffee-100'
              }`}
            >
              {item.name}
            </button>
          ))}
        </div>

        <form
          onSubmit={handleSearch}
          className="flex items-center gap-2 rounded-xl border border-coffee-100 bg-white px-3 py-2"
        >
          <Search size={15} className="text-stone-400" />
          <input
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder="Cari menu..."
            className="w-32 bg-transparent text-xs outline-none placeholder:text-stone-400"
          />
          <button
            type="submit"
            className="text-[10px] font-bold text-coffee-700"
          >
            Cari
          </button>
        </form>
      </div>

      {products.loading && (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 6 }).map((_, index) => (
            <div
              key={index}
              className="h-64 animate-pulse rounded-2xl bg-coffee-100"
            />
          ))}
        </div>
      )}

      {!products.loading && products.error && (
        <div className="rounded-2xl border border-rose-100 bg-rose-50 p-5 text-sm text-rose-700">
          {products.error}{' '}
          <button
            type="button"
            onClick={products.reload}
            className="font-bold underline"
          >
            Coba lagi
          </button>
        </div>
      )}

      {!products.loading &&
        !products.error &&
        (products.data?.items.length ?? 0) > 0 && (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {products.data?.items.map((product) => (
              <OrderProductCard
                key={product.id}
                product={product}
                quantity={quantityOf(product.id)}
                onAdd={add}
                onDetail={setSelectedProduct}
              />
            ))}
          </div>
        )}

      {!products.loading &&
        !products.error &&
        (products.data?.items.length ?? 0) === 0 && (
          <EmptyState
            icon={Coffee}
            title="Menu belum tersedia"
            description="Owner belum menambahkan produk untuk kategori ini."
          />
        )}

      <button
        type="button"
        onClick={() => setCartOpen(true)}
        className="fixed bottom-5 right-5 z-30 flex items-center gap-3 rounded-2xl bg-coffee-900 px-5 py-4 text-white shadow-2xl"
      >
        <ShoppingCart size={18} />
        <span className="text-sm font-bold">Keranjang · {itemCount}</span>
        <span className="rounded-lg bg-white/15 px-2 py-1 text-xs">
          {formatRupiah(subtotal)}
        </span>
      </button>

      {cartOpen && (
        <Modal title="Keranjang pesanan" onClose={() => setCartOpen(false)} wide>
          {items.length === 0 ? (
            <p className="py-8 text-center text-sm text-stone-500">
              Keranjang masih kosong. Pilih menu terlebih dahulu.
            </p>
          ) : (
            <>
              <div className="space-y-4">
                {items.map((item) => (
                  <div key={item.productId} className="flex items-center gap-3">
                    <ProductImage
                      key={item.imageUrl ?? 'fallback'}
                      src={item.imageUrl}
                      alt={item.name}
                      className="h-14 w-14 rounded-xl"
                      iconSize={20}
                    />
                    <div className="min-w-0 flex-1">
                      <p className="text-sm font-bold text-coffee-950">
                        {item.name}
                      </p>
                      <p className="text-xs text-stone-500">
                        {formatRupiah(item.price)}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => decrement(item.productId)}
                        aria-label={`Kurangi ${item.name}`}
                        className="grid h-7 w-7 place-items-center rounded-lg bg-coffee-100 text-coffee-800"
                      >
                        <Minus size={13} />
                      </button>
                      <b className="w-5 text-center text-sm">
                        {item.quantity}
                      </b>
                      <button
                        type="button"
                        onClick={() => increment(item.productId)}
                        aria-label={`Tambah ${item.name}`}
                        className="grid h-7 w-7 place-items-center rounded-lg bg-coffee-100 text-coffee-800"
                      >
                        <Plus size={13} />
                      </button>
                      <button
                        type="button"
                        onClick={() => remove(item.productId)}
                        aria-label={`Hapus ${item.name}`}
                        className="ml-1 rounded-lg p-1.5 text-rose-500 hover:bg-rose-50"
                      >
                        <Trash2 size={14} />
                      </button>
                    </div>
                  </div>
                ))}
              </div>

              <div className="mt-5 border-t border-coffee-100 pt-4">
                <div className="flex justify-between text-sm">
                  <span className="text-stone-500">Subtotal</span>
                  <b className="text-coffee-950">{formatRupiah(subtotal)}</b>
                </div>
                <p className="mt-1 text-[10px] text-stone-400">
                  PB1 10% dihitung pada halaman checkout.
                </p>
                <button
                  type="button"
                  onClick={() => {
                    setCartOpen(false)
                    navigate('/checkout')
                  }}
                  className={buttonClasses('primary', 'mt-4 w-full')}
                >
                  Lanjut checkout
                  <ArrowRight size={16} />
                </button>
              </div>
            </>
          )}
        </Modal>
      )}

      {selectedProduct && (
        <ProductDetailModal
          product={selectedProduct}
          onClose={() => setSelectedProduct(null)}
        />
      )}
    </div>
  )
}
