import { AlertCircle, Loader2, QrCode } from 'lucide-react'
import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { resolveTable } from '../lib/orders'
import { getApiErrorMessage } from '../lib/apiError'
import { useCartStore } from '../stores/cartStore'
import { useTableStore } from '../stores/tableStore'

export function TableLandingPage() {
  const { qrToken } = useParams<{ qrToken: string }>()
  const navigate = useNavigate()
  const setSession = useTableStore((state) => state.setSession)
  const clearSession = useTableStore((state) => state.clearSession)
  const setTable = useCartStore((state) => state.setTable)
  const [error, setError] = useState(
    qrToken ? '' : 'Token QR tidak ditemukan.',
  )
  const [loading, setLoading] = useState(Boolean(qrToken))

  useEffect(() => {
    let active = true

    if (!qrToken) {
      return () => {
        active = false
      }
    }

    resolveTable(qrToken)
      .then((session) => {
        if (!active) {
          return
        }

        setTable(session.table.id)
        setSession(session)
        navigate('/pesan', { replace: true })
      })
      .catch((exception: unknown) => {
        if (active) {
          clearSession()
          setError(getApiErrorMessage(exception))
          setLoading(false)
        }
      })

    return () => {
      active = false
    }
  }, [clearSession, navigate, qrToken, setSession, setTable])

  return (
    <section className="mx-auto max-w-xl px-4 py-20 text-center">
      <div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-coffee-100 text-coffee-600">
        {loading ? (
          <Loader2 size={28} className="animate-spin" />
        ) : (
          <QrCode size={28} />
        )}
      </div>

      <h1 className="mt-5 font-display text-3xl text-coffee-950">
        {loading ? 'Menyiapkan meja...' : 'QR meja tidak berlaku'}
      </h1>

      {loading ? (
        <p className="mt-3 text-sm leading-7 text-stone-500">
          Memvalidasi QR dan menyiapkan sesi pemesanan.
        </p>
      ) : (
        <>
          <p className="mt-3 flex items-center justify-center gap-2 text-sm leading-7 text-rose-700">
            <AlertCircle size={16} />
            {error || 'Silakan hubungi kasir untuk mendapatkan QR terbaru.'}
          </p>
          <Link to="/" className={buttonClasses('primary', 'mt-6')}>
            Kembali ke beranda
          </Link>
        </>
      )}
    </section>
  )
}
