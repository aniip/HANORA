import {
  AlertCircle,
  Banknote,
  CheckCircle2,
  ChevronRight,
  ClipboardList,
  Loader2,
  RefreshCw,
  XCircle,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { buttonClasses } from '../components/ui/buttonStyles'
import { EmptyState } from '../components/ui/EmptyState'
import { Modal } from '../components/ui/Modal'
import { SectionTitle } from '../components/ui/SectionTitle'
import { StatusPill } from '../components/ui/StatusPill'
import type { StatusTone } from '../components/ui/StatusPill'
import {
  cancelCashierOrder,
  confirmCashPayment,
  getCashierOrder,
  getCashierOrders,
  updateCashierOrderStatus,
} from '../lib/orders'
import type {
  CashierOrderDetail,
  OrderStatus,
  PaymentStatus,
  PagedCashierOrders,
} from '../lib/orders'
import { getApiErrorMessage } from '../lib/apiError'
import { formatRupiah } from '../lib/format'

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

const statusOptions: Array<{ value: OrderStatus | 'all'; label: string }> = [
  { value: 'all', label: 'Semua status' },
  { value: 'pending_confirmation', label: 'Menunggu Konfirmasi' },
  { value: 'pending_payment', label: 'Menunggu Pembayaran' },
  { value: 'processing', label: 'Diproses' },
  { value: 'ready', label: 'Siap Disajikan' },
  { value: 'completed', label: 'Selesai' },
  { value: 'cancelled', label: 'Dibatalkan' },
]

export function CashierQueuePage() {
  const [orders, setOrders] = useState<PagedCashierOrders | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState<OrderStatus | 'all'>('all')

  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<CashierOrderDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

  const [actionError, setActionError] = useState('')
  const [actionSuccess, setActionSuccess] = useState('')
  const [actionLoading, setActionLoading] = useState(false)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [cashNote, setCashNote] = useState('')
  const [cancelOpen, setCancelOpen] = useState(false)
  const [cancelReason, setCancelReason] = useState('')

  const loadOrders = useCallback(async (showLoading: boolean) => {
    if (showLoading) {
      setLoading(true)
    }

    try {
      const result = await getCashierOrders({ pageSize: 100 })
      setOrders(result)
      setError('')
      setSelectedId((current) => current ?? result.items[0]?.id ?? null)
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      if (showLoading) {
        setLoading(false)
      }
    }
  }, [])

  const loadDetail = useCallback(async (id: string, showLoading: boolean) => {
    if (showLoading) {
      setDetailLoading(true)
    }

    try {
      const result = await getCashierOrder(id)
      setDetail(result)
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      if (showLoading) {
        setDetailLoading(false)
      }
    }
  }, [])

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- memuat antrean eksternal
    void loadOrders(true)
    const interval = setInterval(() => {
      void loadOrders(false)
      if (selectedId) {
        void loadDetail(selectedId, false)
      }
    }, 5000)

    return () => clearInterval(interval)
  }, [loadDetail, loadOrders, selectedId])

  useEffect(() => {
    if (!selectedId) {
      // oxlint-disable-next-line react/set-state-in-effect -- detail tidak dipilih
      setDetail(null)
      return
    }

    // oxlint-disable-next-line react/set-state-in-effect -- memuat detail pesanan
    void loadDetail(selectedId, true)
  }, [loadDetail, selectedId])

  const filteredOrders = useMemo(
    () =>
      (orders?.items ?? []).filter(
        (order) => filter === 'all' || order.status === filter,
      ),
    [filter, orders],
  )

  const pendingCount = (orders?.items ?? []).filter(
    (order) =>
      order.status === 'pending_confirmation' ||
      order.status === 'pending_payment',
  ).length

  const refreshAll = async () => {
    await loadOrders(false)
    if (selectedId) {
      await loadDetail(selectedId, false)
    }
  }

  const handleConfirmCash = async () => {
    if (!detail) {
      return
    }

    setActionLoading(true)
    setActionError('')

    try {
      const updated = await confirmCashPayment(
        detail.id,
        cashNote.trim() || undefined,
      )
      setDetail(updated)
      setActionSuccess('Pembayaran tunai dikonfirmasi. Pesanan masuk ke proses.')
      setConfirmOpen(false)
      setCashNote('')
      await refreshAll()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setActionLoading(false)
    }
  }

  const handleStatus = async (toStatus: 'ready' | 'completed') => {
    if (!detail) {
      return
    }

    setActionLoading(true)
    setActionError('')

    try {
      const updated = await updateCashierOrderStatus(detail.id, toStatus)
      setDetail(updated)
      setActionSuccess(
        toStatus === 'ready'
          ? 'Pesanan ditandai siap disajikan.'
          : 'Pesanan selesai.',
      )
      await refreshAll()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setActionLoading(false)
    }
  }

  const handleCancel = async () => {
    if (!detail) {
      return
    }

    if (cancelReason.trim().length < 3) {
      setActionError('Alasan pembatalan minimal 3 karakter.')
      return
    }

    setActionLoading(true)
    setActionError('')

    try {
      const updated = await cancelCashierOrder(detail.id, cancelReason.trim())
      setDetail(updated)
      setActionSuccess('Pesanan dibatalkan.')
      setCancelOpen(false)
      setCancelReason('')
      await refreshAll()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setActionLoading(false)
    }
  }

  const status = detail?.status
  const canConfirmCash =
    status === 'pending_confirmation' &&
    detail?.payment.method === 'cash' &&
    detail?.payment.status === 'pending'
  const canCancel =
    status === 'pending_confirmation' || status === 'pending_payment'

  return (
    <div>
      <SectionTitle
        eyebrow="Operational workspace"
        title="Antrean pesanan"
        description="Konfirmasi pembayaran tunai dan kelola status pesanan dari QR meja."
        action={
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill tone="amber">{pendingCount} perlu ditinjau</StatusPill>
            <button
              type="button"
              onClick={() => void refreshAll()}
              className={buttonClasses('outline', '!px-3 !py-2')}
            >
              <RefreshCw size={14} />
              Perbarui
            </button>
          </div>
        }
      />

      {error && (
        <div
          role="alert"
          className="mb-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
        >
          <AlertCircle size={16} className="mt-0.5 shrink-0" />
          {error}
        </div>
      )}

      <div className="grid gap-5 xl:grid-cols-[1.05fr_0.95fr]">
        <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
          <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="font-bold text-coffee-950">Daftar pesanan</h2>
              <p className="text-xs text-stone-500">
                Diperbarui otomatis setiap 5 detik.
              </p>
            </div>
            <select
              value={filter}
              onChange={(event) =>
                setFilter(event.target.value as OrderStatus | 'all')
              }
              className="rounded-xl border border-coffee-200 bg-white px-3 py-2 text-xs text-coffee-800"
            >
              {statusOptions.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          {loading && (
            <div className="grid place-items-center py-14">
              <Loader2 size={24} className="animate-spin text-coffee-500" />
            </div>
          )}

          {!loading && filteredOrders.length === 0 && (
            <EmptyState
              icon={ClipboardList}
              title="Tidak ada pesanan"
              description={
                filter === 'all'
                  ? 'Pesanan dari customer yang scan QR meja akan muncul di sini.'
                  : 'Tidak ada pesanan dengan status ini.'
              }
            />
          )}

          {!loading && filteredOrders.length > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-left text-xs">
                <thead className="border-b border-coffee-100 text-[10px] uppercase tracking-wider text-stone-400">
                  <tr>
                    <th className="pb-3">Order</th>
                    <th className="pb-3">Pelanggan / Meja</th>
                    <th className="pb-3">Total</th>
                    <th className="pb-3">Status</th>
                    <th className="pb-3">Waktu</th>
                    <th className="pb-3" />
                  </tr>
                </thead>
                <tbody>
                  {filteredOrders.map((order) => (
                    <tr
                      key={order.id}
                      onClick={() => setSelectedId(order.id)}
                      className={`cursor-pointer border-b border-coffee-50 transition ${
                        selectedId === order.id ? 'bg-coffee-50' : ''
                      }`}
                    >
                      <td className="py-3">
                        <b className="text-coffee-800">{order.orderNumber}</b>
                        <p className="mt-1 text-[10px] text-stone-400">
                          {order.itemCount} item ·{' '}
                          {order.paymentMethod === 'cash' ? 'Tunai' : 'QRIS'}
                        </p>
                      </td>
                      <td className="py-3">
                        <b>{order.customerName}</b>
                        <p className="mt-1 text-stone-400">{order.table.name}</p>
                      </td>
                      <td className="py-3 font-semibold">
                        {formatRupiah(order.total)}
                      </td>
                      <td className="py-3">
                        <StatusPill tone={statusMeta[order.status].tone}>
                          {statusMeta[order.status].label}
                        </StatusPill>
                      </td>
                      <td className="py-3 text-stone-500">
                        {new Date(order.createdAt).toLocaleTimeString('id-ID', {
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </td>
                      <td className="py-3 text-right text-stone-300">
                        <ChevronRight size={16} className="ml-auto" />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>

        <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
          {!selectedId && (
            <EmptyState
              icon={ClipboardList}
              title="Pilih pesanan"
              description="Pilih salah satu pesanan untuk melihat detail dan mengubah status."
            />
          )}

          {selectedId && detailLoading && (
            <div className="grid place-items-center py-16">
              <Loader2 size={24} className="animate-spin text-coffee-500" />
            </div>
          )}

          {selectedId && !detailLoading && detail && (
            <div>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-[10px] uppercase tracking-[0.18em] text-stone-400">
                    Detail pesanan
                  </p>
                  <h2 className="mt-1 font-display text-2xl text-coffee-950">
                    {detail.orderNumber}
                  </h2>
                  <p className="mt-1 text-xs text-stone-500">
                    {detail.customerName} · {detail.table.name}
                  </p>
                </div>
                <StatusPill tone={statusMeta[detail.status].tone}>
                  {statusMeta[detail.status].label}
                </StatusPill>
              </div>

              <div className="mt-4 rounded-xl bg-coffee-50 p-4">
                <div className="flex justify-between text-xs">
                  <span className="text-stone-500">Pembayaran</span>
                  <b className="text-coffee-950">
                    {detail.payment.method === 'cash' ? 'Tunai' : 'QRIS'} ·{' '}
                    {detail.payment.status === 'unknown'
                      ? 'Tidak diketahui'
                      : paymentStatusLabel[detail.payment.status]}
                  </b>
                </div>
                {detail.notes && (
                  <p className="mt-2 text-xs leading-5 text-stone-500">
                    Catatan: {detail.notes}
                  </p>
                )}
              </div>

              <div className="mt-4 space-y-3">
                {detail.items.map((item, index) => (
                  <div
                    key={`${item.productName}-${index}`}
                    className="flex justify-between gap-3 text-sm"
                  >
                    <span className="text-stone-600">
                      {item.quantity} × {item.productName}
                    </span>
                    <b>{formatRupiah(item.subtotal)}</b>
                  </div>
                ))}
              </div>

              <div className="mt-4 space-y-2 border-t border-coffee-100 pt-4 text-sm">
                <div className="flex justify-between text-stone-500">
                  <span>Subtotal</span>
                  <span>{formatRupiah(detail.subtotal)}</span>
                </div>
                <div className="flex justify-between text-stone-500">
                  <span>PB1 {detail.taxRate}%</span>
                  <span>{formatRupiah(detail.taxAmount)}</span>
                </div>
                <div className="flex justify-between font-bold text-coffee-950">
                  <span>Total</span>
                  <span>{formatRupiah(detail.total)}</span>
                </div>
              </div>

              {detail.timeline.length > 0 && (
                <div className="mt-4 border-t border-coffee-100 pt-4">
                  <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
                    Riwayat
                  </p>
                  <div className="mt-2 space-y-2">
                    {detail.timeline.slice(-3).map((event, index) => (
                      <div
                        key={`${event.status}-${index}`}
                        className="flex justify-between gap-2 text-[11px]"
                      >
                        <span className="text-coffee-800">
                          {statusMeta[event.status]?.label ?? event.status}
                        </span>
                        <span className="text-stone-400">
                          {new Date(event.at).toLocaleTimeString('id-ID', {
                            hour: '2-digit',
                            minute: '2-digit',
                          })}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {actionError && (
                <div
                  role="alert"
                  className="mt-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
                >
                  <AlertCircle size={16} className="mt-0.5 shrink-0" />
                  {actionError}
                </div>
              )}

              {actionSuccess && (
                <div className="mt-4 flex gap-2 rounded-xl bg-emerald-50 p-3 text-xs leading-5 text-emerald-800">
                  <CheckCircle2 size={16} className="mt-0.5 shrink-0" />
                  {actionSuccess}
                </div>
              )}

              <div className="mt-5 flex flex-wrap gap-2">
                {canConfirmCash && (
                  <button
                    type="button"
                    onClick={() => {
                      setActionError('')
                      setActionSuccess('')
                      setConfirmOpen(true)
                    }}
                    className={buttonClasses('primary')}
                  >
                    <Banknote size={15} />
                    Konfirmasi Tunai
                  </button>
                )}

                {status === 'processing' && (
                  <button
                    type="button"
                    disabled={actionLoading}
                    onClick={() => void handleStatus('ready')}
                    className={buttonClasses('primary', 'disabled:opacity-60')}
                  >
                    <CheckCircle2 size={15} />
                    Siap Disajikan
                  </button>
                )}

                {status === 'ready' && (
                  <button
                    type="button"
                    disabled={actionLoading}
                    onClick={() => void handleStatus('completed')}
                    className={buttonClasses('primary', 'disabled:opacity-60')}
                  >
                    <CheckCircle2 size={15} />
                    Selesaikan
                  </button>
                )}

                {canCancel && (
                  <button
                    type="button"
                    onClick={() => {
                      setActionError('')
                      setActionSuccess('')
                      setCancelOpen(true)
                    }}
                    className={buttonClasses('ghost', 'text-rose-600')}
                  >
                    <XCircle size={15} />
                    Batalkan
                  </button>
                )}
              </div>
            </div>
          )}
        </div>
      </div>

      {confirmOpen && (
        <Modal title="Konfirmasi pembayaran tunai" onClose={() => setConfirmOpen(false)}>
          <p className="text-sm leading-6 text-stone-600">
            Pastikan uang tunai sudah diterima sebesar{' '}
            <b>{detail ? formatRupiah(detail.total) : '-'}</b>. Setelah
            dikonfirmasi, pesanan masuk ke status Diproses.
          </p>
          <input
            value={cashNote}
            onChange={(event) => setCashNote(event.target.value)}
            maxLength={255}
            placeholder="Catatan (opsional)"
            className="mt-4 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
          <div className="mt-5 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setConfirmOpen(false)}
              className={buttonClasses('outline')}
            >
              Batal
            </button>
            <button
              type="button"
              disabled={actionLoading}
              onClick={() => void handleConfirmCash()}
              className={buttonClasses('primary', 'disabled:opacity-60')}
            >
              {actionLoading ? (
                <Loader2 size={15} className="animate-spin" />
              ) : (
                <Banknote size={15} />
              )}
              Konfirmasi
            </button>
          </div>
        </Modal>
      )}

      {cancelOpen && (
        <Modal title="Batalkan pesanan?" onClose={() => setCancelOpen(false)}>
          <p className="text-sm leading-6 text-stone-600">
            Pesanan yang dibatalkan tidak dapat diproses kembali. Tulis alasan
            pembatalan.
          </p>
          <textarea
            value={cancelReason}
            onChange={(event) => setCancelReason(event.target.value)}
            rows={3}
            maxLength={255}
            placeholder="Contoh: Customer tidak melanjutkan pembayaran"
            className="mt-4 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
          <div className="mt-5 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setCancelOpen(false)}
              className={buttonClasses('outline')}
            >
              Batal
            </button>
            <button
              type="button"
              disabled={actionLoading}
              onClick={() => void handleCancel()}
              className={buttonClasses('primary', 'disabled:opacity-60')}
            >
              {actionLoading ? (
                <Loader2 size={15} className="animate-spin" />
              ) : (
                <XCircle size={15} />
              )}
              Batalkan pesanan
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}
