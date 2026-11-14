import {
  AlertCircle,
  Download,
  FileSpreadsheet,
  FileText,
  Loader2,
  RefreshCw,
  TrendingDown,
  TrendingUp,
  Wallet,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { SectionTitle } from '../../components/ui/SectionTitle'
import { buttonClasses } from '../../components/ui/buttonStyles'
import { getApiErrorMessage } from '../../lib/apiError'
import {
  firstOfMonthIso,
  formatIsoDate,
  todayIso,
} from '../../lib/dateRange'
import { formatRupiah } from '../../lib/format'
import {
  downloadReport,
  getReportData,
} from '../../lib/reporting'
import type {
  ReportData,
  ReportFormat,
  ReportType,
} from '../../lib/reporting'

const typeLabels: Record<ReportType, string> = {
  summary: 'Ringkasan',
  transactions: 'Transaksi',
  expenses: 'Pengeluaran',
  full: 'Lengkap',
}

export function ReportPage() {
  const [from, setFrom] = useState(firstOfMonthIso())
  const [to, setTo] = useState(todayIso())
  const [type, setType] = useState<ReportType>('full')
  const [format, setFormat] = useState<ReportFormat>('pdf')
  const [data, setData] = useState<ReportData | null>(null)
  const [loading, setLoading] = useState(true)
  const [downloading, setDownloading] = useState(false)
  const [error, setError] = useState('')

  const range = useMemo(() => ({ from, to }), [from, to])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')

    try {
      setData(await getReportData(range))
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setLoading(false)
    }
  }, [range])

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- memuat laporan
    void load()
  }, [load])

  const handleDownload = async () => {
    setDownloading(true)
    setError('')

    try {
      await downloadReport(range, format, type)
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setDownloading(false)
    }
  }

  const summary = data?.summary

  return (
    <div>
      <SectionTitle
        eyebrow="Laporan"
        title="Laporan keuangan"
        description="Ringkasan pendapatan, transaksi, dan pengeluaran. Unduh sebagai PDF atau Excel untuk arsip."
      />

      <div className="mb-5 grid gap-3 rounded-2xl border border-coffee-100 bg-white p-4 shadow-soft md:grid-cols-[repeat(4,minmax(0,1fr))_auto] md:items-end">
        <label className="text-[11px] font-semibold text-coffee-800">
          Dari
          <input
            type="date"
            value={from}
            max={to}
            onChange={(event) => setFrom(event.target.value)}
            className="mt-1 block w-full rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          />
        </label>
        <label className="text-[11px] font-semibold text-coffee-800">
          Sampai
          <input
            type="date"
            value={to}
            min={from}
            onChange={(event) => setTo(event.target.value)}
            className="mt-1 block w-full rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          />
        </label>
        <label className="text-[11px] font-semibold text-coffee-800">
          Isi laporan
          <select
            value={type}
            onChange={(event) => setType(event.target.value as ReportType)}
            className="mt-1 block w-full rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          >
            {Object.entries(typeLabels).map(([value, label]) => (
              <option key={value} value={value}>
                {label}
              </option>
            ))}
          </select>
        </label>
        <label className="text-[11px] font-semibold text-coffee-800">
          Format
          <select
            value={format}
            onChange={(event) => setFormat(event.target.value as ReportFormat)}
            className="mt-1 block w-full rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          >
            <option value="pdf">PDF</option>
            <option value="xlsx">Excel (.xlsx)</option>
          </select>
        </label>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => void handleDownload()}
            disabled={downloading || loading}
            className={buttonClasses('primary', '!px-4 !py-2 text-xs disabled:opacity-60')}
          >
            {downloading ? (
              <Loader2 size={14} className="animate-spin" />
            ) : (
              <Download size={14} />
            )}
            {downloading ? 'Menyiapkan...' : 'Unduh'}
          </button>
          <button
            type="button"
            onClick={() => void load()}
            aria-label="Muat ulang laporan"
            className={buttonClasses('outline', '!px-3 !py-2 text-xs')}
          >
            <RefreshCw size={14} />
          </button>
        </div>
      </div>

      <p className="mb-5 text-[10px] text-stone-400">
        Rentang maksimal 1 tahun per permintaan. Angka diambil langsung dari
        database, bukan dari tampilan.
      </p>

      {error && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-rose-100 bg-rose-50 p-4 text-xs text-rose-700">
          <span className="inline-flex items-center gap-2">
            <AlertCircle size={15} />
            {error}
          </span>
          <button
            type="button"
            onClick={() => void load()}
            className="font-bold underline"
          >
            Coba lagi
          </button>
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {summary ? (
          <>
            <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
              <div className="inline-flex rounded-xl bg-emerald-50 p-2.5 text-emerald-700">
                <TrendingUp size={18} />
              </div>
              <p className="mt-4 text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
                Pendapatan
              </p>
              <p className="mt-1 text-xl font-bold text-coffee-950">
                {formatRupiah(summary.totalRevenue)}
              </p>
              <p className="mt-1 text-[10px] text-stone-400">
                {summary.totalTransactions} transaksi
              </p>
            </div>
            <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
              <div className="inline-flex rounded-xl bg-rose-50 p-2.5 text-rose-700">
                <TrendingDown size={18} />
              </div>
              <p className="mt-4 text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
                Pengeluaran
              </p>
              <p className="mt-1 text-xl font-bold text-coffee-950">
                {formatRupiah(summary.totalExpenses)}
              </p>
              <p className="mt-1 text-[10px] text-stone-400">
                Periode terpilih
              </p>
            </div>
            <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
              <div className="inline-flex rounded-xl bg-coffee-100 p-2.5 text-coffee-800">
                <Wallet size={18} />
              </div>
              <p className="mt-4 text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
                Laba bersih
              </p>
              <p className="mt-1 text-xl font-bold text-coffee-950">
                {formatRupiah(summary.netProfit)}
              </p>
              <p className="mt-1 text-[10px] text-stone-400">
                Pendapatan − pengeluaran
              </p>
            </div>
            <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
              <div className="inline-flex rounded-xl bg-amber-50 p-2.5 text-amber-700">
                <FileText size={18} />
              </div>
              <p className="mt-4 text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
                Komposisi pembayaran
              </p>
              <p className="mt-1 text-sm font-bold text-coffee-950">
                QRIS {formatRupiah(summary.qrisRevenue)}
              </p>
              <p className="text-sm font-bold text-coffee-950">
                Tunai {formatRupiah(summary.cashRevenue)}
              </p>
            </div>
          </>
        ) : (
          Array.from({ length: 4 }).map((_, index) => (
            <div
              key={index}
              className="h-36 animate-pulse rounded-2xl border border-coffee-100 bg-white"
            />
          ))
        )}
      </div>

      <div className="mt-5 grid gap-5 xl:grid-cols-2">
        <div className="overflow-hidden rounded-2xl border border-coffee-100 bg-white shadow-soft">
          <div className="flex items-center justify-between gap-2 border-b border-coffee-100 px-4 py-3">
            <h2 className="text-sm font-bold text-coffee-950">Transaksi lunas</h2>
            <span className="text-[10px] text-stone-400">
              {data?.transactions.length ?? 0} baris
            </span>
          </div>
          <div className="max-h-[420px] overflow-auto">
            <table className="min-w-full text-left text-xs">
              <thead className="sticky top-0 bg-coffee-50 text-[10px] uppercase tracking-[0.12em] text-stone-500">
                <tr>
                  <th className="px-4 py-2.5 font-semibold">Pesanan</th>
                  <th className="px-4 py-2.5 font-semibold">Customer</th>
                  <th className="px-4 py-2.5 font-semibold">Metode</th>
                  <th className="px-4 py-2.5 text-right font-semibold">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-coffee-50">
                {!loading && (data?.transactions.length ?? 0) === 0 && (
                  <tr>
                    <td colSpan={4} className="px-4 py-8 text-center text-stone-500">
                      Belum ada transaksi pada periode ini.
                    </td>
                  </tr>
                )}
                {data?.transactions.map((transaction) => (
                  <tr key={transaction.orderNumber}>
                    <td className="px-4 py-2.5 font-semibold text-coffee-950">
                      {transaction.orderNumber}
                      <span className="block text-[10px] font-normal text-stone-400">
                        {transaction.tableName}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 text-stone-600">
                      {transaction.customerName}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="rounded-full bg-coffee-100 px-2 py-1 text-[10px] font-semibold text-coffee-800">
                        {transaction.method === 'qris' ? 'QRIS' : 'Tunai'}
                      </span>
                    </td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-right font-bold text-coffee-950">
                      {formatRupiah(transaction.total)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="overflow-hidden rounded-2xl border border-coffee-100 bg-white shadow-soft">
          <div className="flex items-center justify-between gap-2 border-b border-coffee-100 px-4 py-3">
            <h2 className="text-sm font-bold text-coffee-950">Pengeluaran</h2>
            <span className="text-[10px] text-stone-400">
              {data?.expenses.length ?? 0} baris
            </span>
          </div>
          <div className="max-h-[420px] overflow-auto">
            <table className="min-w-full text-left text-xs">
              <thead className="sticky top-0 bg-coffee-50 text-[10px] uppercase tracking-[0.12em] text-stone-500">
                <tr>
                  <th className="px-4 py-2.5 font-semibold">Tanggal</th>
                  <th className="px-4 py-2.5 font-semibold">Kategori</th>
                  <th className="px-4 py-2.5 font-semibold">Deskripsi</th>
                  <th className="px-4 py-2.5 text-right font-semibold">Nominal</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-coffee-50">
                {!loading && (data?.expenses.length ?? 0) === 0 && (
                  <tr>
                    <td colSpan={4} className="px-4 py-8 text-center text-stone-500">
                      Belum ada pengeluaran pada periode ini.
                    </td>
                  </tr>
                )}
                {data?.expenses.map((expense) => (
                  <tr key={expense.id}>
                    <td className="whitespace-nowrap px-4 py-2.5 text-stone-600">
                      {formatIsoDate(expense.occurredAt)}
                    </td>
                    <td className="px-4 py-2.5">
                      <span className="rounded-full bg-coffee-100 px-2 py-1 text-[10px] font-semibold text-coffee-800">
                        {expense.category}
                      </span>
                    </td>
                    <td className="px-4 py-2.5 font-medium text-coffee-950">
                      {expense.description}
                    </td>
                    <td className="whitespace-nowrap px-4 py-2.5 text-right font-bold text-rose-700">
                      {formatRupiah(expense.amount)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <div className="mt-5 flex flex-wrap items-center gap-2 rounded-2xl border border-coffee-100 bg-white p-4 text-[11px] text-stone-500 shadow-soft">
        <FileText size={15} className="text-coffee-700" />
        <span>
          Export dibuat di server dari data database untuk periode{' '}
          <b>{formatIsoDate(from)}</b> sampai <b>{formatIsoDate(to)}</b>.
        </span>
        <span className="ml-auto inline-flex items-center gap-1 text-coffee-700">
          <FileSpreadsheet size={14} />
          PDF & Excel siap diunduh
        </span>
      </div>
    </div>
  )
}
