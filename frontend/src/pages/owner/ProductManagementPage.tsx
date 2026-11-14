import {
  AlertCircle,
  Archive,
  ImageUp,
  Loader2,
  Package,
  Pencil,
  Plus,
  Search,
} from 'lucide-react'
import { useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ProductFormModal } from '../../components/owner/ProductFormModal'
import { ProductImage } from '../../components/public/ProductImage'
import { buttonClasses } from '../../components/ui/buttonStyles'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { SectionTitle } from '../../components/ui/SectionTitle'
import { StatusPill } from '../../components/ui/StatusPill'
import {
  archiveProduct,
  getOwnerCategories,
  getOwnerProducts,
  setProductAvailability,
  uploadProductImage,
} from '../../lib/catalog'
import type { Product } from '../../lib/catalog'
import { getApiErrorMessage } from '../../lib/apiError'
import { formatRupiah } from '../../lib/format'
import { useApi } from '../../lib/useApi'

const MAX_IMAGE_SIZE = 2 * 1024 * 1024
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp']

export function ProductManagementPage() {
  const [categoryId, setCategoryId] = useState('')
  const [status, setStatus] = useState('all')
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Product | null>(null)
  const [archiveTarget, setArchiveTarget] = useState<Product | null>(null)
  const [actionError, setActionError] = useState('')
  const [uploadingId, setUploadingId] = useState<string | null>(null)

  const categories = useApi(getOwnerCategories, [])
  const products = useApi(
    () =>
      getOwnerProducts({
        categoryId: categoryId || undefined,
        search: search || undefined,
        status,
        page,
        pageSize: 10,
      }),
    [categoryId, status, search, page],
  )

  const refresh = () => {
    products.reload()
    categories.reload()
  }

  const handleSearch = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setPage(1)
    setSearch(searchInput.trim())
  }

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (product: Product) => {
    setEditing(product)
    setFormOpen(true)
  }

  const handleUpload = async (
    product: Product,
    event: ChangeEvent<HTMLInputElement>,
  ) => {
    const input = event.target
    const file = input.files?.[0]
    input.value = ''

    if (!file) {
      return
    }

    if (file.size > MAX_IMAGE_SIZE) {
      setActionError('Ukuran gambar maksimal 2 MB.')
      return
    }

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      setActionError('Format gambar harus JPEG, PNG, atau WebP.')
      return
    }

    setActionError('')
    setUploadingId(product.id)

    try {
      await uploadProductImage(product.id, file)
      refresh()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setUploadingId(null)
    }
  }

  const handleAvailability = async (product: Product) => {
    setActionError('')

    try {
      await setProductAvailability(product.id, !product.isAvailable)
      refresh()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    }
  }

  const handleArchive = async () => {
    if (!archiveTarget) {
      return
    }

    setActionError('')

    try {
      await archiveProduct(archiveTarget.id)
      setArchiveTarget(null)
      refresh()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    }
  }

  const productItems = products.data?.items ?? []
  const hasCategories = (categories.data?.length ?? 0) > 0

  return (
    <div>
      <SectionTitle
        eyebrow="Catalog management"
        title="Manajemen produk"
        description="Kelola menu, harga, foto, dan ketersediaan yang tampil di website publik."
        action={
          <button
            type="button"
            onClick={openCreate}
            disabled={!hasCategories}
            className={buttonClasses('primary', 'disabled:opacity-60')}
          >
            <Plus size={16} />
            Tambah produk
          </button>
        }
      />

      {!hasCategories && !categories.loading && (
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-amber-100 bg-amber-50 p-4 text-xs text-amber-800">
          <span>Buat kategori terlebih dahulu sebelum menambahkan produk.</span>
          <Link
            to="/owner/kategori"
            className="font-bold underline underline-offset-2"
          >
            Buka kategori
          </Link>
        </div>
      )}

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
        <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
          <div className="flex flex-wrap gap-2">
            <select
              value={categoryId}
              onChange={(event) => {
                setCategoryId(event.target.value)
                setPage(1)
              }}
              className="rounded-xl border border-coffee-200 bg-white px-3 py-2 text-xs text-coffee-800"
            >
              <option value="">Semua kategori</option>
              {(categories.data ?? []).map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>

            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value)
                setPage(1)
              }}
              className="rounded-xl border border-coffee-200 bg-white px-3 py-2 text-xs text-coffee-800"
            >
              <option value="all">Semua status</option>
              <option value="available">Tersedia</option>
              <option value="unavailable">Tidak tersedia</option>
              <option value="archived">Diarsipkan</option>
            </select>
          </div>

          <form
            onSubmit={handleSearch}
            className="flex items-center gap-2 rounded-xl border border-coffee-100 px-3 py-2"
          >
            <Search size={15} className="text-stone-400" />
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Cari produk..."
              className="w-36 text-xs outline-none placeholder:text-stone-400"
            />
            <button
              type="submit"
              className="text-[10px] font-bold text-coffee-700"
            >
              Cari
            </button>
          </form>
        </div>

        {products.loading && (
          <div className="space-y-3">
            {Array.from({ length: 4 }).map((_, index) => (
              <div
                key={index}
                className="h-16 animate-pulse rounded-xl bg-coffee-50"
              />
            ))}
          </div>
        )}

        {!products.loading && products.error && (
          <div className="rounded-xl bg-rose-50 p-4 text-xs text-rose-700">
            {products.error}{' '}
            <button
              type="button"
              onClick={products.reload}
              className="font-bold underline"
            >
              Coba lagi
            </button>
          </div>
        )}

        {!products.loading && !products.error && productItems.length > 0 && (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[900px] text-left text-xs">
              <thead className="border-b border-coffee-100 text-[10px] uppercase tracking-wider text-stone-400">
                <tr>
                  <th className="pb-3">Produk</th>
                  <th className="pb-3">Kategori</th>
                  <th className="pb-3">Harga</th>
                  <th className="pb-3">Ketersediaan</th>
                  <th className="pb-3">Status</th>
                  <th className="pb-3 text-right">Aksi</th>
                </tr>
              </thead>
              <tbody>
                {productItems.map((product) => (
                  <tr key={product.id} className="border-b border-coffee-50">
                    <td className="py-3">
                      <div className="flex items-center gap-3">
                        <ProductImage
                          key={product.imageUrl ?? 'fallback'}
                          src={product.imageUrl}
                          alt={product.name}
                          className="h-12 w-12 rounded-xl"
                          iconSize={18}
                        />
                        <div>
                          <b className="text-coffee-950">{product.name}</b>
                          <p className="mt-1 text-[10px] text-stone-400">
                            {product.slug}
                          </p>
                        </div>
                      </div>
                    </td>
                    <td className="py-3">
                      <StatusPill>{product.categoryName}</StatusPill>
                    </td>
                    <td className="py-3 font-bold">
                      {formatRupiah(product.price)}
                    </td>
                    <td className="py-3">
                      <button
                        type="button"
                        onClick={() => void handleAvailability(product)}
                        disabled={product.isArchived}
                        aria-pressed={product.isAvailable}
                        className={`rounded-full px-2.5 py-1 text-[11px] font-semibold transition disabled:cursor-not-allowed disabled:opacity-50 ${
                          product.isAvailable
                            ? 'bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                            : 'bg-amber-50 text-amber-700 hover:bg-amber-100'
                        }`}
                      >
                        {product.isAvailable ? 'Tersedia' : 'Tidak tersedia'}
                      </button>
                    </td>
                    <td className="py-3">
                      <StatusPill tone={product.isArchived ? 'gray' : 'green'}>
                        {product.isArchived ? 'Diarsipkan' : 'Aktif'}
                      </StatusPill>
                    </td>
                    <td className="py-3">
                      <div className="flex justify-end gap-1">
                        <button
                          type="button"
                          onClick={() => openEdit(product)}
                          disabled={product.isArchived}
                          aria-label={`Edit ${product.name}`}
                          className="rounded-lg p-2 text-coffee-700 hover:bg-coffee-50 disabled:opacity-40"
                        >
                          <Pencil size={15} />
                        </button>

                        <label
                          className={`cursor-pointer rounded-lg p-2 text-coffee-700 hover:bg-coffee-50 ${
                            product.isArchived ? 'pointer-events-none opacity-40' : ''
                          }`}
                          aria-label={`Unggah gambar ${product.name}`}
                        >
                          {uploadingId === product.id ? (
                            <Loader2 size={15} className="animate-spin" />
                          ) : (
                            <ImageUp size={15} />
                          )}
                          <input
                            type="file"
                            accept="image/jpeg,image/png,image/webp"
                            className="hidden"
                            onChange={(event) => void handleUpload(product, event)}
                          />
                        </label>

                        <button
                          type="button"
                          onClick={() => setArchiveTarget(product)}
                          disabled={product.isArchived}
                          aria-label={`Arsipkan ${product.name}`}
                          className="rounded-lg p-2 text-rose-500 hover:bg-rose-50 disabled:opacity-40"
                        >
                          <Archive size={15} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!products.loading && !products.error && productItems.length === 0 && (
          <EmptyState
            icon={Package}
            title="Belum ada produk"
            description={
              hasCategories
                ? 'Tambahkan produk pertama agar menu tampil di website publik.'
                : 'Buat kategori terlebih dahulu, lalu tambahkan produk.'
            }
            action={
              hasCategories ? (
                <button
                  type="button"
                  onClick={openCreate}
                  className={buttonClasses('primary')}
                >
                  <Plus size={16} />
                  Tambah produk
                </button>
              ) : (
                <Link to="/owner/kategori" className={buttonClasses('outline')}>
                  Buat kategori
                </Link>
              )
            }
          />
        )}

        {!products.loading &&
          !products.error &&
          (products.data?.totalPages ?? 0) > 1 && (
            <div className="mt-5 flex items-center justify-between gap-3 text-xs text-stone-500">
              <span>
                Halaman {products.data?.page} dari {products.data?.totalPages}
              </span>
              <div className="flex gap-2">
                <button
                  type="button"
                  disabled={page <= 1}
                  onClick={() => setPage((current) => current - 1)}
                  className={buttonClasses('outline', '!px-3 !py-2 disabled:opacity-40')}
                >
                  Sebelumnya
                </button>
                <button
                  type="button"
                  disabled={page >= (products.data?.totalPages ?? 1)}
                  onClick={() => setPage((current) => current + 1)}
                  className={buttonClasses('outline', '!px-3 !py-2 disabled:opacity-40')}
                >
                  Berikutnya
                </button>
              </div>
            </div>
          )}
      </div>

      {formOpen && hasCategories && (
        <ProductFormModal
          key={editing?.id ?? 'new'}
          product={editing}
          categories={categories.data ?? []}
          onClose={() => setFormOpen(false)}
          onSaved={() => {
            setFormOpen(false)
            refresh()
          }}
        />
      )}

      {archiveTarget && (
        <Modal
          title="Arsipkan produk?"
          onClose={() => setArchiveTarget(null)}
        >
          <p className="text-sm leading-6 text-stone-600">
            Produk <b>{archiveTarget.name}</b> akan disembunyikan dari website
            publik. Riwayat transaksi yang sudah ada tidak terpengaruh.
          </p>
          <div className="mt-6 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setArchiveTarget(null)}
              className={buttonClasses('outline')}
            >
              Batal
            </button>
            <button
              type="button"
              onClick={() => void handleArchive()}
              className={buttonClasses('primary')}
            >
              <Archive size={15} />
              Arsipkan
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}

