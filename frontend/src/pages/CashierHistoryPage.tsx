import { AlertCircle, History, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { buttonClasses } from '../components/ui/buttonStyles'
import { EmptyState } from '../components/ui/EmptyState'
import { SectionTitle } from '../components/ui/SectionTitle'
import { StatusPill } from '../components/ui/StatusPill'
import { getCashierTransactions } from '../lib/orders'
import { formatRupiah } from '../lib/format'
import { useApi } from '../lib/useApi'

export function CashierHistoryPage() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [method, setMethod] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)

  const transactions = useApi(
    () =>
      getCashierTransactions({
        from: from || undefined,
        to: to || undefined,
        method: method || undefined,
        status: status || undefined,
        page,
        pageSize: 20,
      }),
    [from, to, method, status, page],
  )

  return (
    <div>
      <SectionTitle
        eyebrow="Transaction history"
        title="Riwayat transaksi"
        description="Transaksi yang sudah dibayar dan dikembalikan."
        action={
          <Link to="/kasir" className={buttonClasses('outline', '!px-4 !py-2')}>
            Kembali ke antrean
          </Link>
        }
      />

      <div className="mb-5 grid gap-3 rounded-2xl border border-coffee-100 bg-white p-4 shadow-soft sm:grid-cols-2 lg:grid-cols-4">
        <label className="block text-xs font-semibold text-coffee-800">
          Dari tanggal
          <input
            type="date"
            value={from}
            onChange={(event) => {
              setFrom(event.target.value)
              setPage(1)
            }}
            className="mt-2 w-full rounded-xl border border-coffee-200 p-2.5 text-xs outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Sampai tanggal
          <input
            type="date"
            value={to}
            onChange={(event) => {
              setTo(event.target.value)
              setPage(1)
            }}
            className="mt-2 w-full rounded-xl border border-coffee-200 p-2.5 text-xs outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Metode
          <select
            value={method}
            onChange={(event) => {
              setMethod(event.target.value)
              setPage(1)
            }}
            className="mt-2 w-full rounded-xl border border-coffee-200 bg-white p-2.5 text-xs"
          >
            <option value="">Semua metode</option>
            <option value="cash">Tunai</option>
            <option value="qris">QRIS</option>
          </select>
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Status
          <select
            value={status}
            onChange={(event) => {
              setStatus(event.target.value)
              setPage(1)
            }}
            className="mt-2 w-full rounded-xl border border-coffee-200 bg-white p-2.5 text-xs"
          >
            <option value="">Semua status</option>
            <option value="paid">Lunas</option>
            <option value="refunded">Dikembalikan</option>
          </select>
        </label>
      </div>

      <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
        {transactions.loading && (
          <div className="grid place-items-center py-14">
            <Loader2 size={24} className="animate-spin text-coffee-500" />
          </div>
        )}

        {!transactions.loading && transactions.error && (
          <div
            role="alert"
            className="flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
          >
            <AlertCircle size={16} className="mt-0.5 shrink-0" />
            {transactions.error}
          </div>
        )}

        {!transactions.loading &&
          !transactions.error &&
          (transactions.data?.items.length ?? 0) === 0 && (
            <EmptyState
              icon={History}
              title="Belum ada transaksi"
              description="Transaksi yang sudah dikonfirmasi pembayarannya akan tampil di sini."
            />
          )}

        {!transactions.loading &&
          !transactions.error &&
          (transactions.data?.items.length ?? 0) > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[760px] text-left text-xs">
                <thead className="border-b border-coffee-100 text-[10px] uppercase tracking-wider text-stone-400">
                  <tr>
                    <th className="pb-3">Order</th>
                    <th className="pb-3">Pelanggan / Meja</th>
                    <th className="pb-3">Total</th>
                    <th className="pb-3">Metode</th>
                    <th className="pb-3">Status</th>
                    <th className="pb-3">Dibayar</th>
                  </tr>
                </thead>
                <tbody>
                  {(transactions.data?.items ?? []).map((transaction) => (
                    <tr
                      key={transaction.orderNumber}
                      className="border-b border-coffee-50"
                    >
                      <td className="py-4 font-bold text-coffee-800">
                        {transaction.orderNumber}
                      </td>
                      <td className="py-4">
                        <b>{transaction.customerName}</b>
                        <p className="mt-1 text-stone-400">
                          {transaction.table.name}
                        </p>
                      </td>
                      <td className="py-4 font-semibold">
                        {formatRupiah(transaction.total)}
                      </td>
                      <td className="py-4">
                        {transaction.method === 'cash' ? 'Tunai' : 'QRIS'}
                      </td>
                      <td className="py-4">
                        <StatusPill
                          tone={
                            transaction.status === 'paid' ? 'green' : 'brown'
                          }
                        >
                          {transaction.status === 'paid'
                            ? 'Lunas'
                            : 'Dikembalikan'}
                        </StatusPill>
                      </td>
                      <td className="py-4 text-stone-500">
                        {transaction.paidAt
                          ? new Date(transaction.paidAt).toLocaleString('id-ID', {
                              dateStyle: 'medium',
                              timeStyle: 'short',
                            })
                          : '-'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

        {!transactions.loading &&
          !transactions.error &&
          (transactions.data?.totalPages ?? 0) > 1 && (
            <div className="mt-5 flex items-center justify-between gap-3 text-xs text-stone-500">
              <span>
                Halaman {transactions.data?.page} dari{' '}
                {transactions.data?.totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((current) => current - 1)}
                  className={buttonClasses(
                    'outline',
                    '!px-3 !py-2 disabled:opacity-40',
                  )}
                >
                  Sebelumnya
                </button>
                <button
                  type="button"
                  disabled={page >= (transactions.data?.totalPages ?? 1)}
                  onClick={() => setPage((current) => current + 1)}
                  className={buttonClasses(
                    'outline',
                    '!px-3 !py-2 disabled:opacity-40',
                  )}
                >
                  Berikutnya
                </button>
              </div>
            </div>
          )}
      </div>
    </div>
  )
}
