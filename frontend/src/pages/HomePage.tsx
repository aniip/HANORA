import {
  ArrowRight,
  Clock3,
  Coffee,
  Images,
  MapPin,
  Phone,
  Sparkles,
} from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ProductCard } from '../components/public/ProductCard'
import { ProductDetailModal } from '../components/public/ProductDetailModal'
import { ProductImage } from '../components/public/ProductImage'
import { buttonClasses } from '../components/ui/buttonStyles'
import { EmptyState } from '../components/ui/EmptyState'
import { getPublicProducts, getPublicSite } from '../lib/catalog'
import type { Product } from '../lib/catalog'
import { useApi } from '../lib/useApi'

function ProductSkeleton() {
  return (
    <div className="overflow-hidden rounded-2xl border border-coffee-100 bg-white">
      <div className="h-48 animate-pulse bg-coffee-100" />
      <div className="space-y-3 p-4">
        <div className="h-3 w-20 animate-pulse rounded bg-coffee-100" />
        <div className="h-4 w-40 animate-pulse rounded bg-coffee-100" />
        <div className="h-3 w-full animate-pulse rounded bg-coffee-100" />
      </div>
    </div>
  )
}

export function HomePage() {
  const site = useApi(getPublicSite, [])
  const featured = useApi(() => getPublicProducts({ pageSize: 4 }), [])
  const gallery = useApi(() => getPublicProducts({ pageSize: 100 }), [])
  const [selectedProduct, setSelectedProduct] = useState<Product | null>(null)

  const galleryProducts = (gallery.data?.items ?? []).filter(
    (product) => product.imageUrl,
  )

  return (
    <div>
      <section className="relative overflow-hidden bg-coffee-950 text-white">
        <div className="pointer-events-none absolute -left-24 top-10 h-72 w-72 rounded-full bg-coffee-700/40 blur-3xl" />
        <div className="pointer-events-none absolute -right-16 bottom-0 h-96 w-96 rounded-full bg-caramel/20 blur-3xl" />
        <div className="pointer-events-none absolute right-8 top-12 hidden opacity-10 lg:block">
          <Coffee size={260} strokeWidth={0.7} />
        </div>

        <div className="relative mx-auto max-w-6xl px-6 py-20 md:py-28">
          <div className="max-w-2xl">
            <p className="flex items-center gap-2 text-[10px] font-bold uppercase tracking-[0.32em] text-gold">
              <span className="h-px w-8 bg-gold" />
              {site.data?.name ?? 'HANORA CAFE'} · Coffee & Comfort
            </p>

            <h1 className="mt-6 font-display text-5xl leading-[1.08] md:text-7xl">
              Find your
              <br />
              <em className="font-medium text-gold">slow moment.</em>
            </h1>

            <p className="mt-6 max-w-xl text-sm leading-7 text-white/70">
              {site.data?.tagline ??
                'A little pause, a lot of warmth.'}{' '}
              {site.data?.description ??
                'Tempat singgah untuk secangkir kopi yang dibuat dengan hati.'}
            </p>

            <div className="mt-8 flex flex-wrap gap-3">
              <Link to="/menu" className={buttonClasses('gold')}>
                Jelajahi menu
                <ArrowRight size={16} />
              </Link>
              <Link to="/lokasi" className={buttonClasses('inverse')}>
                Lokasi & jam buka
              </Link>
            </div>
          </div>
        </div>

        <div className="relative border-t border-white/10">
          <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3 px-6 py-4 text-[10px] tracking-[0.18em] text-white/45">
            <span>COFFEE · COMFORT · CONNECTION</span>
            <span>EST. 2026</span>
          </div>
        </div>
      </section>

      {site.error && (
        <div className="mx-auto max-w-6xl px-6 pt-8">
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-rose-100 bg-rose-50 p-4 text-xs text-rose-700">
            <span>Informasi bisnis belum dapat dimuat: {site.error}</span>
            <button
              type="button"
              onClick={site.reload}
              className="font-bold underline"
            >
              Coba lagi
            </button>
          </div>
        </div>
      )}

      <section className="mx-auto grid max-w-6xl gap-10 px-6 py-16 md:grid-cols-2 md:py-24">
        <div>
          <p className="text-[10px] font-bold uppercase tracking-[0.25em] text-coffee-500">
            The Hanora story
          </p>
          <h2 className="mt-3 font-display text-4xl leading-tight text-coffee-950">
            More than coffee.
            <br />
            A place to belong.
          </h2>
        </div>
        <div className="self-end text-sm leading-7 text-stone-600">
          <p>
            {site.data?.description ??
              'Hanora Coffee menghadirkan pengalaman menikmati kopi dalam suasana hangat dan bersahaja. Setiap racikan, sajian, dan sudut ruang dirancang untuk menemani momen sederhana menjadi lebih berarti.'}
          </p>
          <p className="mt-4 font-display text-xl italic text-coffee-700">
            “Take a breath. Take a sip.”
          </p>
        </div>
      </section>

      <section className="border-y border-coffee-100 bg-white">
        <div className="mx-auto max-w-6xl px-6 py-16">
          <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
            <div>
              <p className="text-[10px] font-bold uppercase tracking-[0.25em] text-coffee-500">
                Curated for you
              </p>
              <h2 className="mt-2 font-display text-4xl text-coffee-950">
                Our favorites
              </h2>
              <p className="mt-2 text-sm text-stone-500">
                Pilihan menu yang paling sering dinikmati di HANORA CAFE.
              </p>
            </div>
            <Link
              to="/menu"
              className="inline-flex items-center gap-1 text-xs font-bold text-coffee-700 hover:text-coffee-900"
            >
              Lihat semua menu
              <ArrowRight size={14} />
            </Link>
          </div>

          {featured.loading && (
            <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
              {Array.from({ length: 4 }).map((_, index) => (
                <ProductSkeleton key={index} />
              ))}
            </div>
          )}

          {!featured.loading && featured.error && (
            <div className="rounded-2xl border border-rose-100 bg-rose-50 p-5 text-sm text-rose-700">
              {featured.error}{' '}
              <button
                type="button"
                onClick={featured.reload}
                className="font-bold underline"
              >
                Coba lagi
              </button>
            </div>
          )}

          {!featured.loading &&
            !featured.error &&
            (featured.data?.items.length ?? 0) > 0 && (
              <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                {featured.data?.items.map((product) => (
                  <ProductCard
                    key={product.id}
                    product={product}
                    onDetail={setSelectedProduct}
                  />
                ))}
              </div>
            )}

          {!featured.loading &&
            !featured.error &&
            (featured.data?.items.length ?? 0) === 0 && (
              <EmptyState
                icon={Coffee}
                title="Menu belum tersedia"
                description="Owner belum menambahkan produk. Menu akan tampil otomatis setelah katalog diisi."
              />
            )}
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-6 py-16">
        <div className="mb-7 flex flex-wrap items-end justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.25em] text-coffee-500">
              The atmosphere
            </p>
            <h2 className="mt-2 font-display text-4xl text-coffee-950">
              A space to slow down.
            </h2>
          </div>
          <Link
            to="/galeri"
            className="inline-flex items-center gap-1 text-xs font-bold text-coffee-700 hover:text-coffee-900"
          >
            Buka galeri
            <ArrowRight size={14} />
          </Link>
        </div>

        {galleryProducts.length > 0 ? (
          <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
            {galleryProducts.slice(0, 4).map((product, index) => (
              <div
                key={product.id}
                className={
                  index === 0
                    ? 'md:col-span-2 md:row-span-1'
                    : ''
                }
              >
                <ProductImage
                  key={product.imageUrl ?? 'fallback'}
                  src={product.imageUrl}
                  alt={product.name}
                  className="h-48 w-full rounded-2xl md:h-64"
                  iconSize={36}
                />
              </div>
            ))}
          </div>
        ) : (
          <EmptyState
            icon={Images}
            title="Galeri belum tersedia"
            description="Foto menu akan tampil di sini setelah owner menambahkan gambar produk."
          />
        )}
      </section>

      <section className="bg-coffee-900 text-white">
        <div className="mx-auto grid max-w-6xl gap-8 px-6 py-14 md:grid-cols-3">
          <div>
            <div className="flex items-center gap-3">
              <img
                src={site.data?.logoUrl ?? '/hanora-logo.jpeg'}
                alt="Logo HANORA CAFE"
                className="h-12 w-12 rounded-full object-cover"
              />
              <div>
                <p className="font-display text-xl leading-none">
                  {site.data?.name ?? 'HANORA CAFE'}
                </p>
                <p className="mt-1 text-[9px] tracking-[0.3em] text-white/50">
                  CAFE
                </p>
              </div>
            </div>
            <p className="mt-4 text-xs leading-6 text-white/60">
              A little pause, a lot of warmth.
            </p>
          </div>

          <div>
            <h3 className="font-display text-xl">Come visit</h3>
            <p className="mt-3 flex gap-2 text-xs leading-6 text-white/70">
              <MapPin size={15} className="mt-0.5 shrink-0" />
              {site.data?.address || 'Alamat akan ditampilkan setelah diatur owner.'}
            </p>
            <p className="mt-2 flex gap-2 text-xs leading-6 text-white/70">
              <Clock3 size={15} className="mt-0.5 shrink-0" />
              {site.data?.openingHours || 'Jam operasional belum diatur.'}
            </p>
            {site.data?.phone && (
              <p className="mt-2 flex gap-2 text-xs leading-6 text-white/70">
                <Phone size={15} className="mt-0.5 shrink-0" />
                {site.data.phone}
              </p>
            )}
          </div>

          <div>
            <h3 className="font-display text-xl">Stay connected</h3>
            <p className="mt-3 text-xs leading-6 text-white/70">
              {site.data?.instagram
                ? `@${site.data.instagram.replace(/^@/, '')}`
                : 'Instagram belum diatur.'}
            </p>
            <div className="mt-4">
              <Link to="/lokasi" className={buttonClasses('inverse', '!px-4 !py-2')}>
                <MapPin size={14} />
                Lihat lokasi
              </Link>
            </div>
            <p className="mt-6 flex items-center gap-2 text-[10px] text-white/40">
              <Sparkles size={13} />
              Coffee · Comfort · Connection
            </p>
          </div>
        </div>
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
