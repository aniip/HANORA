import {
  AlertCircle,
  Copy,
  Download,
  Loader2,
  QrCode,
} from 'lucide-react'
import { useEffect, useState } from 'react'
import { getTableQr } from '../../lib/tables'
import type { Table } from '../../lib/tables'
import { getApiErrorMessage } from '../../lib/apiError'
import { Modal } from '../ui/Modal'
import { buttonClasses } from '../ui/buttonStyles'

type TableQrModalProps = {
  table: Table
  onClose: () => void
}

export function TableQrModal({ table, onClose }: TableQrModalProps) {
  const [qrUrl, setQrUrl] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    let objectUrl: string | null = null
    let active = true

    getTableQr(table.id, 'png')
      .then((blob) => {
        if (!active) {
          return
        }

        objectUrl = URL.createObjectURL(blob)
        setQrUrl(objectUrl)
      })
      .catch((exception: unknown) => {
        if (active) {
          setError(getApiErrorMessage(exception))
        }
      })
      .finally(() => {
        if (active) {
          setLoading(false)
        }
      })

    return () => {
      active = false
      if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
      }
    }
  }, [table.id])

  const copyUrl = async () => {
    if (!table.qrUrl) {
      return
    }

    try {
      await navigator.clipboard.writeText(table.qrUrl)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      setCopied(false)
    }
  }

  return (
    <Modal title={`QR ${table.name}`} onClose={onClose}>
      <div className="flex flex-col items-center gap-4">
        <div className="grid h-64 w-64 place-items-center rounded-2xl border border-coffee-100 bg-white p-4">
          {loading && <Loader2 size={28} className="animate-spin text-coffee-500" />}
          {!loading && error && (
            <div className="px-4 text-center text-xs text-rose-700">
              <AlertCircle className="mx-auto mb-2" />
              {error}
            </div>
          )}
          {!loading && qrUrl && (
            <img
              src={qrUrl}
              alt={`QR ${table.name}`}
              className="h-full w-full object-contain"
            />
          )}
        </div>

        <div className="w-full rounded-2xl bg-coffee-50 p-4">
          <p className="text-[10px] font-bold uppercase tracking-[0.18em] text-stone-400">
            URL QR meja
          </p>
          <p className="mt-2 break-all text-xs text-coffee-800">
            {table.qrUrl ?? 'QR belum tersedia.'}
          </p>
          <p className="mt-2 text-[10px] leading-5 text-stone-500">
            Cetak QR ini dan tempel di meja. Regenerasi QR akan mencabut QR lama.
          </p>
        </div>

        <div className="flex w-full flex-wrap gap-2">
          {table.qrUrl && (
            <button
              type="button"
              onClick={() => void copyUrl()}
              className={buttonClasses('outline', 'flex-1 !px-3 !py-2')}
            >
              <Copy size={14} />
              {copied ? 'Tersalin' : 'Salin URL'}
            </button>
          )}
          {qrUrl && (
            <a
              href={qrUrl}
              download={`qr-${table.number}.png`}
              className={buttonClasses('primary', 'flex-1 !px-3 !py-2')}
            >
              <Download size={14} />
              Unduh QR
            </a>
          )}
        </div>

        {!qrUrl && !loading && !error && (
          <p className="flex items-center gap-2 text-xs text-stone-500">
            <QrCode size={15} />
            QR belum dapat dibuat.
          </p>
        )}
      </div>
    </Modal>
  )
}
