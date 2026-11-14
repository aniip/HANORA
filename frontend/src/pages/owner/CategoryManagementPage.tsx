import {
  AlertCircle,
  Pencil,
  Plus,
  Tags,
  Trash2,
} from 'lucide-react'
import { useState } from 'react'
import { CategoryFormModal } from '../../components/owner/CategoryFormModal'
import { buttonClasses } from '../../components/ui/buttonStyles'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { SectionTitle } from '../../components/ui/SectionTitle'
import { StatusPill } from '../../components/ui/StatusPill'
import { deleteCategory, getOwnerCategories } from '../../lib/catalog'
import type { Category } from '../../lib/catalog'
import { getApiErrorMessage } from '../../lib/apiError'
import { useApi } from '../../lib/useApi'

export function CategoryManagementPage() {
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Category | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<Category | null>(null)
  const [actionError, setActionError] = useState('')

  const categories = useApi(getOwnerCategories, [])

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (category: Category) => {
    setEditing(category)
    setFormOpen(true)
  }

  const handleDelete = async () => {
    if (!deleteTarget) {
      return
    }

    setActionError('')

    try {
      await deleteCategory(deleteTarget.id)
      setDeleteTarget(null)
      categories.reload()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
      setDeleteTarget(null)
    }
  }

  return (
    <div>
      <SectionTitle
        eyebrow="Catalog management"
        title="Manajemen kategori"
        description="Susun kategori menu. Kategori aktif akan tampil sebagai filter di website publik."
        action={
          <button
            type="button"
            onClick={openCreate}
            className={buttonClasses('primary')}
          >
            <Plus size={16} />
            Tambah kategori
          </button>
        }
      />

      {actionError && (
        <div
          role="alert"
          className="mb-5 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
        >
          <AlertCircle size={16} className="mt-0.5 shrink-0" />
          {actionError}
        </div>
      )}

      <div className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft">
        {categories.loading && (
          <div className="space-y-3">
            {Array.from({ length: 4 }).map((_, index) => (
              <div
                key={index}
                className="h-14 animate-pulse rounded-xl bg-coffee-50"
              />
            ))}
          </div>
        )}

        {!categories.loading && categories.error && (
          <div className="rounded-xl bg-rose-50 p-4 text-xs text-rose-700">
            {categories.error}{' '}
            <button
              type="button"
              onClick={categories.reload}
              className="font-bold underline"
            >
              Coba lagi
            </button>
          </div>
        )}

        {!categories.loading &&
          !categories.error &&
          (categories.data?.length ?? 0) > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[650px] text-left text-xs">
                <thead className="border-b border-coffee-100 text-[10px] uppercase tracking-wider text-stone-400">
                  <tr>
                    <th className="pb-3">Kategori</th>
                    <th className="pb-3">Slug</th>
                    <th className="pb-3">Urutan</th>
                    <th className="pb-3">Produk</th>
                    <th className="pb-3">Status</th>
                    <th className="pb-3 text-right">Aksi</th>
                  </tr>
                </thead>
                <tbody>
                  {(categories.data ?? []).map((category) => (
                    <tr
                      key={category.id}
                      className="border-b border-coffee-50"
                    >
                      <td className="py-4 font-bold text-coffee-950">
                        {category.name}
                      </td>
                      <td className="py-4 text-stone-500">{category.slug}</td>
                      <td className="py-4">{category.sortOrder}</td>
                      <td className="py-4">{category.productCount}</td>
                      <td className="py-4">
                        <StatusPill tone={category.isActive ? 'green' : 'gray'}>
                          {category.isActive ? 'Aktif' : 'Nonaktif'}
                        </StatusPill>
                      </td>
                      <td className="py-4">
                        <div className="flex justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => openEdit(category)}
                            aria-label={`Edit ${category.name}`}
                            className="rounded-lg p-2 text-coffee-700 hover:bg-coffee-50"
                          >
                            <Pencil size={15} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setDeleteTarget(category)}
                            aria-label={`Hapus ${category.name}`}
                            className="rounded-lg p-2 text-rose-500 hover:bg-rose-50"
                          >
                            <Trash2 size={15} />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

        {!categories.loading &&
          !categories.error &&
          (categories.data?.length ?? 0) === 0 && (
            <EmptyState
              icon={Tags}
              title="Belum ada kategori"
              description="Buat kategori pertama agar produk dapat dikelompokkan di menu publik."
              action={
                <button
                  type="button"
                  onClick={openCreate}
                  className={buttonClasses('primary')}
                >
                  <Plus size={16} />
                  Tambah kategori
                </button>
              }
            />
          )}
      </div>

      {formOpen && (
        <CategoryFormModal
          key={editing?.id ?? 'new'}
          category={editing}
          onClose={() => setFormOpen(false)}
          onSaved={() => {
            setFormOpen(false)
            categories.reload()
          }}
        />
      )}

      {deleteTarget && (
        <Modal
          title="Hapus kategori?"
          onClose={() => setDeleteTarget(null)}
        >
          <p className="text-sm leading-6 text-stone-600">
            Kategori <b>{deleteTarget.name}</b> akan dihapus permanen. Kategori
            yang masih memiliki produk tidak dapat dihapus.
          </p>
          <div className="mt-6 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setDeleteTarget(null)}
              className={buttonClasses('outline')}
            >
              Batal
            </button>
            <button
              type="button"
              onClick={() => void handleDelete()}
              className={buttonClasses('primary')}
            >
              <Trash2 size={15} />
              Hapus
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}

