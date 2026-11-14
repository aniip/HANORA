import { Images } from 'lucide-react'
import { ProductImage } from '../components/public/ProductImage'
import { EmptyState } from '../components/ui/EmptyState'
import { getPublicProducts } from '../lib/catalog'
import { useApi } from '../lib/useApi'

export function GalleryPage() {
  const products = useApi(() => getPublicProducts({ pageSize: 100 }), [])

  const images = (products.data?.items ?? []).filter(
    (product) => product.imageUrl,
  )

  return (
    <div>
      <section className="border-b border-coffee-100 bg-[#f9f4eb]">
        <div className="mx-auto max-w-6xl px-6 py-14 md:py-20">
          <p className="text-[10px] font-bold uppercase tracking-[0.28em] text-coffee-500">
            Gallery
          </p>
          <h1 className="mt-3 font-display text-5xl text-coffee-950">
            A space to slow down.
          </h1>
          <p className="mt-3 max-w-xl text-sm leading-7 text-stone-600">
            Potret menu dan suasana HANORA CAFE.
          </p>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-6 py-10">
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
          images.length > 0 && (
            <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {images.map((product, index) => (
                <figure
                  key={product.id}
                  className={`overflow-hidden rounded-2xl bg-white shadow-soft ${
                    index === 0 ? 'sm:col-span-2' : ''
                  }`}
                >
                  <ProductImage
                    key={product.imageUrl ?? 'fallback'}
                    src={product.imageUrl}
                    alt={product.name}
                    className={`w-full ${index === 0 ? 'h-72' : 'h-64'}`}
                    iconSize={36}
                  />
                  <figcaption className="flex items-center justify-between gap-3 px-4 py-3 text-xs">
                    <span className="font-bold text-coffee-950">
                      {product.name}
                    </span>
                    <span className="text-stone-400">
                      {product.categoryName}
                    </span>
                  </figcaption>
                </figure>
              ))}
            </div>
          )}

        {!products.loading &&
          !products.error &&
          images.length === 0 && (
            <EmptyState
              icon={Images}
              title="Galeri belum tersedia"
              description="Foto menu akan tampil di sini setelah owner menambahkan gambar produk."
            />
          )}
      </section>
    </div>
  )
}
