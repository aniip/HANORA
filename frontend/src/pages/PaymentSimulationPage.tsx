import {
  AlertTriangle,
  ArrowLeft,
  CheckCircle2,
  Loader2,
  QrCode,
  RefreshCw,
} from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { StatusPill } from '../components/ui/StatusPill'
import { getApiErrorMessage } from '../lib/apiError'
import { formatRupiah } from '../lib/format'
import { simulateQrisPayment, trackOrder } from '../lib/orders'
import type { OrderTracking } from '../lib/orders'

/**
 * Halaman ini hanya muncul pada mode Midtrans simulation. Tidak ada uang
 * yang benar-benar berpindah; tombol di sini menandai pesanan sebagai lunas
 * agar alur kasir dan tracking dapat diuji tanpa akun Midtrans.
 */
export function PaymentSimulationPage() {
  const { trackingToken } = useParams<{ trackingToken: string }>()
  const navigate = useNavigate()

  const [order, setOrder] = useState<OrderTracking | null>(null)
  const [loading, setLoading] = useState(true)
  const [paying, setPaying] = useState(false)
  const [error, setError] = useState('')

  const load = useCallback(async () => {
    if (!trackingToken) {
      setError('Token pesanan tidak ditemukan.')
      setLoading(false)
      return
    }

    try {
      const data = await trackOrder(trackingToken)
      setOrder(data)
      setError('')
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setLoading(false)
    }
  }, [trackingToken])

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- memuat data eksternal
    void load()
  }, [load])

  const handleSimulate = async () => {
    if (!trackingToken) {
      return
    }

    setPaying(true)
    setError('')

    try {
      await simulateQrisPayment(trackingToken)
      navigate(`/pesanan/${trackingToken}`, { replace: true })
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setPaying(false)
    }
  }

  if (loading) {
    return (
      <div className="grid min-h-[50vh] place-items-center">
        <div className="flex items-center gap-2 text-sm text-coffee-700">
          <Loader2 size={18} className="animate-spin" />
          Memuat data pembayaran...
        </div>
      </div>
    )
  }

  if (!order) {
    return (
      <section className="mx-auto max-w-xl px-4 py-20 text-center">
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-rose-50 text-rose-600">
          <AlertTriangle size={28} />
        </div>
        <h1 className="mt-5 font-display text-3xl text-coffee-950">
          Pesanan tidak ditemukan.
        </h1>
        <p className="mt-3 text-sm leading-7 text-stone-500">
          {error || 'Periksa kembali tautan pesanan Anda.'}
        </p>
        <Link to="/" className={buttonClasses('primary', 'mt-6')}>
          <ArrowLeft size={16} />
          Kembali ke beranda
        </Link>
      </section>
    )
  }

  const isPayable =
    order.payment.method === 'qris' &&
    order.payment.status === 'pending' &&
    order.status === 'pending_payment'

  if (!isPayable) {
    return (
      <section className="mx-auto max-w-xl px-4 py-20 text-center">
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-emerald-50 text-emerald-600">
          <CheckCircle2 size={28} />
        </div>
        <h1 className="mt-5 font-display text-3xl text-coffee-950">
          Pembayaran tidak diperlukan.
        </h1>
        <p className="mt-3 text-sm leading-7 text-stone-500">
          Pesanan ini sudah dibayar atau tidak menggunakan QRIS.
        </p>
        <Link
          to={`/pesanan/${trackingToken}`}
          className={buttonClasses('primary', 'mt-6')}
        >
          <ArrowLeft size={16} />
          Lihat pesanan
        </Link>
      </section>
    )
  }

  return (
    <div className="mx-auto max-w-xl px-4 py-8 md:px-6 md:py-10">
      <Link
        to={`/pesanan/${trackingToken}`}
        className="inline-flex items-center gap-2 text-xs font-semibold text-coffee-600"
      >
        <ArrowLeft size={14} />
        Kembali ke pesanan
      </Link>

      <div className="mt-4 flex items-start gap-3 rounded-2xl border border-amber-200 bg-amber-50 p-4 text-amber-900">
        <AlertTriangle size={20} className="mt-0.5 shrink-0" />
        <div>
          <p className="text-sm font-bold">Mode simulasi</p>
          <p className="mt-1 text-xs leading-5">
            Ini bukan pembayaran nyata. Halaman ini hanya tersedia saat Midtrans
            berjalan pada mode <b>simulation</b> untuk pengujian alur QRIS.
          </p>
        </div>
      </div>

      <div className="mt-5 rounded-3xl border border-coffee-100 bg-white p-6 text-center shadow-soft">
        <div className="flex items-center justify-between gap-3">
          <div className="text-left">
            <p className="text-[10px] font-bold uppercase tracking-[0.22em] text-coffee-500">
              {order.orderNumber}
            </p>
            <h1 className="mt-1 font-display text-2xl text-coffee-950">
              Pembayaran QRIS
            </h1>
          </div>
          <StatusPill tone="amber">Simulasi</StatusPill>
        </div>

        <div className="mx-auto mt-6 grid h-52 w-52 place-items-center rounded-2xl border-4 border-dashed border-coffee-200 bg-coffee-50 text-coffee-400">
          <QrCode size={120} strokeWidth={1.2} />
        </div>

        <p className="mt-4 text-xs text-stone-500">
          QR ini hanya ilustrasi. Pada mode sandbox/production, customer
          diarahkan ke halaman pembayaran Midtrans.
        </p>

        <div className="mt-6 rounded-2xl bg-coffee-900 p-4 text-white">
          <p className="text-[10px] uppercase tracking-[0.2em] text-white/50">
            Total tagihan
          </p>
          <p className="mt-1 font-display text-3xl">
            {formatRupiah(order.total)}
          </p>
          <p className="mt-1 text-xs text-white/50">
            {order.customerName} · {order.table.name}
          </p>
        </div>

        {error && (
          <div
            role="alert"
            className="mt-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-left text-xs leading-5 text-rose-700"
          >
            <AlertTriangle size={16} className="mt-0.5 shrink-0" />
            {error}
          </div>
        )}

        <div className="mt-5 flex flex-col gap-2">
          <button
            type="button"
            onClick={() => void handleSimulate()}
            disabled={paying}
            className={buttonClasses('primary', 'w-full disabled:opacity-60')}
          >
            {paying ? (
              <Loader2 size={16} className="animate-spin" />
            ) : (
              <CheckCircle2 size={16} />
            )}
            {paying
              ? 'Memproses simulasi...'
              : 'Simulasikan pembayaran berhasil'}
          </button>
          <button
            type="button"
            onClick={() => void load()}
            className={buttonClasses('outline', 'w-full')}
          >
            <RefreshCw size={15} />
            Muat ulang status
          </button>
        </div>
      </div>
    </div>
  )
}
