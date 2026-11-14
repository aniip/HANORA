import { AlertCircle, Loader2 } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { createProduct, updateProduct } from '../../lib/catalog'
import type { Category, Product, ProductInput } from '../../lib/catalog'
import { getApiErrorMessage } from '../../lib/apiError'
import { buttonClasses } from '../ui/buttonStyles'
import { Modal } from '../ui/Modal'
import { ProductImage } from '../public/ProductImage'

type ProductFormModalProps = {
  product: Product | null
  categories: Category[]
  onClose: () => void
  onSaved: () => void
}

export function ProductFormModal({
  product,
  categories,
  onClose,
  onSaved,
}: ProductFormModalProps) {
  const [name, setName] = useState(product?.name ?? '')
  const [categoryId, setCategoryId] = useState(
    product?.categoryId ?? categories[0]?.id ?? '',
  )
  const [description, setDescription] = useState(product?.description ?? '')
  const [price, setPrice] = useState(product ? String(product.price) : '')
  const [isAvailable, setIsAvailable] = useState(product?.isAvailable ?? true)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')

    const numericPrice = Number(price)
    if (!name.trim()) {
      setError('Nama produk wajib diisi.')
      return
    }

    if (!categoryId) {
      setError('Kategori wajib dipilih.')
      return
    }

    if (!Number.isFinite(numericPrice) || numericPrice <= 0) {
      setError('Harga harus lebih dari 0.')
      return
    }

    const input: ProductInput = {
      name: name.trim(),
      categoryId,
      description: description.trim(),
      price: numericPrice,
      isAvailable,
    }

    setSaving(true)

    try {
      if (product) {
        await updateProduct(product.id, input)
      } else {
        await createProduct(input)
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
      title={product ? 'Edit produk' : 'Tambah produk'}
      onClose={onClose}
      wide
    >
      <form onSubmit={handleSubmit} className="space-y-4">
        {product?.imageUrl && (
          <div className="flex items-center gap-4 rounded-2xl bg-coffee-50 p-3">
            <ProductImage
              src={product.imageUrl}
              alt={product.name}
              className="h-20 w-20 rounded-xl"
              iconSize={24}
            />
            <p className="text-xs leading-5 text-stone-500">
              Ganti foto melalui tombol gambar di tabel produk. Foto baru
              langsung tersimpan.
            </p>
          </div>
        )}

        <div className="grid gap-4 md:grid-cols-2">
          <label className="block text-xs font-semibold text-coffee-800 md:col-span-2">
            Nama produk
            <input
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
              maxLength={120}
              placeholder="Contoh: Hanora Signature Latte"
              className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
            />
          </label>

          <label className="block text-xs font-semibold text-coffee-800">
            Kategori
            <select
              value={categoryId}
              onChange={(event) => setCategoryId(event.target.value)}
              required
              className="mt-2 w-full rounded-xl border border-coffee-200 bg-white p-3 text-sm outline-none focus:border-coffee-600"
            >
              {categories.map((category) => (
                <option key={category.id} value={category.id}>
                  {category.name}
                </option>
              ))}
            </select>
          </label>

          <label className="block text-xs font-semibold text-coffee-800">
            Harga (Rp)
            <input
              type="number"
              value={price}
              onChange={(event) => setPrice(event.target.value)}
              required
              min={1}
              step={1}
              placeholder="32000"
              className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
            />
          </label>

          <label className="block text-xs font-semibold text-coffee-800 md:col-span-2">
            Deskripsi
            <textarea
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              rows={3}
              maxLength={2000}
              placeholder="Deskripsi singkat produk"
              className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
            />
          </label>
        </div>

        <label className="flex items-center gap-3 rounded-xl border border-coffee-100 bg-coffee-50 p-3 text-xs font-semibold text-coffee-800">
          <input
            type="checkbox"
            checked={isAvailable}
            onChange={(event) => setIsAvailable(event.target.checked)}
            className="h-4 w-4 accent-coffee-800"
          />
          Tampilkan produk sebagai tersedia di menu publik
        </label>

        {error && (
          <div
            role="alert"
            className="flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
          >
            <AlertCircle size={16} className="mt-0.5 shrink-0" />
            {error}
          </div>
        )}

        <div className="flex flex-wrap justify-end gap-2 pt-2">
          <button
            type="button"
            onClick={onClose}
            className={buttonClasses('outline')}
          >
            Batal
          </button>
          <button
            type="submit"
            disabled={saving || categories.length === 0}
            className={buttonClasses('primary', 'disabled:opacity-60')}
          >
            {saving && <Loader2 size={15} className="animate-spin" />}
            {saving ? 'Menyimpan...' : 'Simpan produk'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
