import { Coffee, ScanLine } from 'lucide-react'
import { Link, Outlet } from 'react-router-dom'
import {
  hasActiveTableSession,
  useTableStore,
} from '../../stores/tableStore'

export function CustomerLayout() {
  const table = useTableStore((state) => state.table)
  const sessionToken = useTableStore((state) => state.sessionToken)
  const expiresAt = useTableStore((state) => state.expiresAt)
  const active = hasActiveTableSession({ sessionToken, table, expiresAt })

  return (
    <div className="min-h-screen bg-coffee-50">
      <header className="sticky top-0 z-40 border-b border-coffee-100 bg-coffee-50/90 backdrop-blur-xl">
        <div className="mx-auto flex h-16 max-w-5xl items-center justify-between px-4 md:px-6">
          <Link to="/" className="flex items-center gap-3">
            <img
              src="/hanora-logo.jpeg"
              alt="Logo HANORA CAFE"
              className="h-10 w-10 rounded-full object-cover"
            />
            <span>
              <span className="block font-display text-base leading-none tracking-wide text-coffee-950">
                HANORA
              </span>
              <span className="mt-1 block text-[8px] tracking-[0.3em] text-coffee-600">
                CAFE
              </span>
            </span>
          </Link>

          <div className="flex items-center gap-2 text-xs">
            {active && table ? (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-coffee-900 px-3 py-1.5 font-semibold text-white">
                <Coffee size={13} />
                {table.name}
              </span>
            ) : (
              <span className="inline-flex items-center gap-1.5 rounded-full bg-amber-50 px-3 py-1.5 font-semibold text-amber-700">
                <ScanLine size={13} />
                Scan QR meja
              </span>
            )}
            <Link
              to="/"
              className="rounded-lg px-2 py-1.5 text-[11px] font-semibold text-coffee-600 hover:bg-coffee-100"
            >
              Ganti meja
            </Link>
          </div>
        </div>
      </header>

      <main>
        <Outlet />
      </main>

      <footer className="border-t border-coffee-100 bg-white">
        <div className="mx-auto flex max-w-5xl flex-col gap-1 px-4 py-5 text-[11px] text-stone-400 sm:flex-row sm:items-center sm:justify-between md:px-6">
          <p>© 2026 HANORA CAFE. Seluruh hak dilindungi.</p>
          <p>Pemesanan dari meja · Bayar di kasir</p>
        </div>
      </footer>
    </div>
  )
}
