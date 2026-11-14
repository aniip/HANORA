import {
  AlertCircle,
  Pencil,
  Plus,
  QrCode,
  RefreshCw,
  Trash2,
} from 'lucide-react'
import { useState } from 'react'
import { TableFormModal } from '../../components/owner/TableFormModal'
import { TableQrModal } from '../../components/owner/TableQrModal'
import { buttonClasses } from '../../components/ui/buttonStyles'
import { EmptyState } from '../../components/ui/EmptyState'
import { Modal } from '../../components/ui/Modal'
import { SectionTitle } from '../../components/ui/SectionTitle'
import { StatusPill } from '../../components/ui/StatusPill'
import { deleteTable, getOwnerTables, regenerateTableQr } from '../../lib/tables'
import type { Table } from '../../lib/tables'
import { getApiErrorMessage } from '../../lib/apiError'
import { useApi } from '../../lib/useApi'

export function TableManagementPage() {
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Table | null>(null)
  const [qrTarget, setQrTarget] = useState<Table | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<Table | null>(null)
  const [regenerateTarget, setRegenerateTarget] = useState<Table | null>(null)
  const [actionError, setActionError] = useState('')

  const tables = useApi(getOwnerTables, [])

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }

  const openEdit = (table: Table) => {
    setEditing(table)
    setFormOpen(true)
  }

  const handleDelete = async () => {
    if (!deleteTarget) {
      return
    }

    setActionError('')

    try {
      await deleteTable(deleteTarget.id)
      setDeleteTarget(null)
      tables.reload()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
      setDeleteTarget(null)
    }
  }

  const handleRegenerate = async () => {
    if (!regenerateTarget) {
      return
    }

    setActionError('')

    try {
      await regenerateTableQr(regenerateTarget.id)
      setRegenerateTarget(null)
      tables.reload()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
      setRegenerateTarget(null)
    }
  }

  return (
    <div>
      <SectionTitle
        eyebrow="Table & QR management"
        title="Meja & QR"
        description="Setiap meja memiliki satu QR unik. QR mengarah ke sesi pemesanan customer."
        action={
          <button
            type="button"
            onClick={openCreate}
            className={buttonClasses('primary')}
          >
            <Plus size={16} />
            Tambah meja
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
        {tables.loading && (
          <div className="space-y-3">
            {Array.from({ length: 4 }).map((_, index) => (
              <div
                key={index}
                className="h-16 animate-pulse rounded-xl bg-coffee-50"
              />
            ))}
          </div>
        )}

        {!tables.loading && tables.error && (
          <div className="rounded-xl bg-rose-50 p-4 text-xs text-rose-700">
            {tables.error}{' '}
            <button
              type="button"
              onClick={tables.reload}
              className="font-bold underline"
            >
              Coba lagi
            </button>
          </div>
        )}

        {!tables.loading &&
          !tables.error &&
          (tables.data?.length ?? 0) > 0 && (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[800px] text-left text-xs">
                <thead className="border-b border-coffee-100 text-[10px] uppercase tracking-wider text-stone-400">
                  <tr>
                    <th className="pb-3">Meja</th>
                    <th className="pb-3">Kapasitas</th>
                    <th className="pb-3">Status</th>
                    <th className="pb-3">QR</th>
                    <th className="pb-3 text-right">Aksi</th>
                  </tr>
                </thead>
                <tbody>
                  {(tables.data ?? []).map((table) => (
                    <tr key={table.id} className="border-b border-coffee-50">
                      <td className="py-4">
                        <b className="text-coffee-950">{table.name}</b>
                        <p className="mt-1 text-[10px] text-stone-400">
                          Nomor {table.number}
                        </p>
                      </td>
                      <td className="py-4">{table.capacity} orang</td>
                      <td className="py-4">
                        <StatusPill tone={table.isActive ? 'green' : 'gray'}>
                          {table.isActive ? 'Aktif' : 'Nonaktif'}
                        </StatusPill>
                      </td>
                      <td className="py-4">
                        <StatusPill tone={table.qrUrl ? 'green' : 'amber'}>
                          {table.qrUrl ? 'QR aktif' : 'Belum ada QR'}
                        </StatusPill>
                      </td>
                      <td className="py-4">
                        <div className="flex justify-end gap-1">
                          <button
                            type="button"
                            onClick={() => setQrTarget(table)}
                            disabled={!table.qrUrl}
                            aria-label={`Lihat QR ${table.name}`}
                            className="rounded-lg p-2 text-coffee-700 hover:bg-coffee-50 disabled:opacity-40"
                          >
                            <QrCode size={15} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setRegenerateTarget(table)}
                            aria-label={`Regenerasi QR ${table.name}`}
                            className="rounded-lg p-2 text-amber-700 hover:bg-amber-50"
                          >
                            <RefreshCw size={15} />
                          </button>
                          <button
                            type="button"
                            onClick={() => openEdit(table)}
                            aria-label={`Edit ${table.name}`}
                            className="rounded-lg p-2 text-coffee-700 hover:bg-coffee-50"
                          >
                            <Pencil size={15} />
                          </button>
                          <button
                            type="button"
                            onClick={() => setDeleteTarget(table)}
                            aria-label={`Hapus ${table.name}`}
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

        {!tables.loading &&
          !tables.error &&
          (tables.data?.length ?? 0) === 0 && (
            <EmptyState
              icon={QrCode}
              title="Belum ada meja"
              description="Tambahkan meja pertama. QR unik akan dibuat otomatis dan dapat dicetak."
              action={
                <button
                  type="button"
                  onClick={openCreate}
                  className={buttonClasses('primary')}
                >
                  <Plus size={16} />
                  Tambah meja
                </button>
              }
            />
          )}
      </div>

      {formOpen && (
        <TableFormModal
          key={editing?.id ?? 'new'}
          table={editing}
          onClose={() => setFormOpen(false)}
          onSaved={() => {
            setFormOpen(false)
            tables.reload()
          }}
        />
      )}

      {qrTarget && (
        <TableQrModal table={qrTarget} onClose={() => setQrTarget(null)} />
      )}

      {regenerateTarget && (
        <Modal
          title="Regenerasi QR?"
          onClose={() => setRegenerateTarget(null)}
        >
          <p className="text-sm leading-6 text-stone-600">
            QR lama untuk <b>{regenerateTarget.name}</b> akan langsung dicabut.
            Customer yang memakai QR lama harus scan ulang.
          </p>
          <div className="mt-6 flex justify-end gap-2">
            <button
              type="button"
              onClick={() => setRegenerateTarget(null)}
              className={buttonClasses('outline')}
            >
              Batal
            </button>
            <button
              type="button"
              onClick={() => void handleRegenerate()}
              className={buttonClasses('primary')}
            >
              <RefreshCw size={15} />
              Regenerasi
            </button>
          </div>
        </Modal>
      )}

      {deleteTarget && (
        <Modal title="Hapus meja?" onClose={() => setDeleteTarget(null)}>
          <p className="text-sm leading-6 text-stone-600">
            Meja <b>{deleteTarget.name}</b> akan dihapus. Meja yang memiliki
            riwayat pesanan tidak dapat dihapus; nonaktifkan saja.
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

