import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  Banknote,
  Loader2,
  QrCode,
  ShoppingBag,
} from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ProductImage } from '../components/public/ProductImage'
import { buttonClasses } from '../components/ui/buttonStyles'
import { EmptyState } from '../components/ui/EmptyState'
import { createOrder, initiateQrisPayment } from '../lib/orders'
import type { PaymentMethod } from '../lib/orders'
import { getApiErrorMessage } from '../lib/apiError'
import { formatRupiah } from '../lib/format'
import { calculateOrderTotals } from '../lib/orderMath'
import { useCartStore } from '../stores/cartStore'
import { useTableStore } from '../stores/tableStore'

export function CheckoutPage() {
  const navigate = useNavigate()
  const table = useTableStore((state) => state.table)
  const sessionToken = useTableStore((state) => state.sessionToken)
  const items = useCartStore((state) => state.items)
  const clearCart = useCartStore((state) => state.clear)

  const [customerName, setCustomerName] = useState('')
  const [notes, setNotes] = useState('')
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethod>('cash')
  const [error, setError] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [idempotencyKey] = useState(
    () => crypto.randomUUID?.() ?? `${Date.now()}-${Math.random()}`,
  )

  const subtotal = items.reduce(
    (total, item) => total + item.price * item.quantity,
    0,
  )
  const totals = calculateOrderTotals(subtotal)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')

    if (customerName.trim().length < 2) {
      setError('Nama minimal 2 karakter.')
      return
    }

    if (!sessionToken) {
      setError('Sesi meja tidak ditemukan. Scan ulang QR di meja.')
      return
    }

    setSubmitting(true)

    try {
      const order = await createOrder(sessionToken, idempotencyKey, {
        customerName: customerName.trim(),
        paymentMethod,
        notes: notes.trim() || undefined,
        items: items.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      })

      clearCart()

      if (paymentMethod === 'qris') {
        try {
          const payment = await initiateQrisPayment(order.trackingToken)

          if (payment.simulation) {
            navigate(`/pesanan/${order.trackingToken}/simulasi`, {
              replace: true,
            })
            return
          }

          if (payment.redirectUrl) {
            window.location.assign(payment.redirectUrl)
            return
          }
        } catch {
          // Pesanan tetap tersimpan. Customer dapat mencoba lagi dari halaman
          // pelacakan tanpa membuat pesanan baru.
        }
      }

      navigate(`/pesanan/${order.trackingToken}`, { replace: true })
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setSubmitting(false)
    }
  }

  if (items.length === 0) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 md:px-6">
        <Link
          to="/pesan"
          className="inline-flex items-center gap-2 text-xs font-semibold text-coffee-600"
        >
          <ArrowLeft size={14} />
          Kembali ke menu
        </Link>
        <div className="mt-6">
          <EmptyState
            icon={ShoppingBag}
            title="Keranjang kosong"
            description="Tambahkan menu terlebih dahulu sebelum checkout."
            action={
              <Link to="/pesan" className={buttonClasses('primary')}>
                Pilih menu
              </Link>
            }
          />
        </div>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-5xl px-4 py-7 md:px-6 md:py-9">
      <Link
        to="/pesan"
        className="inline-flex items-center gap-2 text-xs font-semibold text-coffee-600"
      >
        <ArrowLeft size={14} />
        Kembali ke menu
      </Link>

      <div className="mt-4">
        <p className="text-[10px] font-bold uppercase tracking-[0.24em] text-coffee-500">
          Checkout
        </p>
        <h1 className="mt-2 font-display text-4xl text-coffee-950">
          Selesaikan pesanan.
        </h1>
        <p className="mt-2 text-sm text-stone-500">
          {table?.name ?? 'Meja Anda'} · Pilih tunai di kasir atau bayar QRIS
          sekarang.
        </p>
      </div>

      <form
        onSubmit={handleSubmit}
        className="mt-6 grid gap-5 lg:grid-cols-[1fr_380px]"
      >
        <div className="space-y-5">
          <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
            <h2 className="font-bold text-coffee-950">Data pesanan</h2>
            <div className="mt-4 space-y-4">
              <label className="block text-xs font-semibold text-coffee-800">
                Nama customer
                <input
                  value={customerName}
                  onChange={(event) => setCustomerName(event.target.value)}
                  required
                  minLength={2}
                  maxLength={80}
                  placeholder="Nama kamu"
                  className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
                />
              </label>

              <label className="block text-xs font-semibold text-coffee-800">
                Catatan (opsional)
                <textarea
                  value={notes}
                  onChange={(event) => setNotes(event.target.value)}
                  rows={3}
                  maxLength={500}
                  placeholder="Contoh: tanpa gula, dipisah"
                  className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
                />
              </label>
            </div>
          </div>

          <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
            <h2 className="font-bold text-coffee-950">Metode pembayaran</h2>
            <div className="mt-4 space-y-2">
              <label
                className={`flex cursor-pointer items-center gap-3 rounded-xl border p-4 transition ${
                  paymentMethod === 'cash'
                    ? 'border-coffee-700 bg-coffee-50'
                    : 'border-coffee-200 bg-white hover:border-coffee-400'
                }`}
              >
                <input
                  type="radio"
                  name="payment"
                  value="cash"
                  checked={paymentMethod === 'cash'}
                  onChange={() => setPaymentMethod('cash')}
                  className="h-4 w-4 accent-coffee-800"
                />
                <Banknote size={20} className="text-coffee-700" />
                <span className="flex-1">
                  <b className="block text-sm">Tunai di kasir</b>
                  <small className="text-xs text-stone-500">
                    Bayar langsung kepada kasir setelah pesanan dikirim.
                  </small>
                </span>
              </label>

              <label
                className={`flex cursor-pointer items-center gap-3 rounded-xl border p-4 transition ${
                  paymentMethod === 'qris'
                    ? 'border-coffee-700 bg-coffee-50'
                    : 'border-coffee-200 bg-white hover:border-coffee-400'
                }`}
              >
                <input
                  type="radio"
                  name="payment"
                  value="qris"
                  checked={paymentMethod === 'qris'}
                  onChange={() => setPaymentMethod('qris')}
                  className="h-4 w-4 accent-coffee-800"
                />
                <QrCode size={20} className="text-coffee-700" />
                <span className="flex-1">
                  <b className="block text-sm">QRIS</b>
                  <small className="text-xs text-stone-500">
                    Bayar sekarang dengan scan QR. Diproses melalui Midtrans.
                  </small>
                </span>
              </label>
            </div>
          </div>
        </div>

        <aside className="h-fit rounded-2xl bg-coffee-900 p-5 text-white shadow-soft">
          <h2 className="font-display text-2xl">Ringkasan</h2>
          <div className="mt-4 space-y-3">
            {items.map((item) => (
              <div key={item.productId} className="flex items-center gap-2">
                <ProductImage
                  key={item.imageUrl ?? 'fallback'}
                  src={item.imageUrl}
                  alt={item.name}
                  className="h-10 w-10 rounded-lg"
                  iconSize={16}
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-xs text-white/80">
                    {item.quantity} × {item.name}
                  </p>
                </div>
                <span className="text-xs font-semibold">
                  {formatRupiah(item.price * item.quantity)}
                </span>
              </div>
            ))}
          </div>

          <div className="mt-5 space-y-2 border-t border-white/15 pt-4 text-xs">
            <div className="flex justify-between text-white/65">
              <span>Subtotal</span>
              <span>{formatRupiah(totals.subtotal)}</span>
            </div>
            <div className="flex justify-between text-white/65">
              <span>PB1 {totals.taxRate}%</span>
              <span>{formatRupiah(totals.taxAmount)}</span>
            </div>
            <div className="flex justify-between pt-2 text-base font-bold">
              <span>Total</span>
              <span>{formatRupiah(totals.total)}</span>
            </div>
          </div>

          <p className="mt-3 text-[10px] leading-5 text-white/40">
            Total akhir dihitung ulang oleh server saat pesanan dibuat.
          </p>

          {error && (
            <div
              role="alert"
              className="mt-4 flex gap-2 rounded-xl bg-rose-500/15 p-3 text-xs leading-5 text-rose-100"
            >
              <AlertCircle size={16} className="mt-0.5 shrink-0" />
              {error}
            </div>
          )}

          <button
            type="submit"
            disabled={submitting}
            className={buttonClasses('gold', 'mt-5 w-full disabled:opacity-60')}
          >
            {submitting ? (
              <Loader2 size={16} className="animate-spin" />
            ) : (
              <ArrowRight size={16} />
            )}
            {submitting
              ? 'Memproses...'
              : paymentMethod === 'qris'
                ? 'Lanjut ke pembayaran QRIS'
                : 'Kirim pesanan'}
          </button>
        </aside>
      </form>
    </div>
  )
}
