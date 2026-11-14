import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  Clock3,
  Loader2,
  QrCode,
  RefreshCw,
  ShoppingBag,
  XCircle,
} from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { Modal } from '../components/ui/Modal'
import { StatusPill } from '../components/ui/StatusPill'
import type { StatusTone } from '../components/ui/StatusPill'
import { cancelOrder, initiateQrisPayment, trackOrder } from '../lib/orders'
import type {
  OrderStatus,
  OrderTracking,
  PaymentStatus,
} from '../lib/orders'
import { getApiErrorMessage } from '../lib/apiError'
import { formatDate, formatRupiah } from '../lib/format'
import {
  hasActiveTableSession,
  useTableStore,
} from '../stores/tableStore'

const statusMeta: Record<OrderStatus, { label: string; tone: StatusTone }> = {
  pending_payment: { label: 'Menunggu Pembayaran', tone: 'amber' },
  pending_confirmation: { label: 'Menunggu Konfirmasi', tone: 'amber' },
  processing: { label: 'Diproses', tone: 'brown' },
  ready: { label: 'Siap Disajikan', tone: 'green' },
  completed: { label: 'Selesai', tone: 'green' },
  cancelled: { label: 'Dibatalkan', tone: 'red' },
}

const paymentStatusLabel: Record<PaymentStatus, string> = {
  pending: 'Menunggu',
  paid: 'Lunas',
  failed: 'Gagal',
  expired: 'Kedaluwarsa',
  refunded: 'Dikembalikan',
  cancelled: 'Dibatalkan',
}

