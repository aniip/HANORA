import {
  AlertCircle,
  ArrowDownLeft,
  ArrowUpRight,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  Trash2,
} from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Modal } from '../../components/ui/Modal'
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
  createExpense,
  createManualIncome,
  deleteExpense,
  deleteIncome,
  getExpenses,
  getIncomes,
  updateExpense,
} from '../../lib/reporting'
import type {
  Expense,
  Income,
  PagedResult,
} from '../../lib/reporting'

const categories = [
  'Bahan baku',
  'Operasional',
  'Perlengkapan',
  'SDM',
  'Lainnya',
]

type Tab = 'expenses' | 'incomes'

type ExpenseForm = {
  category: string
  description: string
  amount: string
  occurredAt: string
  note: string
}

type IncomeForm = {
  description: string
  amount: string
  occurredAt: string
}

const emptyExpense = (): ExpenseForm => ({
  category: categories[0],
  description: '',
  amount: '',
  occurredAt: todayIso(),
  note: '',
})

const emptyIncome = (): IncomeForm => ({
  description: '',
  amount: '',
  occurredAt: todayIso(),
})

export function ExpenseManagementPage() {
  const [tab, setTab] = useState<Tab>('expenses')
  const [from, setFrom] = useState(firstOfMonthIso())
  const [to, setTo] = useState(todayIso())
  const [page, setPage] = useState(1)
  const [expenses, setExpenses] = useState<PagedResult<Expense> | null>(null)
  const [incomes, setIncomes] = useState<PagedResult<Income> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [saving, setSaving] = useState(false)

  const [expenseOpen, setExpenseOpen] = useState(false)
  const [editingExpense, setEditingExpense] = useState<Expense | null>(null)
  const [expenseForm, setExpenseForm] = useState<ExpenseForm>(emptyExpense)

  const [incomeOpen, setIncomeOpen] = useState(false)
  const [incomeForm, setIncomeForm] = useState<IncomeForm>(emptyIncome)

  const [deleteTarget, setDeleteTarget] = useState<{
    kind: Tab
    id: string
    label: string
  } | null>(null)
  const [deleting, setDeleting] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    setError('')

    try {
      if (tab === 'expenses') {
        setExpenses(await getExpenses({ from, to, page, pageSize: 20 }))
      } else {
        setIncomes(await getIncomes({ from, to, page, pageSize: 20 }))
      }
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setLoading(false)
    }
  }, [tab, from, to, page])

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- memuat data keuangan
    void load()
  }, [load])

  const changeTab = (next: Tab) => {
    setTab(next)
    setPage(1)
    setActionError('')
  }

  const openCreateExpense = () => {
    setEditingExpense(null)
    setExpenseForm(emptyExpense())
    setActionError('')
    setExpenseOpen(true)
  }

  const openCreateIncome = () => {
    setIncomeForm(emptyIncome())
    setActionError('')
    setIncomeOpen(true)
  }

  const openEditExpense = (expense: Expense) => {
    setEditingExpense(expense)
    setExpenseForm({
      category: expense.category,
      description: expense.description,
      amount: String(expense.amount),
      occurredAt: expense.occurredAt,
      note: expense.note ?? '',
    })
    setActionError('')
    setExpenseOpen(true)
  }

  const submitExpense = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setActionError('')

    const amount = Number(expenseForm.amount.replace(',', '.'))

    if (expenseForm.description.trim().length < 2) {
      setActionError('Deskripsi minimal 2 karakter.')
      return
    }

    if (!Number.isFinite(amount) || amount <= 0) {
      setActionError('Nominal pengeluaran harus lebih dari 0.')
      return
    }

    setSaving(true)

    try {
      const input = {
        category: expenseForm.category,
        description: expenseForm.description.trim(),
        amount,
        occurredAt: expenseForm.occurredAt,
        note: expenseForm.note.trim() || undefined,
      }

      if (editingExpense) {
        await updateExpense(editingExpense.id, input)
      } else {
        await createExpense(input)
      }

      setExpenseOpen(false)
      await load()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setSaving(false)
    }
  }

  const submitIncome = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setActionError('')

    const amount = Number(incomeForm.amount.replace(',', '.'))

    if (incomeForm.description.trim().length < 2) {
      setActionError('Deskripsi minimal 2 karakter.')
      return
    }

    if (!Number.isFinite(amount) || amount <= 0) {
      setActionError('Nominal pemasukan harus lebih dari 0.')
      return
    }

    setSaving(true)

    try {
      await createManualIncome({
        description: incomeForm.description.trim(),
        amount,
        occurredAt: incomeForm.occurredAt,
      })

      setIncomeOpen(false)
      setIncomeForm(emptyIncome())
      await load()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setSaving(false)
    }
  }

  const confirmDelete = async () => {
    if (!deleteTarget) {
      return
    }

    setDeleting(true)
    setActionError('')

    try {
      if (deleteTarget.kind === 'expenses') {
        await deleteExpense(deleteTarget.id)
      } else {
        await deleteIncome(deleteTarget.id)
      }

      setDeleteTarget(null)
      await load()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
      setDeleteTarget(null)
    } finally {
      setDeleting(false)
    }
  }

  const current = tab === 'expenses' ? expenses : incomes

  return (
    <div>
      <SectionTitle
        eyebrow="Keuangan"
        title="Pengeluaran & pemasukan"
        description="Catat biaya operasional dan pemasukan manual. Pendapatan pesanan masuk otomatis saat pembayaran lunas."
        action={
          <button
            type="button"
            onClick={() =>
              tab === 'expenses' ? openCreateExpense() : openCreateIncome()
            }
            className={buttonClasses('primary', '!px-4 !py-2')}
          >
            <Plus size={15} />
            {tab === 'expenses' ? 'Catat pengeluaran' : 'Catat pemasukan'}
          </button>
        }
      />

      <div className="mb-5 flex flex-wrap items-end gap-3 rounded-2xl border border-coffee-100 bg-white p-4 shadow-soft">
        <div className="flex rounded-xl bg-coffee-50 p-1 text-xs font-semibold">
          <button
            type="button"
            onClick={() => changeTab('expenses')}
            className={`inline-flex items-center gap-2 rounded-lg px-3 py-2 ${
              tab === 'expenses'
                ? 'bg-white text-coffee-900 shadow-sm'
                : 'text-stone-500'
            }`}
          >
            <ArrowUpRight size={14} />
            Pengeluaran
          </button>
          <button
            type="button"
            onClick={() => changeTab('incomes')}
            className={`inline-flex items-center gap-2 rounded-lg px-3 py-2 ${
              tab === 'incomes'
                ? 'bg-white text-coffee-900 shadow-sm'
                : 'text-stone-500'
            }`}
          >
            <ArrowDownLeft size={14} />
            Pemasukan
          </button>
        </div>

        <label className="text-[11px] font-semibold text-coffee-800">
          Dari
          <input
            type="date"
            value={from}
            max={to}
            onChange={(event) => {
              setFrom(event.target.value)
              setPage(1)
            }}
            className="mt-1 block rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          />
        </label>
        <label className="text-[11px] font-semibold text-coffee-800">
          Sampai
          <input
            type="date"
            value={to}
            min={from}
            onChange={(event) => {
              setTo(event.target.value)
              setPage(1)
            }}
            className="mt-1 block rounded-xl border border-coffee-200 px-3 py-2 text-xs outline-none focus:border-coffee-600"
          />
        </label>

        <button
          type="button"
          onClick={() => void load()}
          className={buttonClasses('outline', '!px-3 !py-2 text-xs')}
        >
          <RefreshCw size={14} />
          Muat ulang
        </button>
      </div>

      {error && (
        <div className="mb-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
          <AlertCircle size={16} className="mt-0.5 shrink-0" />
          {error}
        </div>
      )}

      {actionError && (
        <div className="mb-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
          <AlertCircle size={16} className="mt-0.5 shrink-0" />
          {actionError}
        </div>
      )}

      <div className="overflow-hidden rounded-2xl border border-coffee-100 bg-white shadow-soft">
        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-xs">
            <thead className="bg-coffee-50 text-[10px] uppercase tracking-[0.12em] text-stone-500">
              <tr>
                <th className="px-4 py-3 font-semibold">Tanggal</th>
                {tab === 'expenses' ? (
                  <>
                    <th className="px-4 py-3 font-semibold">Kategori</th>
                    <th className="px-4 py-3 font-semibold">Deskripsi</th>
                    <th className="px-4 py-3 font-semibold">Catatan</th>
                    <th className="px-4 py-3 text-right font-semibold">Nominal</th>
                  </>
                ) : (
                  <>
                    <th className="px-4 py-3 font-semibold">Sumber</th>
                    <th className="px-4 py-3 font-semibold">Deskripsi</th>
                    <th className="px-4 py-3 font-semibold">Pesanan</th>
                    <th className="px-4 py-3 text-right font-semibold">Nominal</th>
                  </>
                )}
                <th className="px-4 py-3 text-right font-semibold">Aksi</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-coffee-50">
              {loading && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-stone-500">
                    <span className="inline-flex items-center gap-2">
                      <Loader2 size={15} className="animate-spin" />
                      Memuat data...
                    </span>
                  </td>
                </tr>
              )}

              {!loading &&
                tab === 'expenses' &&
                (expenses?.items.length ?? 0) === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-10 text-center text-stone-500">
                      Belum ada pengeluaran pada periode ini.
                    </td>
                  </tr>
                )}

              {!loading &&
                tab === 'incomes' &&
                (incomes?.items.length ?? 0) === 0 && (
                  <tr>
                    <td colSpan={6} className="px-4 py-10 text-center text-stone-500">
                      Belum ada pemasukan pada periode ini.
                    </td>
                  </tr>
                )}

              {!loading &&
                tab === 'expenses' &&
                expenses?.items.map((expense) => (
                  <tr key={expense.id} className="hover:bg-coffee-50/50">
                    <td className="whitespace-nowrap px-4 py-3 text-stone-600">
                      {formatIsoDate(expense.occurredAt)}
                    </td>
                    <td className="px-4 py-3">
                      <span className="rounded-full bg-coffee-100 px-2 py-1 text-[10px] font-semibold text-coffee-800">
                        {expense.category}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-medium text-coffee-950">
                      {expense.description}
                    </td>
                    <td className="max-w-[220px] truncate px-4 py-3 text-stone-500">
                      {expense.note ?? '—'}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right font-bold text-rose-700">
                      {formatRupiah(expense.amount)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right">
                      <div className="inline-flex gap-1">
                        <button
                          type="button"
                          onClick={() => openEditExpense(expense)}
                          aria-label={`Ubah ${expense.description}`}
                          className="rounded-lg p-2 text-coffee-600 hover:bg-coffee-100"
                        >
                          <Pencil size={14} />
                        </button>
                        <button
                          type="button"
                          onClick={() =>
                            setDeleteTarget({
                              kind: 'expenses',
                              id: expense.id,
                              label: expense.description,
                            })
                          }
                          aria-label={`Hapus ${expense.description}`}
                          className="rounded-lg p-2 text-rose-600 hover:bg-rose-50"
                        >
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}

              {!loading &&
                tab === 'incomes' &&
                incomes?.items.map((income) => (
                  <tr key={income.id} className="hover:bg-coffee-50/50">
                    <td className="whitespace-nowrap px-4 py-3 text-stone-600">
                      {formatIsoDate(income.occurredAt)}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`rounded-full px-2 py-1 text-[10px] font-semibold ${
                          income.source === 'manual'
                            ? 'bg-amber-50 text-amber-700'
                            : 'bg-emerald-50 text-emerald-700'
                        }`}
                      >
                        {income.source === 'manual' ? 'Manual' : 'Pesanan'}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-medium text-coffee-950">
                      {income.description}
                    </td>
                    <td className="px-4 py-3 text-stone-500">
                      {income.orderNumber ?? '—'}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right font-bold text-emerald-700">
                      {formatRupiah(income.amount)}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right">
                      {income.source === 'manual' ? (
                        <button
                          type="button"
                          onClick={() =>
                            setDeleteTarget({
                              kind: 'incomes',
                              id: income.id,
                              label: income.description,
                            })
                          }
                          aria-label={`Hapus ${income.description}`}
                          className="rounded-lg p-2 text-rose-600 hover:bg-rose-50"
                        >
                          <Trash2 size={14} />
                        </button>
                      ) : (
                        <span className="text-[10px] text-stone-400">
                          Otomatis
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>

        {current && current.totalPages > 1 && (
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-coffee-100 px-4 py-3 text-[11px] text-stone-500">
            <span>
              Halaman {current.page} dari {current.totalPages} ·{' '}
              {current.totalItems} data
            </span>
            <div className="flex gap-1">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((value) => Math.max(1, value - 1))}
                className="rounded-lg border border-coffee-200 px-3 py-1.5 font-semibold text-coffee-700 disabled:opacity-40"
              >
                Sebelumnya
              </button>
              <button
                type="button"
                disabled={page >= current.totalPages}
                onClick={() =>
                  setPage((value) => Math.min(current.totalPages, value + 1))
                }
                className="rounded-lg border border-coffee-200 px-3 py-1.5 font-semibold text-coffee-700 disabled:opacity-40"
              >
                Berikutnya
              </button>
            </div>
          </div>
        )}
      </div>

      {expenseOpen && (
        <Modal
          title={editingExpense ? 'Ubah pengeluaran' : 'Catat pengeluaran'}
          onClose={() => setExpenseOpen(false)}
        >
          <form onSubmit={(event) => void submitExpense(event)} className="space-y-4">
            <label className="block text-xs font-semibold text-coffee-800">
              Kategori
              <select
                value={expenseForm.category}
                onChange={(event) =>
                  setExpenseForm((form) => ({
                    ...form,
                    category: event.target.value,
                  }))
                }
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              >
                {categories.map((category) => (
                  <option key={category} value={category}>
                    {category}
                  </option>
                ))}
              </select>
            </label>

            <label className="block text-xs font-semibold text-coffee-800">
              Deskripsi
              <input
                value={expenseForm.description}
                onChange={(event) =>
                  setExpenseForm((form) => ({
                    ...form,
                    description: event.target.value,
                  }))
                }
                required
                minLength={2}
                maxLength={255}
                placeholder="Contoh: Pembelian susu"
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
            </label>

            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block text-xs font-semibold text-coffee-800">
                Nominal
                <input
                  type="number"
                  min="1"
                  step="any"
                  value={expenseForm.amount}
                  onChange={(event) =>
                    setExpenseForm((form) => ({
                      ...form,
                      amount: event.target.value,
                    }))
                  }
                  required
                  placeholder="150000"
                  className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
                />
              </label>

              <label className="block text-xs font-semibold text-coffee-800">
                Tanggal
                <input
                  type="date"
                  value={expenseForm.occurredAt}
                  onChange={(event) =>
                    setExpenseForm((form) => ({
                      ...form,
                      occurredAt: event.target.value,
                    }))
                  }
                  required
                  className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
                />
              </label>
            </div>

            <label className="block text-xs font-semibold text-coffee-800">
              Catatan (opsional)
              <textarea
                value={expenseForm.note}
                onChange={(event) =>
                  setExpenseForm((form) => ({
                    ...form,
                    note: event.target.value,
                  }))
                }
                rows={3}
                maxLength={500}
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
            </label>

            {actionError && (
              <div className="rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
                {actionError}
              </div>
            )}

            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setExpenseOpen(false)}
                className={buttonClasses('outline')}
              >
                Batal
              </button>
              <button
                type="submit"
                disabled={saving}
                className={buttonClasses('primary', 'disabled:opacity-60')}
              >
                {saving && <Loader2 size={15} className="animate-spin" />}
                {saving ? 'Menyimpan...' : 'Simpan'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {incomeOpen && (
        <Modal
          title="Catat pemasukan manual"
          onClose={() => setIncomeOpen(false)}
        >
          <form onSubmit={(event) => void submitIncome(event)} className="space-y-4">
            <p className="rounded-xl bg-coffee-50 p-3 text-[11px] leading-5 text-stone-600">
              Gunakan untuk pemasukan di luar pesanan, misalnya penjualan
              merchandise atau pendapatan lain.
            </p>

            <label className="block text-xs font-semibold text-coffee-800">
              Deskripsi
              <input
                value={incomeForm.description}
                onChange={(event) =>
                  setIncomeForm((form) => ({
                    ...form,
                    description: event.target.value,
                  }))
                }
                required
                minLength={2}
                maxLength={255}
                placeholder="Contoh: Penjualan merchandise"
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
            </label>

            <div className="grid gap-4 sm:grid-cols-2">
              <label className="block text-xs font-semibold text-coffee-800">
                Nominal
                <input
                  type="number"
                  min="1"
                  step="any"
                  value={incomeForm.amount}
                  onChange={(event) =>
                    setIncomeForm((form) => ({
                      ...form,
                      amount: event.target.value,
                    }))
                  }
                  required
                  placeholder="100000"
                  className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
                />
              </label>

              <label className="block text-xs font-semibold text-coffee-800">
                Tanggal
                <input
                  type="date"
                  value={incomeForm.occurredAt}
                  onChange={(event) =>
                    setIncomeForm((form) => ({
                      ...form,
                      occurredAt: event.target.value,
                    }))
                  }
                  required
                  className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
                />
              </label>
            </div>

            {actionError && (
              <div className="rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
                {actionError}
              </div>
            )}

            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setIncomeOpen(false)}
                className={buttonClasses('outline')}
              >
                Batal
              </button>
              <button
                type="submit"
                disabled={saving}
                className={buttonClasses('primary', 'disabled:opacity-60')}
              >
                {saving && <Loader2 size={15} className="animate-spin" />}
                {saving ? 'Menyimpan...' : 'Simpan'}
              </button>
            </div>
          </form>
        </Modal>
      )}

      {deleteTarget && (
        <Modal
          title={
            deleteTarget.kind === 'expenses'
              ? 'Hapus pengeluaran?'
              : 'Hapus pemasukan?'
          }
          onClose={() => setDeleteTarget(null)}
        >
          <p className="text-sm leading-6 text-stone-600">
            Data <b>{deleteTarget.label}</b> akan dihapus permanen. Tindakan ini
            tidak dapat dibatalkan.
          </p>

          {actionError && (
            <div className="mt-4 rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
              {actionError}
            </div>
          )}

          <div className="mt-5 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setDeleteTarget(null)}
              className={buttonClasses('outline')}
            >
              Batal
            </button>
            <button
              type="button"
              onClick={() => void confirmDelete()}
              disabled={deleting}
              className={buttonClasses('primary', 'disabled:opacity-60')}
            >
              {deleting && <Loader2 size={15} className="animate-spin" />}
              {deleting ? 'Menghapus...' : 'Hapus'}
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}
