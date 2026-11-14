import { AlertCircle, Loader2 } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { createCategory, updateCategory } from '../../lib/catalog'
import type { Category, CategoryInput } from '../../lib/catalog'
import { getApiErrorMessage } from '../../lib/apiError'
import { buttonClasses } from '../ui/buttonStyles'
import { Modal } from '../ui/Modal'

type CategoryFormModalProps = {
  category: Category | null
  onClose: () => void
  onSaved: () => void
}

export function CategoryFormModal({
  category,
  onClose,
  onSaved,
}: CategoryFormModalProps) {
  const [name, setName] = useState(category?.name ?? '')
  const [slug, setSlug] = useState(category?.slug ?? '')
  const [sortOrder, setSortOrder] = useState(
    category ? String(category.sortOrder) : '0',
  )
  const [isActive, setIsActive] = useState(category?.isActive ?? true)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')

    if (!name.trim()) {
      setError('Nama kategori wajib diisi.')
      return
    }

    const input: CategoryInput = {
      name: name.trim(),
      slug: slug.trim() || undefined,
      sortOrder: Number(sortOrder) || 0,
      isActive,
    }

    setSaving(true)

    try {
      if (category) {
        await updateCategory(category.id, input)
      } else {
        await createCategory(input)
      }

      onSaved()
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      title={category ? 'Edit kategori' : 'Tambah kategori'}
      onClose={onClose}
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        <label className="block text-xs font-semibold text-coffee-800">
          Nama kategori
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            maxLength={60}
            placeholder="Contoh: Coffee"
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Slug (opsional)
          <input
            value={slug}
            onChange={(event) => setSlug(event.target.value)}
            maxLength={80}
            placeholder="coffee"
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
          <span className="mt-1 block text-[10px] font-normal text-stone-400">
            Kosongkan untuk dibuat otomatis dari nama.
          </span>
        </label>

        <div className="grid grid-cols-2 gap-4">
          <label className="block text-xs font-semibold text-coffee-800">
            Urutan tampil
            <input
              type="number"
              value={sortOrder}
              onChange={(event) => setSortOrder(event.target.value)}
              className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
            />
          </label>

          <label className="flex items-center gap-3 self-end rounded-xl border border-coffee-100 bg-coffee-50 p-3 text-xs font-semibold text-coffee-800">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(event) => setIsActive(event.target.checked)}
              className="h-4 w-4 accent-coffee-800"
            />
            Aktif
          </label>
        </div>

        {error && (
          <div
            role="alert"
            className="flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
          >
            <AlertCircle size={16} className="mt-0.5 shrink-0" />
            {error}
          </div>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <button
            type="button"
            onClick={onClose}
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
            {saving ? 'Menyimpan...' : 'Simpan kategori'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
