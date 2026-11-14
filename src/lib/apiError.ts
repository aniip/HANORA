import axios from 'axios'

type ProblemDetails = {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export function getApiErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as ProblemDetails | undefined

    if (data?.errors) {
      const firstError = Object.values(data.errors).flat()[0]
      if (firstError) {
        return firstError
      }
    }

    if (data?.detail) {
      return data.detail
    }

    if (data?.title) {
      return data.title
    }

    if (!error.response) {
      return 'Tidak dapat terhubung ke server. Periksa koneksi dan pastikan API berjalan.'
    }
  }

  return 'Terjadi kesalahan yang tidak terduga. Silakan coba lagi.'
}
