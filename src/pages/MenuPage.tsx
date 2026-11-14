import { Coffee, Search } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { ProductCard } from '../components/public/ProductCard'
import { ProductDetailModal } from '../components/public/ProductDetailModal'
import { EmptyState } from '../components/ui/EmptyState'
import { getPublicCategories, getPublicProducts } from '../lib/catalog'
import type { Product } from '../lib/catalog'
import { useApi } from '../lib/useApi'

export function MenuPage() {
  const [category, setCategory] = useState('all')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null)

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

  const handleSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setSearch(searchInput.trim())
  }

  return (
    <div>
      <section className="border-b border-coffee-100 bg-[#f9f4eb]">
        <div className="mx-auto max-w-6xl px-6 py-14 md:py-20">
          <p className="text-[10px] font-bold uppercase tracking-[0.28em] text-coffee-500">
            Our menu
          </p>
          <h1 className="mt-3 font-display text-5xl text-coffee-950">
            What are you craving?
          </h1>
          <p className="mt-3 max-w-xl text-sm leading-7 text-stone-600">
            Kopi, non-coffee, makanan, dan pastry yang dibuat dengan bahan pilihan.
          </p>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-6 py-10">
        <div className="mb-7 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => setCategory('all')}
              className={`rounded-full px-4 py-2 text-xs font-semibold transition ${
                category === 'all'
                  ? 'bg-coffee-900 text-white'
                  : 'bg-white text-coffee-700 ring-1 ring-coffee-100 hover:bg-coffee-50'
              }`}
            >
              Semua
            </button>
            {(categories.data ?? []).map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setCategory(item.slug)}
                className={`rounded-full px-4 py-2 text-xs font-semibold transition ${
                  category === item.slug
                    ? 'bg-coffee-900 text-white'
                    : 'bg-white text-coffee-700 ring-1 ring-coffee-100 hover:bg-coffee-50'
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
              className="w-36 bg-transparent text-xs outline-none placeholder:text-stone-400"
            />
            <button
              type="submit"
              className="text-[10px] font-bold text-coffee-700 hover:text-coffee-900"
            >
              Cari
            </button>
          </form>
        </div>

        {products.loading && (
          <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 6 }).map((_, index) => (
              <div
                key={index}
                className="h-72 animate-pulse rounded-2xl bg-coffee-100"
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
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
              {products.data?.items.map((product) => (
                <ProductCard
                  key={product.id}
                  product={product}
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
              description="Belum ada produk yang cocok dengan filter ini. Coba kategori lain atau kembali lagi nanti."
            />
          )}

        <p className="mt-8 rounded-2xl border border-coffee-100 bg-white p-4 text-xs leading-6 text-stone-500">
          Untuk memesan, scan QR yang ada di meja Anda. Pesanan akan langsung
          masuk ke kasir.
        </p>
      </section>

      {selectedProduct && (
        <ProductDetailModal
          product={selectedProduct}
          onClose={() => setSelectedProduct(null)}
        />
      )}
    </div>
  )
}
