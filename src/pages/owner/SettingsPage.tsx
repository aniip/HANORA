import {
  AlertCircle,
  CheckCircle2,
  Loader2,
  Save,
} from 'lucide-react'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { buttonClasses } from '../../components/ui/buttonStyles'
import { SectionTitle } from '../../components/ui/SectionTitle'
import { getOwnerSettings, updateOwnerSettings } from '../../lib/catalog'
import type { PublicSite, SiteSettingsInput } from '../../lib/catalog'
import { getApiErrorMessage } from '../../lib/apiError'
import { useApi } from '../../lib/useApi'

function SettingsForm({
  initial,
  onSaved,
}: {
  initial: PublicSite
  onSaved: () => void
}) {
  const [name, setName] = useState(initial.name)
  const [tagline, setTagline] = useState(initial.tagline)
  const [description, setDescription] = useState(initial.description)
  const [address, setAddress] = useState(initial.address)
  const [phone, setPhone] = useState(initial.phone)
  const [instagram, setInstagram] = useState(initial.instagram)
  const [openingHours, setOpeningHours] = useState(initial.openingHours)
  const [mapsUrl, setMapsUrl] = useState(initial.mapsUrl)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [saving, setSaving] = useState(false)

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError('')
    setSuccess('')

    if (!name.trim()) {
      setError('Nama bisnis wajib diisi.')
      return
    }

    const input: SiteSettingsInput = {
      name: name.trim(),
      tagline: tagline.trim(),
      description: description.trim(),
      address: address.trim(),
      phone: phone.trim(),
      instagram: instagram.trim(),
      openingHours: openingHours.trim(),
      mapsUrl: mapsUrl.trim(),
    }

    setSaving(true)

    try {
      await updateOwnerSettings(input)
      setSuccess('Pengaturan berhasil disimpan dan langsung tampil di website publik.')
      onSaved()
    } catch (exception) {
      setError(getApiErrorMessage(exception))
    } finally {
      setSaving(false)
    }
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="rounded-2xl border border-coffee-100 bg-white p-5 shadow-soft md:p-6"
    >
      <div className="grid gap-4 md:grid-cols-2">
        <label className="block text-xs font-semibold text-coffee-800 md:col-span-2">
          Nama bisnis
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            maxLength={100}
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800 md:col-span-2">
          Tagline
          <input
            value={tagline}
            onChange={(event) => setTagline(event.target.value)}
            maxLength={160}
            placeholder="A little pause, a lot of warmth."
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800 md:col-span-2">
          Deskripsi
          <textarea
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            rows={4}
            maxLength={1000}
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Alamat
          <input
            value={address}
            onChange={(event) => setAddress(event.target.value)}
            maxLength={255}
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Jam operasional
          <input
            value={openingHours}
            onChange={(event) => setOpeningHours(event.target.value)}
            maxLength={100}
            placeholder="Setiap hari Â· 09.00â€“22.00 WIB"
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Telepon
          <input
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            maxLength={30}
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800">
          Instagram
          <input
            value={instagram}
            onChange={(event) => setInstagram(event.target.value)}
            maxLength={60}
            placeholder="@hanoracafe"
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>

        <label className="block text-xs font-semibold text-coffee-800 md:col-span-2">
          URL Google Maps
          <input
            type="url"
            value={mapsUrl}
            onChange={(event) => setMapsUrl(event.target.value)}
            maxLength={500}
            placeholder="https://maps.google.com/..."
            className="mt-2 w-full rounded-xl border border-coffee-200 p-3 text-sm outline-none focus:border-coffee-600"
          />
        </label>
      </div>

      {error && (
        <div
          role="alert"
          className="mt-4 flex gap-2 rounded-xl bg-rose-50 p-3 text-xs leading-5 text-rose-700"
        >
          <AlertCircle size={16} className="mt-0.5 shrink-0" />
          {error}
        </div>
      )}

      {success && (
        <div className="mt-4 flex gap-2 rounded-xl bg-emerald-50 p-3 text-xs leading-5 text-emerald-800">
          <CheckCircle2 size={16} className="mt-0.5 shrink-0" />
          {success}
        </div>
      )}

      <div className="mt-5 flex justify-end">
        <button
          type="submit"
          disabled={saving}
          className={buttonClasses('primary', 'disabled:opacity-60')}
        >
          {saving ? (
            <Loader2 size={15} className="animate-spin" />
          ) : (
            <Save size={15} />
          )}
          {saving ? 'Menyimpan...' : 'Simpan pengaturan'}
        </button>
      </div>
    </form>
  )
}

export function SettingsPage() {
  const settings = useApi(getOwnerSettings, [])

  return (
    <div>
      <SectionTitle
        eyebrow="Business settings"
        title="Pengaturan bisnis"
        description="Informasi ini tampil di beranda, halaman lokasi, dan footer website publik."
      />

      {settings.loading && !settings.data && (
        <div className="h-96 animate-pulse rounded-2xl bg-coffee-100" />
      )}

      {!settings.loading && settings.error && (
        <div className="rounded-2xl border border-rose-100 bg-rose-50 p-5 text-sm text-rose-700">
          {settings.error}{' '}
          <button
            type="button"
            onClick={settings.reload}
            className="font-bold underline"
          >
            Coba lagi
          </button>
        </div>
      )}

      {settings.data && (
        <SettingsForm
          initial={settings.data}
          onSaved={settings.reload}
        />
      )}
    </div>
  )
}

