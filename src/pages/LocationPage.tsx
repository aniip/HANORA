import { AtSign, Clock3, ExternalLink, MapPin, Phone } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { buttonClasses } from '../components/ui/buttonStyles'
import { getPublicSite } from '../lib/catalog'
import { useApi } from '../lib/useApi'

function InfoRow({
  icon: Icon,
  label,
  value,
}: {
  icon: LucideIcon
  label: string
  value: string
}) {
  return (
    <div className="flex gap-3 rounded-2xl border border-coffee-100 bg-white p-4">
      <div className="rounded-xl bg-coffee-100 p-2 text-coffee-700">
        <Icon size={18} />
      </div>
      <div>
        <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
          {label}
        </p>
        <p className="mt-1 text-sm leading-6 text-coffee-950">
          {value || 'Belum diatur owner.'}
        </p>
      </div>
    </div>
  )
}

export function LocationPage() {
  const site = useApi(getPublicSite, [])

  const instagram = site.data?.instagram?.replace(/^@/, '').trim() ?? ''
  const instagramUrl = instagram ? `https://instagram.com/${instagram}` : null
  const mapsUrl = site.data?.mapsUrl?.trim() ?? ''

  return (
    <div>
      <section className="border-b border-coffee-100 bg-[#f9f4eb]">
        <div className="mx-auto max-w-6xl px-6 py-14 md:py-20">
          <p className="text-[10px] font-bold uppercase tracking-[0.28em] text-coffee-500">
            Visit us
          </p>
          <h1 className="mt-3 font-display text-5xl text-coffee-950">
            Come say hello.
          </h1>
          <p className="mt-3 max-w-xl text-sm leading-7 text-stone-600">
            Temukan HANORA CAFE, jam operasional, dan cara menghubungi kami.
          </p>
        </div>
      </section>

      <section className="mx-auto max-w-6xl px-6 py-10">
        {site.loading && (
          <div className="grid gap-4 md:grid-cols-2">
            {Array.from({ length: 4 }).map((_, index) => (
              <div
                key={index}
                className="h-24 animate-pulse rounded-2xl bg-coffee-100"
              />
            ))}
          </div>
        )}

        {!site.loading && site.error && (
          <div className="rounded-2xl border border-rose-100 bg-rose-50 p-5 text-sm text-rose-700">
            {site.error}{' '}
            <button
              type="button"
              onClick={site.reload}
              className="font-bold underline"
            >
              Coba lagi
            </button>
          </div>
        )}

        {!site.loading && !site.error && (
          <div className="grid gap-5 lg:grid-cols-[1fr_0.9fr]">
            <div className="space-y-4">
              <InfoRow
                icon={MapPin}
                label="Alamat"
                value={site.data?.address ?? ''}
              />
              <InfoRow
                icon={Clock3}
                label="Jam operasional"
                value={site.data?.openingHours ?? ''}
              />
              <InfoRow
                icon={Phone}
                label="Telepon"
                value={site.data?.phone ?? ''}
              />
              <InfoRow
                icon={AtSign}
                label="Instagram"
                value={
                  site.data?.instagram
                    ? `@${instagram}`
                    : ''
                }
              />
            </div>

            <div className="rounded-3xl border border-coffee-100 bg-coffee-900 p-6 text-white">
              <div className="grid h-40 place-items-center rounded-2xl border border-white/10 bg-white/5">
                <MapPin size={42} className="text-gold" />
              </div>
              <h2 className="mt-5 font-display text-2xl">
                {site.data?.name ?? 'HANORA CAFE'}
              </h2>
              <p className="mt-2 text-xs leading-6 text-white/60">
                {site.data?.address ||
                  'Alamat akan ditampilkan setelah diatur owner.'}
              </p>

              <div className="mt-5 flex flex-wrap gap-2">
                {mapsUrl && (
                  <a
                    href={mapsUrl}
                    target="_blank"
                    rel="noreferrer"
                    className={buttonClasses('gold', '!px-4 !py-2')}
                  >
                    Buka Google Maps
                    <ExternalLink size={14} />
                  </a>
                )}
                {instagramUrl && (
                  <a
                    href={instagramUrl}
                    target="_blank"
                    rel="noreferrer"
                    className={buttonClasses('inverse', '!px-4 !py-2')}
                  >
                    Instagram
                    <ExternalLink size={14} />
                  </a>
                )}
              </div>
            </div>
          </div>
        )}
      </section>
    </div>
  )
}
