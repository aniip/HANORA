import {
  AlertCircle,
  Loader2,
  Pencil,
  Plus,
  RefreshCw,
  ShieldCheck,
  UserCheck,
  UserX,
} from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Modal } from '../../components/ui/Modal'
import { SectionTitle } from '../../components/ui/SectionTitle'
import { buttonClasses } from '../../components/ui/buttonStyles'
import { getApiErrorMessage } from '../../lib/apiError'
import { formatDate } from '../../lib/format'
import {
  createStaffUser,
  getStaffUsers,
  updateStaffUser,
  updateStaffUserStatus,
} from '../../lib/users'
import type { StaffRole, StaffUser } from '../../lib/users'

type UserForm = {
  name: string
  email: string
  password: string
  role: StaffRole
}

const emptyForm = (): UserForm => ({
  name: '',
  email: '',
  password: '',
  role: 'Cashier',
})

export function UserManagementPage() {
  const [users, setUsers] = useState<StaffUser[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [actionError, setActionError] = useState('')
  const [saving, setSaving] = useState(false)
  const [statusId, setStatusId] = useState<string | null>(null)

  const [createOpen, setCreateOpen] = useState(false)
  const [form, setForm] = useState<UserForm>(emptyForm)

  const [editTarget, setEditTarget] = useState<StaffUser | null>(null)
  const [editForm, setEditForm] = useState({
    name: '',
    email: '',
    role: 'Cashier' as StaffRole,
  })

  const load = useCallback(async () => {
    setLoading(true)
    setError('')

    try {
      const result = await getStaffUsers(1, 100)
      setUsers(result.items)
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    // oxlint-disable-next-line react/set-state-in-effect -- memuat data pengguna
    void load()
  }, [load])

  const submitCreate = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setActionError('')

    if (form.name.trim().length < 2) {
      setActionError('Nama minimal 2 karakter.')
      return
    }

    if (form.password.length < 12) {
      setActionError('Password minimal 12 karakter.')
      return
    }

    setSaving(true)

    try {
      await createStaffUser({
        name: form.name.trim(),
        email: form.email.trim(),
        password: form.password,
        role: form.role,
      })
      setCreateOpen(false)
      setForm(emptyForm())
      await load()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setSaving(false)
    }
  }

  const openEdit = (user: StaffUser) => {
    setEditTarget(user)
    setEditForm({
      name: user.name,
      email: user.email,
      role: user.role === 'Owner' ? 'Owner' : 'Cashier',
    })
    setActionError('')
  }

  const submitEdit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()

    if (!editTarget) {
      return
    }

    setActionError('')
    setSaving(true)

    try {
      await updateStaffUser(editTarget.id, {
        name: editForm.name.trim(),
        email: editForm.email.trim(),
        role: editForm.role,
      })
      setEditTarget(null)
      await load()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setSaving(false)
    }
  }

  const toggleStatus = async (user: StaffUser) => {
    setStatusId(user.id)
    setActionError('')

    try {
      await updateStaffUserStatus(user.id, !user.isActive)
      await load()
    } catch (exception) {
      setActionError(getApiErrorMessage(exception))
    } finally {
      setStatusId(null)
    }
  }

  return (
    <div>
      <SectionTitle
        eyebrow="Akses staf"
        title="Pengguna"
        description="Kelola akun owner dan kasir. Password baru wajib diganti saat login pertama."
        action={
          <button
            type="button"
            onClick={() => {
              setForm(emptyForm())
              setActionError('')
              setCreateOpen(true)
            }}
            className={buttonClasses('primary', '!px-4 !py-2')}
          >
            <Plus size={15} />
            Tambah pengguna
          </button>
        }
      />

      {error && (
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
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

      {actionError && (
        <div className="mb-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
          <AlertCircle size={15} className="mt-0.5 shrink-0" />
          {actionError}
        </div>
      )}

      <div className="overflow-hidden rounded-2xl border border-coffee-100 bg-white shadow-soft">
        <div className="flex items-center justify-between border-b border-coffee-100 px-4 py-3">
          <h2 className="text-sm font-bold text-coffee-950">Akun internal</h2>
          <button
            type="button"
            onClick={() => void load()}
            className={buttonClasses('outline', '!px-3 !py-2 text-xs')}
          >
            <RefreshCw size={14} />
            Muat ulang
          </button>
        </div>

        <div className="overflow-x-auto">
          <table className="min-w-full text-left text-xs">
            <thead className="bg-coffee-50 text-[10px] uppercase tracking-[0.12em] text-stone-500">
              <tr>
                <th className="px-4 py-3 font-semibold">Nama</th>
                <th className="px-4 py-3 font-semibold">Email</th>
                <th className="px-4 py-3 font-semibold">Role</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="px-4 py-3 font-semibold">Login terakhir</th>
                <th className="px-4 py-3 text-right font-semibold">Aksi</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-coffee-50">
              {loading && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-stone-500">
                    <span className="inline-flex items-center gap-2">
                      <Loader2 size={15} className="animate-spin" />
                      Memuat pengguna...
                    </span>
                  </td>
                </tr>
              )}

              {!loading && users.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-stone-500">
                    Belum ada pengguna.
                  </td>
                </tr>
              )}

              {!loading &&
                users.map((user) => (
                  <tr key={user.id} className="hover:bg-coffee-50/50">
                    <td className="px-4 py-3 font-semibold text-coffee-950">
                      {user.name}
                      {user.mustChangePassword && (
                        <span className="ml-2 rounded-full bg-amber-50 px-2 py-0.5 text-[9px] font-bold text-amber-700">
                          Wajib ganti password
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-stone-600">{user.email}</td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-[10px] font-semibold ${
                          user.role === 'Owner'
                            ? 'bg-coffee-100 text-coffee-800'
                            : 'bg-stone-100 text-stone-600'
                        }`}
                      >
                        <ShieldCheck size={11} />
                        {user.role === 'Owner' ? 'Owner' : 'Kasir'}
                      </span>
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-[10px] font-semibold ${
                          user.isActive
                            ? 'bg-emerald-50 text-emerald-700'
                            : 'bg-rose-50 text-rose-700'
                        }`}
                      >
                        {user.isActive ? (
                          <UserCheck size={11} />
                        ) : (
                          <UserX size={11} />
                        )}
                        {user.isActive ? 'Aktif' : 'Nonaktif'}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-stone-500">
                      {user.lastLoginAt ? formatDate(user.lastLoginAt) : '—'}
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-right">
                      <div className="inline-flex gap-1">
                        <button
                          type="button"
                          onClick={() => openEdit(user)}
                          aria-label={`Ubah ${user.name}`}
                          className="rounded-lg p-2 text-coffee-600 hover:bg-coffee-100"
                        >
                          <Pencil size={14} />
                        </button>
                        <button
                          type="button"
                          onClick={() => void toggleStatus(user)}
                          disabled={statusId === user.id}
                          aria-label={`${user.isActive ? 'Nonaktifkan' : 'Aktifkan'} ${user.name}`}
                          className={`rounded-lg p-2 disabled:opacity-50 ${
                            user.isActive
                              ? 'text-rose-600 hover:bg-rose-50'
                              : 'text-emerald-600 hover:bg-emerald-50'
                          }`}
                        >
                          {statusId === user.id ? (
                            <Loader2 size={14} className="animate-spin" />
                          ) : user.isActive ? (
                            <UserX size={14} />
                          ) : (
                            <UserCheck size={14} />
                          )}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </div>

      {createOpen && (
        <Modal title="Tambah pengguna" onClose={() => setCreateOpen(false)}>
          <form onSubmit={(event) => void submitCreate(event)} className="space-y-4">
            <label className="block text-xs font-semibold text-coffee-800">
              Nama
              <input
                value={form.name}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
                required
                minLength={2}
                maxLength={100}
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
            </label>

            <label className="block text-xs font-semibold text-coffee-800">
              Email
              <input
                type="email"
                value={form.email}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    email: event.target.value,
                  }))
                }
                required
                maxLength={254}
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
            </label>

            <label className="block text-xs font-semibold text-coffee-800">
              Password awal
              <input
                type="password"
                value={form.password}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    password: event.target.value,
                  }))
                }
                required
                minLength={12}
                maxLength={128}
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
              <span className="mt-1 block text-[10px] font-normal text-stone-400">
                Minimal 12 karakter. Pengguna wajib mengganti saat login pertama.
              </span>
            </label>

            <label className="block text-xs font-semibold text-coffee-800">
              Role
              <select
                value={form.role}
                onChange={(event) =>
                  setForm((current) => ({
                    ...current,
                    role: event.target.value as StaffRole,
                  }))
                }
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              >
                <option value="Cashier">Kasir</option>
                <option value="Owner">Owner</option>
              </select>
            </label>

            {actionError && (
              <div className="rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
                {actionError}
              </div>
            )}

            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setCreateOpen(false)}
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

      {editTarget && (
        <Modal
          title={`Ubah ${editTarget.name}`}
          onClose={() => setEditTarget(null)}
        >
          <form onSubmit={(event) => void submitEdit(event)} className="space-y-4">
            <label className="block text-xs font-semibold text-coffee-800">
              Nama
              <input
                value={editForm.name}
                onChange={(event) =>
                  setEditForm((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
                required
                minLength={2}
                maxLength={100}
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
            </label>

            <label className="block text-xs font-semibold text-coffee-800">
              Email
              <input
                type="email"
                value={editForm.email}
                onChange={(event) =>
                  setEditForm((current) => ({
                    ...current,
                    email: event.target.value,
                  }))
                }
                required
                maxLength={254}
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              />
            </label>

            <label className="block text-xs font-semibold text-coffee-800">
              Role
              <select
                value={editForm.role}
                onChange={(event) =>
                  setEditForm((current) => ({
                    ...current,
                    role: event.target.value as StaffRole,
                  }))
                }
                className="mt-1 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
              >
                <option value="Cashier">Kasir</option>
                <option value="Owner">Owner</option>
              </select>
            </label>

            {actionError && (
              <div className="rounded-xl bg-rose-50 p-3 text-xs text-rose-700">
                {actionError}
              </div>
            )}

            <div className="flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setEditTarget(null)}
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
    </div>
  )
}
