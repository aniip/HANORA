import {
  ArrowRight,
  ArrowUpRight,
  ClipboardList,
  Loader2,
  ReceiptText,
  TrendingDown,
  TrendingUp,
  Wallet,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  Area,
  AreaChart,
  CartesianGrid,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { SectionTitle } from '../../components/ui/SectionTitle'
import { buttonClasses } from '../../components/ui/buttonStyles'
import { getApiErrorMessage } from '../../lib/apiError'
import {
  addDaysIso,
  firstOfMonthIso,
  formatPeriodLabel,
  todayIso,
} from '../../lib/dateRange'
import { formatRupiah } from '../../lib/format'
import {
  getDashboardSummary,
  getPaymentMethods,
  getSalesTrend,
  getTopProducts,
} from '../../lib/reporting'
import type {
  DashboardSummary,
  PaymentMethodBreakdown,
  SalesTrendPoint,
  TopProduct,
} from '../../lib/reporting'

const paymentColors: Record<string, string> = {
  qris: '#7c5c44',
  cash: '#d4a373',
}

const shortcuts = [
  {
    to: '/owner/pengeluaran',
    title: 'Pengeluaran & pemasukan',
    description: 'Catat biaya operasional dan pemasukan manual.',
  },
  {
    to: '/owner/laporan',
    title: 'Laporan keuangan',
    description: 'Lihat ringkasan dan unduh laporan PDF/Excel.',
  },
  {
    to: '/owner/produk',
    title: 'Kelola produk',
    description: 'Atur menu, harga, foto, dan ketersediaan.',
  },
  {
    to: '/owner/pengguna',
    title: 'Kelola pengguna',
    description: 'Tambah kasir dan atur akses staf.',
  },
]

export function OwnerDashboardPage() {
  const [from, setFrom] = useState(firstOfMonthIso())
  const [to, setTo] = useState(todayIso())
  const [groupBy, setGroupBy] = useState<'day' | 'month'>('day')
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [trend, setTrend] = useState<SalesTrendPoint[]>([])
  const [topProducts, setTopProducts] = useState<TopProduct[]>([])
  const [methods, setMethods] = useState<PaymentMethodBreakdown[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  const range = useMemo(() => ({ from, to }), [from, to])

  const load = useCallback(async () => {
    setLoading(true)
    setError('')

    try {
      const [summaryData, trendData, topData, methodData] = await Promise.all([
        getDashboardSummary(range),
        getSalesTrend(range, groupBy),
        getTopProducts(range, 8),
        getPaymentMethods(range),
      ])

      setSummary(summaryData)
      setTrend(trendData)
      setTopProducts(topData)
      setMethods(methodData)
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setLoading(false)
    }
  }, [range, groupBy])

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- memuat data dashboard
    void load()
  }, [load])

  const applyPreset = (days: number | 'month') => {
    const today = todayIso()
    setTo(today)
    setFrom(days === 'month' ? firstOfMonthIso() : addDaysIso(today, -(days - 1)))
  }

  const methodTotal = methods.reduce((total, item) => total + item.revenue, 0)
  const topRevenue = topProducts.reduce(
    (max, item) => Math.max(max, item.revenue),
    0,
  )

  const cards = summary
    ? [
        {
          label: 'Pendapatan',
          value: formatRupiah(summary.totalRevenue),
          hint: `${summary.totalTransactions} transaksi`,
          icon: TrendingUp,
          tone: 'bg-emerald-50 text-emerald-700',
        },
        {
          label: 'Pengeluaran',
          value: formatRupiah(summary.totalExpenses),
          hint: 'Periode terpilih',
          icon: TrendingDown,
          tone: 'bg-rose-50 text-rose-700',
        },
        {
          label: 'Laba bersih',
          value: formatRupiah(summary.netProfit),
          hint: 'Pendapatan − pengeluaran',
          icon: Wallet,
          tone: 'bg-coffee-100 text-coffee-800',
        },
        {
          label: 'Pesanan aktif',
          value: summary.activeOrders.toLocaleString('id-ID'),
          hint: 'Perlu diproses kasir',
          icon: ClipboardList,
          tone: 'bg-amber-50 text-amber-700',
        },
        {
          label: 'Rata-rata transaksi',
          value: formatRupiah(summary.averageOrderValue),
          hint: 'Per transaksi lunas',
          icon: ReceiptText,
          tone: 'bg-stone-100 text-stone-700',
        },
      ]
    : []

  return (
    <div>
      <SectionTitle
        eyebrow="Business overview"
        title="Dashboard owner"
        description="Pantau pendapatan, pengeluaran, produk terlaris, dan metode pembayaran dalam satu tampilan."
        action={
          <Link
            to="/owner/laporan"
            className={buttonClasses('primary', '!px-4 !py-2')}
          >
            Buka laporan
            <ArrowUpRight size={15} />
          </Link>
        }
      />

      <div className="mb-5 flex flex-wrap items-end gap-3 rounded-2xl border border-coffee-100 bg-white p-4 shadow-soft">
        <label className="text-[11px] font-semibold text-coffee-800">
          Dari
          <input
            type="date"
            value={from}
            max={to}
            onChange={(event) => setFrom(event.target.value)}
            className="mt-1 block rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          />
        </label>
        <label className="text-[11px] font-semibold text-coffee-800">
          Sampai
          <input
            type="date"
            value={to}
            min={from}
            onChange={(event) => setTo(event.target.value)}
            className="mt-1 block rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          />
        </label>
        <div className="flex flex-wrap gap-1">
          <button
            type="button"
            onClick={() => applyPreset(7)}
            className="rounded-lg border border-coffee-200 px-3 py-2 text-[11px] font-semibold text-coffee-700 hover:bg-coffee-50"
          >
            7 hari
          </button>
          <button
            type="button"
            onClick={() => applyPreset(30)}
            className="rounded-lg border border-coffee-200 px-3 py-2 text-[11px] font-semibold text-coffee-700 hover:bg-coffee-50"
          >
            30 hari
          </button>
          <button
            type="button"
            onClick={() => applyPreset('month')}
            className="rounded-lg border border-coffee-200 px-3 py-2 text-[11px] font-semibold text-coffee-700 hover:bg-coffee-50"
          >
            Bulan ini
          </button>
        </div>
        {loading && (
          <span className="ml-auto inline-flex items-center gap-2 text-[11px] text-stone-500">
            <Loader2 size={13} className="animate-spin" />
            Memuat...
          </span>
        )}
      </div>

      {error && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-rose-100 bg-rose-50 p-4 text-xs text-rose-700">
          <span>{error}</span>
          <button
            type="button"
            onClick={() => void load()}
            className="font-bold underline"
          >
            Coba lagi
          </button>
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        {summary
          ? cards.map((card) => (
              <div
                key={card.label}
                className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft"
              >
                <div className={`inline-flex rounded-xl p-2.5 ${card.tone}`}>
                  <card.icon size={18} />
                </div>
                <p className="mt-4 text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
                  {card.label}
                </p>
                <p className="mt-1 text-xl font-bold text-coffee-950">
                  {card.value}
                </p>
                <p className="mt-1 text-[10px] text-stone-400">{card.hint}</p>
              </div>
            ))
          : Array.from({ length: 5 }).map((_, index) => (
              <div
                key={index}
                className="h-36 animate-pulse rounded-2xl border border-coffee-100 bg-white"
              />
            ))}
      </div>

      <div className="mt-5 grid gap-5 xl:grid-cols-[1.55fr_1fr]">
        <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="font-bold text-coffee-950">Tren pendapatan</h2>
              <p className="mt-1 text-[11px] text-stone-400">
                Berdasarkan tanggal pemasukan tercatat.
              </p>
            </div>
            <div className="flex rounded-xl bg-coffee-50 p-1 text-[11px] font-semibold">
              <button
                type="button"
                onClick={() => setGroupBy('day')}
                className={`rounded-lg px-3 py-1.5 ${
                  groupBy === 'day'
                    ? 'bg-white text-coffee-900 shadow-sm'
                    : 'text-stone-500'
                }`}
              >
                Harian
              </button>
              <button
                type="button"
                onClick={() => setGroupBy('month')}
                className={`rounded-lg px-3 py-1.5 ${
                  groupBy === 'month'
                    ? 'bg-white text-coffee-900 shadow-sm'
                    : 'text-stone-500'
                }`}
              >
                Bulanan
              </button>
            </div>
          </div>

          <div className="mt-5 h-[290px] w-full">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={trend}>
                <defs>
                  <linearGradient id="revenueFill" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="0%" stopColor="#8a6a4f" stopOpacity={0.35} />
                    <stop offset="100%" stopColor="#8a6a4f" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" stroke="#efe5d8" />
                <XAxis
                  dataKey="period"
                  tick={{ fontSize: 10, fill: '#78716c' }}
                  tickFormatter={(value: string) =>
                    formatPeriodLabel(value, groupBy)
                  }
                  minTickGap={24}
                />
                <YAxis
                  tick={{ fontSize: 10, fill: '#78716c' }}
                  tickFormatter={(value: number) =>
                    `${Math.round(Number(value) / 1000)}k`
                  }
                  width={44}
                />
                <Tooltip
                  formatter={(value) => formatRupiah(Number(value))}
                  labelFormatter={(label) =>
                    formatPeriodLabel(String(label), groupBy)
                  }
                />
                <Area
                  type="monotone"
                  dataKey="revenue"
                  name="Pendapatan"
                  stroke="#6b4f3a"
                  strokeWidth={2}
                  fill="url(#revenueFill)"
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>

        <div className="space-y-5">
          <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
            <h2 className="font-bold text-coffee-950">Metode pembayaran</h2>
            <p className="mt-1 text-[11px] text-stone-400">
              Komposisi pendapatan lunas.
            </p>

            {methods.length === 0 ? (
              <p className="mt-6 rounded-xl bg-coffee-50 p-4 text-xs text-stone-500">
                Belum ada pembayaran lunas pada periode ini.
              </p>
            ) : (
              <div className="mt-3 flex items-center gap-4">
                <div className="h-36 w-36 shrink-0">
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie
                        data={methods}
                        dataKey="revenue"
                        nameKey="method"
                        innerRadius={38}
                        outerRadius={62}
                        paddingAngle={2}
                      >
                        {methods.map((item) => (
                          <Cell
                            key={item.method}
                            fill={paymentColors[item.method] ?? '#a8a29e'}
                          />
                        ))}
                      </Pie>
                      <Tooltip
                        formatter={(value) => formatRupiah(Number(value))}
                      />
                    </PieChart>
                  </ResponsiveContainer>
                </div>
                <div className="min-w-0 flex-1 space-y-3">
                  {methods.map((item) => (
                    <div key={item.method}>
                      <div className="flex items-center justify-between gap-2 text-xs">
                        <span className="inline-flex items-center gap-2 font-semibold text-coffee-900">
                          <span
                            className="h-2.5 w-2.5 rounded-full"
                            style={{
                              backgroundColor:
                                paymentColors[item.method] ?? '#a8a29e',
                            }}
                          />
                          {item.method === 'qris' ? 'QRIS' : 'Tunai'}
                        </span>
                        <span className="text-stone-500">
                          {formatRupiah(item.revenue)}
                        </span>
                      </div>
                      <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-coffee-50">
                        <div
                          className="h-full rounded-full"
                          style={{
                            width: `${methodTotal === 0 ? 0 : Math.round((item.revenue / methodTotal) * 100)}%`,
                            backgroundColor:
                              paymentColors[item.method] ?? '#a8a29e',
                          }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>

          <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
            <h2 className="font-bold text-coffee-950">Produk terlaris</h2>
            <p className="mt-1 text-[11px] text-stone-400">
              Berdasarkan pesanan lunas.
            </p>

            {topProducts.length === 0 ? (
              <p className="mt-4 rounded-xl bg-coffee-50 p-4 text-xs text-stone-500">
                Belum ada penjualan pada periode ini.
              </p>
            ) : (
              <div className="mt-4 space-y-3">
                {topProducts.map((product, index) => (
                  <div key={`${product.productId ?? product.productName}-${index}`}>
                    <div className="flex items-center justify-between gap-2 text-xs">
                      <span className="truncate font-semibold text-coffee-900">
                        {index + 1}. {product.productName}
                      </span>
                      <span className="shrink-0 text-stone-500">
                        {product.quantitySold}×
                      </span>
                    </div>
                    <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-coffee-50">
                      <div
                        className="h-full rounded-full bg-coffee-700"
                        style={{
                          width: `${topRevenue === 0 ? 0 : Math.round((product.revenue / topRevenue) * 100)}%`,
                        }}
                      />
                    </div>
                    <p className="mt-1 text-[10px] text-stone-400">
                      {formatRupiah(product.revenue)}
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="mt-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        {shortcuts.map((shortcut) => (
          <Link
            key={shortcut.to}
            to={shortcut.to}
            className="group rounded-2xl border border-coffee-100 bg-white p-4 shadow-soft transition hover:-translate-y-0.5 hover:border-coffee-200"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-sm font-bold text-coffee-950">
                  {shortcut.title}
                </h2>
                <p className="mt-1 text-[11px] leading-5 text-stone-500">
                  {shortcut.description}
                </p>
              </div>
              <ArrowRight
                size={16}
                className="mt-1 shrink-0 text-stone-300 transition group-hover:text-coffee-700"
              />
            </div>
          </Link>
        ))}
      </div>
    </div>
  )
}
