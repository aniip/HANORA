import type { ReactNode } from 'react'
import { QrCode } from 'lucide-react'
import { Link } from 'react-router-dom'
import { buttonClasses } from '../ui/buttonStyles'
import {
  hasActiveTableSession,
  useTableStore,
} from '../../stores/tableStore'

export function RequireTableSession({ children }: { children: ReactNode }) {
  const table = useTableStore((state) => state.table)
  const sessionToken = useTableStore((state) => state.sessionToken)
  const expiresAt = useTableStore((state) => state.expiresAt)

  if (!hasActiveTableSession({ sessionToken, table, expiresAt })) {
    return (
      <section className="mx-auto max-w-xl px-4 py-20 text-center">
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-coffee-100 text-coffee-600">
          <QrCode size={28} />
        </div>
        <h1 className="mt-5 font-display text-3xl text-coffee-950">
          Sesi meja berakhir.
        </h1>
        <p className="mt-3 text-sm leading-7 text-stone-500">
          Silakan scan ulang QR yang ada di meja untuk melanjutkan pemesanan.
        </p>
        <Link to="/" className={buttonClasses('primary', 'mt-6')}>
          Kembali ke beranda
        </Link>
      </section>
    )
  }

  return <>{children}</>
}