export function OrderTrackingPage() {
  const { trackingToken } = useParams<{ trackingToken: string }>()
  const navigate = useNavigate()
  const table = useTableStore((state) => state.table)
  const sessionToken = useTableStore((state) => state.sessionToken)
  const expiresAt = useTableStore((state) => state.expiresAt)
  const hasTable = hasActiveTableSession({ sessionToken, table, expiresAt })

  const [order, setOrder] = useState<OrderTracking | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [cancelOpen, setCancelOpen] = useState(false)
  const [cancelReason, setCancelReason] = useState('')
  const [cancelling, setCancelling] = useState(false)
  const [paying, setPaying] = useState(false)

  const load = useCallback(
    async (showLoading: boolean) => {
      if (!trackingToken) {
        setError('Token pesanan tidak ditemukan.')
        setLoading(false)
        return
      }

      if (showLoading) {
        setLoading(true)
      }

      try {
        const data = await trackOrder(trackingToken)
        setOrder(data)
        setError('')
      } catch (exception) {
        setError(getApiErrorMessage(exception))
      } finally {
        if (showLoading) {
          setLoading(false)
        }
      }
    },
    [trackingToken],
  )

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- polling status eksternal
    void load(true)
    const interval = setInterval(() => {
      void load(false)
    }, 5000)

    return () => clearInterval(interval)
  }, [load])

  const handleCancel = async () => {
    if (!trackingToken) {
      return
    }

    setCancelling(true)

    try {
      const updated = await cancelOrder(
        trackingToken,
        cancelReason.trim() || 'Dibatalkan customer',
      )
      setOrder(updated)
      setCancelOpen(false)
    } catch (exception) {
      setError(getApiErrorMessage(exception))
      setCancelOpen(false)
    } finally {
      setCancelling(false)
    }
  }

  const handlePay = async () => {
    if (!trackingToken) {
      return
    }

    setPaying(true)
    setError('')

    try {
      const payment = await initiateQrisPayment(trackingToken)

      if (payment.redirectUrl?.startsWith('/')) {
        navigate(payment.redirectUrl)
        return
      }

      if (payment.redirectUrl) {
        window.location.assign(payment.redirectUrl)
        return
      }

      await load(true)
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
          Memuat status pesanan...
        </div>
      </div>
    )
  }

  if (error && !order) {
    return (
      <section className="mx-auto max-w-xl px-4 py-20 text-center">
        <div className="mx-auto grid h-16 w-16 place-items-center rounded-2xl bg-rose-50 text-rose-600">
          <AlertCircle size={28} />
        </div>
        <h1 className="mt-5 font-display text-3xl text-coffee-950">
          Pesanan tidak ditemukan.
        </h1>
        <p className="mt-3 text-sm leading-7 text-stone-500">{error}</p>
        <Link to="/" className={buttonClasses('primary', 'mt-6')}>
          <ArrowLeft size={16} />
          Kembali ke beranda
        </Link>
      </section>
    )
  }

  if (!order) {
    return null
  }

  const meta = statusMeta[order.status]
  const canCancel =
    order.status === 'pending_payment' ||
    order.status === 'pending_confirmation'
  const canPay =
    order.status === 'pending_payment' &&
    order.payment.method === 'qris' &&
    order.payment.status === 'pending'

  return (
    <div className="mx-auto max-w-3xl px-4 py-8 md:px-6 md:py-10">
      <div className="rounded-3xl bg-coffee-900 p-6 text-white md:p-8">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-[10px] font-bold uppercase tracking-[0.24em] text-gold">
              Status pesanan
            </p>
            <h1 className="mt-2 font-display text-3xl">
              {order.orderNumber}
            </h1>
            <p className="mt-2 text-sm text-white/60">
              {order.customerName} · {order.table.name}
            </p>
          </div>
          <StatusPill tone={meta.tone}>{meta.label}</StatusPill>
        </div>

        <p className="mt-5 flex items-center gap-2 text-[10px] text-white/40">
          <Clock3 size={13} />
          Dibuat {formatDate(order.createdAt)} · halaman ini diperbarui otomatis
        </p>
      </div>

      <div className="mt-5 grid gap-5 md:grid-cols-[1.1fr_0.9fr]">
        <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
          <h2 className="font-bold text-coffee-950">Item pesanan</h2>
          <div className="mt-4 space-y-3">
            {order.items.map((item, index) => (
              <div
                key={`${item.productName}-${index}`}
                className="flex justify-between gap-3 text-sm"
              >
                <span className="text-stone-600">
                  {item.quantity} × {item.productName}
                </span>
                <b className="text-coffee-950">
                  {formatRupiah(item.subtotal)}
                </b>
              </div>
            ))}
          </div>

          <div className="mt-4 space-y-2 border-t border-coffee-100 pt-4 text-sm">
            <div className="flex justify-between text-stone-500">
              <span>Subtotal</span>
              <span>{formatRupiah(order.subtotal)}</span>
            </div>
            <div className="flex justify-between text-stone-500">
              <span>PB1 {order.taxRate}%</span>
              <span>{formatRupiah(order.taxAmount)}</span>
            </div>
            <div className="flex justify-between font-bold text-coffee-950">
              <span>Total</span>
              <span>{formatRupiah(order.total)}</span>
            </div>
          </div>

          <div className="mt-5 rounded-xl bg-coffee-50 p-4 text-xs text-coffee-800">
            Pembayaran:{' '}
            <b>
              {order.payment.method === 'cash'
                ? 'Tunai'
                : order.payment.method === 'qris'
                  ? 'QRIS'
                  : 'Tidak diketahui'}
            </b>
            {' · '}
            {order.payment.status === 'unknown'
              ? 'Tidak diketahui'
              : paymentStatusLabel[order.payment.status]}
            {order.payment.simulation && (
              <span className="mt-2 block text-[10px] font-semibold text-amber-700">
                Mode simulasi · pembayaran tidak nyata
              </span>
            )}
          </div>
        </div>

        <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
          <h2 className="font-bold text-coffee-950">Riwayat status</h2>
          <div className="mt-4 space-y-4">
            {order.timeline.map((event, index) => {
              const eventMeta = statusMeta[event.status]

              return (
                <div key={`${event.status}-${index}`} className="flex gap-3">
                  <div className="mt-0.5">
                    {event.status === 'cancelled' ? (
                      <XCircle size={17} className="text-rose-500" />
                    ) : (
                      <CheckCircle2
                        size={17}
                        className={
                          index === order.timeline.length - 1
                            ? 'text-emerald-600'
                            : 'text-coffee-400'
                        }
                      />
                    )}
                  </div>
                  <div>
                    <p className="text-xs font-bold text-coffee-950">
                      {eventMeta?.label ?? event.status}
                    </p>
                    <p className="text-[10px] text-stone-400">
                      {new Date(event.at).toLocaleString('id-ID')}
                    </p>
                    {event.note && (
                      <p className="mt-1 text-[10px] leading-5 text-stone-500">
                        {event.note}
                      </p>
                    )}
                  </div>
                </div>
              )
            })}
          </div>
        </div>
      </div>

      {error && (
        <div
          role="alert"
          className="mt-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
        >
          <AlertCircle size={16} className="mt-0.5 shrink-0" />
          {error}
        </div>
      )}

      <div className="mt-5 flex flex-wrap gap-2">
        {canPay && (
          <button
            type="button"
            onClick={() => void handlePay()}
            disabled={paying}
            className={buttonClasses('primary', '!px-4 !py-2 disabled:opacity-60')}
          >
            {paying ? (
              <Loader2 size={14} className="animate-spin" />
            ) : (
              <QrCode size={14} />
            )}
            {paying ? 'Menyiapkan...' : 'Bayar dengan QRIS'}
          </button>
        )}

        <button
          type="button"
          onClick={() => void load(true)}
          className={buttonClasses('outline', '!px-4 !py-2')}
        >
          <RefreshCw size={14} />
          Perbarui status
        </button>

        {canCancel && (
          <button
            type="button"
            onClick={() => setCancelOpen(true)}
            className={buttonClasses('ghost', '!px-4 !py-2 text-rose-600')}
          >
            <XCircle size={14} />
            Batalkan pesanan
          </button>
        )}

        {hasTable ? (
          <Link to="/pesan" className={buttonClasses('primary', '!px-4 !py-2')}>
            <ShoppingBag size={14} />
            Pesan lagi
          </Link>
        ) : (
          <Link to="/" className={buttonClasses('primary', '!px-4 !py-2')}>
            <ArrowLeft size={14} />
            Beranda
          </Link>
        )}
      </div>

      {cancelOpen && (
        <Modal title="Batalkan pesanan?" onClose={() => setCancelOpen(false)}>
          <p className="text-sm leading-6 text-stone-600">
            Pesanan yang dibatalkan tidak dapat diproses kembali. Berikan alasan
            agar kasir mengetahui kondisinya.
          </p>
          <textarea
            value={cancelReason}
            onChange={(event) => setCancelReason(event.target.value)}
            rows={3}
            maxLength={255}
            placeholder="Contoh: Salah pilih menu"
            className="mt-4 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
          <div className="mt-5 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setCancelOpen(false)}
              className={buttonClasses('outline')}
            >
              Kembali
            </button>
            <button
              type="button"
              onClick={() => void handleCancel()}
              disabled={cancelling}
              className={buttonClasses('primary', 'disabled:opacity-60')}
            >
              {cancelling && <Loader2 size={15} className="animate-spin" />}
              {cancelling ? 'Membatalkan...' : 'Batalkan pesanan'}
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}
