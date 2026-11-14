import { AlertCircle, Loader2 } from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { createTable, updateTable } from '../../lib/tables'
import type { Table, TableInput } from '../../lib/tables'
import { getApiErrorMessage } from '../../lib/apiError'
import { buttonClasses } from '../ui/buttonStyles'
import { Modal } from '../ui/Modal'

type TableFormModalProps = {
  table: Table | null
  onClose: () => void
  onSaved: () => void
}

export function TableFormModal({
  table,
  onClose,
  onSaved,
}: TableFormModalProps) {
  const [number, setNumber] = useState(table?.number ?? '')
  const [name, setName] = useState(table?.name ?? '')
  const [capacity, setCapacity] = useState(String(table?.capacity ?? 2))
  const [isActive, setIsActive] = useState(table?.isActive ?? true)
  const [error, setError] = useState('')
  const [saving, setSaving] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')

    if (!number.trim() || !name.trim()) {
      setError('Nomor dan nama meja wajib diisi.')
      return
    }

    const parsedCapacity = Number(capacity)
    if (!Number.isFinite(parsedCapacity) || parsedCapacity < 1 || parsedCapacity > 50) {
      setError('Kapasitas meja antara 1 dan 50.')
      return
    }

    const input: TableInput = {
      number: number.trim(),
      name: name.trim(),
      capacity: parsedCapacity,
      isActive,
    }

    setSaving(true)

    try {
      if (table) {
        await updateTable(table.id, input)
      } else {
        await createTable(input)
      }

      onSaved()
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal title={table ? 'Edit meja' : 'Tambah meja'} onClose={onClose}>
      <form onSubmit={handleSubmit} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <label className="block text-xs font-semibold text-coffee-800">
            Nomor meja
            <input
              value={number}
              onChange={(event) => setNumber(event.target.value)}
              required
              maxLength={10}
              placeholder="01"
              className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
            />
          </label>

          <label className="block text-xs font-semibold text-coffee-800">
            Kapasitas
            <input
              type="number"
              value={capacity}
              onChange={(event) => setCapacity(event.target.value)}
              required
              min={1}
              max={50}
              className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
            />
          </label>
        </div>

        <label className="block text-xs font-semibold text-coffee-800">
          Nama meja
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            maxLength={50}
            placeholder="Meja 01"
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="flex items-center gap-3 rounded-xl border border-coffee-100 bg-coffee-50 p-3 text-xs font-semibold text-coffee-800">
          <input
            type="checkbox"
            checked={isActive}
            onChange={(event) => setIsActive(event.target.checked)}
            className="h-4 w-4 accent-coffee-800"
          />
          Meja aktif dan QR dapat dipakai
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
            {saving ? 'Menyimpan...' : 'Simpan meja'}
          </button>
        </div>
      </form>
    </Modal>
  )
}
